using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FastDFS.Client.Exceptions;
using FastDFS.Client.Protocol;
using FastDFS.Client.Protocol.Responses;
using FastDFS.Client.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FastDFS.Client.Connection
{
    /// <summary>
    /// Represents a TCP connection to a FastDFS server (Tracker or Storage).
    /// Uses high-performance Socket for better performance and control.
    /// </summary>
    public class FastDFSConnection : IDisposable
    {
        // Shared round-robin counter for DNS multi-IP load distribution
        private static int _dnsRoundRobinIndex;

        private Socket? _socket;
        private readonly string _host;
        private readonly int _port;
        private readonly int _connectTimeout;
        private readonly int _sendTimeout;
        private readonly int _receiveTimeout;
        private readonly ILogger _logger;
        private int _poisoned;
        private bool _disposed;

        /// <summary>
        /// Gets the remote endpoint in the format "host:port".
        /// </summary>
        public string RemoteEndpoint { get; }

        /// <summary>
        /// Gets the time when this connection was created.
        /// </summary>
        public DateTime CreatedTime { get; }

        /// <summary>
        /// Gets or sets the time when this connection was last used.
        /// </summary>
        public DateTime LastUsedTime { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this connection is alive and usable.
        /// Uses a lightweight check (Connected flag only) by default.
        /// Call <see cref="PollIsAlive"/> for a deeper check when the connection has been idle.
        /// </summary>
        public bool IsAlive
        {
            get
            {
                if (_disposed || Volatile.Read(ref _poisoned) == 1 || _socket == null)
                    return false;

                try
                {
                    return _socket.Connected;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Performs a deeper liveness check using socket polling.
        /// Use this when the connection has been sitting idle in the pool,
        /// to detect server-side closes before attempting to reuse it.
        /// </summary>
        public bool PollIsAlive()
        {
            if (_disposed || Volatile.Read(ref _poisoned) == 1 || _socket == null)
                return false;

            try
            {
                if (!_socket.Connected)
                    return false;

                // Poll with 1ms timeout: true + no data available = remote closed
                bool readReady = _socket.Poll(1000, SelectMode.SelectRead);
                if (readReady && _socket.Available == 0)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSConnection"/> class.
        /// </summary>
        /// <param name="host">The server host.</param>
        /// <param name="port">The server port.</param>
        /// <param name="connectTimeout">The connect timeout in milliseconds (0 = infinite).</param>
        /// <param name="sendTimeout">The send timeout in milliseconds (0 = infinite).</param>
        /// <param name="receiveTimeout">The receive timeout in milliseconds (0 = infinite).</param>
        /// <param name="logger">Optional logger instance.</param>
        public FastDFSConnection(
            string host,
            int port,
            int connectTimeout = 30000,
            int sendTimeout = 30000,
            int receiveTimeout = 30000,
            ILogger<FastDFSConnection>? logger = null)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host cannot be null or empty.", nameof(host));
            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");

            _host = host;
            _port = port;
            _connectTimeout = connectTimeout;
            _sendTimeout = sendTimeout;
            _receiveTimeout = receiveTimeout;
            _logger = logger ?? NullLogger<FastDFSConnection>.Instance;
            RemoteEndpoint = $"{host}:{port}";
            CreatedTime = DateTime.UtcNow;
            LastUsedTime = CreatedTime;
        }

        /// <summary>
        /// Gets a value indicating whether this connection has entered an unusable state and must not be returned to the pool.
        /// </summary>
        public bool IsPoisoned => Volatile.Read(ref _poisoned) == 1;

        /// <summary>
        /// Connects to the FastDFS server asynchronously using high-performance Socket.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FastDFSConnection));

            try
            {
                _logger.LogDebug("Resolving DNS for {Host}...", _host);

                var addresses = await WaitWithTimeoutAsync(
                    Dns.GetHostAddressesAsync(_host),
                    _connectTimeout,
                    $"DNS resolution for {_host}",
                    cancellationToken).ConfigureAwait(false);
                if (addresses == null || addresses.Length == 0)
                    throw new SocketException((int)SocketError.HostNotFound);

                cancellationToken.ThrowIfCancellationRequested();

                // Pick a random address to distribute load across multi-IP DNS entries
                // Use unsigned modulo to avoid negative index after int overflow (~2 billion increments)
                var address = addresses.Length == 1
                    ? addresses[0]
                    : addresses[(uint)System.Threading.Interlocked.Increment(ref _dnsRoundRobinIndex) % addresses.Length];
                _socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                {
                    // Configure socket options for better performance
                    NoDelay = true, // Disable Nagle's algorithm for low latency
                    SendBufferSize = 64 * 1024, // 64KB send buffer
                    ReceiveBufferSize = 64 * 1024 // 64KB receive buffer
                };

                _logger.LogDebug("Connecting to {Endpoint} (IP: {IP})...", RemoteEndpoint, address);

                var endpoint = new IPEndPoint(address, _port);
                await ExecuteSocketOperationAsync(
                    () => _socket.ConnectAsync(endpoint),
                    _connectTimeout,
                    "Connect",
                    cancellationToken).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                LastUsedTime = DateTime.UtcNow;
                _logger.LogInformation("Successfully connected to {Endpoint}", RemoteEndpoint);
            }
            catch (OperationCanceledException)
            {
                AbortForCancellation();
                throw;
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested && (ex is SocketException || ex is ObjectDisposedException))
            {
                AbortForCancellation();
                throw new OperationCanceledException($"Connection attempt to {RemoteEndpoint} was cancelled.", ex, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to {Endpoint}", RemoteEndpoint);

                // Clean up socket on failure
                InvalidateConnection();

                throw new FastDFSNetworkException(
                    $"Failed to connect to {RemoteEndpoint}.",
                    RemoteEndpoint,
                    ex);
            }
        }

        /// <summary>
        /// Sends a request and receives a response asynchronously.
        /// </summary>
        /// <typeparam name="TRequest">The request type.</typeparam>
        /// <typeparam name="TResponse">The response type.</typeparam>
        /// <param name="request">The request to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The response from the server.</returns>
        public async Task<TResponse> SendRequestAsync<TRequest, TResponse>(
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : FastDFSRequest<TResponse>
            where TResponse : IFastDFSResponse, new()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FastDFSConnection));
            if (_socket == null || !_socket.Connected)
                throw new InvalidOperationException("Connection is not established. Call ConnectAsync first.");
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                _logger.LogDebug("Sending request to {Endpoint}: {RequestType}", RemoteEndpoint, typeof(TRequest).Name);

                // Send the request
                await SendAsync(request, cancellationToken).ConfigureAwait(false);

                // Receive the response
                var response = await ReceiveAsync<TResponse>(cancellationToken).ConfigureAwait(false);

                LastUsedTime = DateTime.UtcNow;

                _logger.LogDebug("Received response from {Endpoint}: {ResponseType}", RemoteEndpoint, typeof(TResponse).Name);

                return response;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (FastDFSException ex)
            {
                _logger.LogWarning(ex, "FastDFS protocol error communicating with {Endpoint}", RemoteEndpoint);
                // Re-throw FastDFS-specific exceptions
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Network error communicating with {Endpoint}", RemoteEndpoint);
                throw new FastDFSNetworkException(
                    $"Error communicating with {RemoteEndpoint}.",
                    RemoteEndpoint,
                    ex);
            }
        }

        /// <summary>
        /// Sends an upload or appender-upload request to the server while streaming the file body directly from the source stream.
        /// </summary>
        public async Task<UploadFileResponse> SendUploadRequestAsync(
            byte command,
            byte storePathIndex,
            Stream contentStream,
            long contentLength,
            string fileExtension,
            int bufferSize,
            CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FastDFSConnection));
            if (_socket == null || !_socket.Connected)
                throw new InvalidOperationException("Connection is not established. Call ConnectAsync first.");
            if (contentStream == null)
                throw new ArgumentNullException(nameof(contentStream));
            if (!contentStream.CanRead)
                throw new ArgumentException("Content stream must be readable.", nameof(contentStream));
            if (contentLength < 0)
                throw new ArgumentOutOfRangeException(nameof(contentLength), "Content length cannot be negative.");
            if (string.IsNullOrEmpty(fileExtension))
                throw new ArgumentException("File extension cannot be null or empty.", nameof(fileExtension));
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be greater than 0.");

            var extension = fileExtension.StartsWith(".", StringComparison.Ordinal)
                ? fileExtension.Substring(1)
                : fileExtension;

            var bodyPrefix = new byte[1 + 8 + FastDFSConstants.FileExtNameMaxLength];
            bodyPrefix[0] = storePathIndex;
            ByteConverter.WriteInt64(contentLength, bodyPrefix, 1);
            ByteExtensions.CopyFixedString(extension, bodyPrefix, 9, FastDFSConstants.FileExtNameMaxLength);

            bool requestStarted = false;
            bool responseReceived = false;

            try
            {
                _logger.LogDebug("Streaming upload request to {Endpoint}: Command={Command}, Length={Length}", RemoteEndpoint, command, contentLength);

                var header = new FastDFSHeader(bodyPrefix.Length + contentLength, command, 0).ToBytes();
                requestStarted = true;
                await SendBufferAsync(header, 0, header.Length, cancellationToken).ConfigureAwait(false);
                await SendBufferAsync(bodyPrefix, 0, bodyPrefix.Length, cancellationToken).ConfigureAwait(false);
                await SendStreamAsync(contentStream, contentLength, bufferSize, cancellationToken).ConfigureAwait(false);

                var response = await ReceiveAsync<UploadFileResponse>(cancellationToken).ConfigureAwait(false);
                responseReceived = true;
                LastUsedTime = DateTime.UtcNow;
                return response;
            }
            catch (OperationCanceledException)
            {
                AbortForCancellation();
                throw;
            }
            catch (FastDFSException)
            {
                if (requestStarted && !responseReceived)
                {
                    InvalidateConnection();
                }

                throw;
            }
            catch (Exception ex)
            {
                if (requestStarted)
                {
                    InvalidateConnection();
                }

                _logger.LogError(ex, "Network error during streaming upload to {Endpoint}", RemoteEndpoint);
                throw new FastDFSNetworkException(
                    $"Error communicating with {RemoteEndpoint}.",
                    RemoteEndpoint,
                    ex);
            }
        }

        /// <summary>
        /// Sends a download request and copies the response body directly to the destination stream.
        /// </summary>
        public async Task DownloadToStreamAsync(
            IFastDFSRequest request,
            Stream destination,
            int bufferSize,
            CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FastDFSConnection));
            if (_socket == null || !_socket.Connected)
                throw new InvalidOperationException("Connection is not established. Call ConnectAsync first.");
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (!destination.CanWrite)
                throw new ArgumentException("Destination stream must be writable.", nameof(destination));
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be greater than 0.");

            bool requestSent = false;
            bool responseCompleted = false;

            try
            {
                _logger.LogDebug("Streaming download request to {Endpoint}: {RequestType}", RemoteEndpoint, request.GetType().Name);
                await SendAsync(request, cancellationToken).ConfigureAwait(false);
                requestSent = true;
                await ReceiveToStreamAsync(destination, bufferSize, cancellationToken).ConfigureAwait(false);
                responseCompleted = true;
                LastUsedTime = DateTime.UtcNow;
            }
            catch (OperationCanceledException)
            {
                AbortForCancellation();
                throw;
            }
            catch (FastDFSException)
            {
                if (requestSent && !responseCompleted)
                {
                    InvalidateConnection();
                }

                throw;
            }
            catch (Exception ex)
            {
                if (requestSent)
                {
                    InvalidateConnection();
                }

                _logger.LogError(ex, "Network error during streaming download from {Endpoint}", RemoteEndpoint);
                throw new FastDFSNetworkException(
                    $"Error communicating with {RemoteEndpoint}.",
                    RemoteEndpoint,
                    ex);
            }
        }
 
        /// <summary>
        /// Sends a request packet to the server asynchronously using Socket.
        /// </summary>
        /// <param name="request">The request to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task SendAsync(IFastDFSRequest request, CancellationToken cancellationToken)
        {
            if (_socket == null || !_socket.Connected)
                throw new InvalidOperationException("Socket is not connected.");

            byte[] packetBytes = request.Encode();

            try
            {
                _logger.LogTrace("Sending {ByteCount} bytes to {Endpoint}", packetBytes.Length, RemoteEndpoint);
                await SendBufferAsync(packetBytes, 0, packetBytes.Length, cancellationToken).ConfigureAwait(false);
                _logger.LogTrace("Successfully sent {ByteCount} bytes to {Endpoint}", packetBytes.Length, RemoteEndpoint);
            }
            catch (OperationCanceledException)
            {
                AbortForCancellation();
                throw;
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested && (ex is SocketException || ex is ObjectDisposedException))
            {
                AbortForCancellation();
                throw new OperationCanceledException($"Send operation to {RemoteEndpoint} was cancelled.", ex, cancellationToken);
            }
            catch (SocketException ex)
            {
                InvalidateConnection();
                _logger.LogError(ex, "Failed to send {ByteCount} bytes to {Endpoint}", packetBytes.Length, RemoteEndpoint);
                throw new FastDFSNetworkException(
                    $"Failed to send data to {RemoteEndpoint}.",
                    RemoteEndpoint,
                    ex);
            }
        }

        /// <summary>
        /// Receives a response packet from the server asynchronously using Socket.
        /// </summary>
        /// <typeparam name="TResponse">The response type.</typeparam>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The parsed response.</returns>
        private async Task<TResponse> ReceiveAsync<TResponse>(CancellationToken cancellationToken)
            where TResponse : IFastDFSResponse, new()
        {
            if (_socket == null || !_socket.Connected)
                throw new InvalidOperationException("Socket is not connected.");

            // Read the header (10 bytes)
            byte[] headerBuffer = ArrayPool<byte>.Shared.Rent(FastDFSHeader.HeaderSize);
            try
            {
                _logger.LogTrace("Reading header ({HeaderSize} bytes) from {Endpoint}", FastDFSHeader.HeaderSize, RemoteEndpoint);
                await ReadExactlyAsync(headerBuffer, 0, FastDFSHeader.HeaderSize, cancellationToken).ConfigureAwait(false);

                // Parse the header
                var header = FastDFSHeader.Parse(headerBuffer, 0);
                _logger.LogTrace("Received header from {Endpoint}: Command={Command}, Status={Status}, BodyLength={BodyLength}",
                    RemoteEndpoint, header.Command, header.Status, header.BodyLength);

                // Read the body if present
                ValidateResponseBodyLength(header);

                if (header.BodyLength > 0)
                {
                    int bodyLength = (int)header.BodyLength;

                    // Decode() stores the body reference on the response object, so we cannot
                    // use ArrayPool here — the buffer would be returned while still referenced.
                    // Allocate exactly the required size and read directly into it (zero-copy).
                    byte[] body = new byte[bodyLength];
                    _logger.LogTrace("Reading body ({BodyLength} bytes) from {Endpoint}", bodyLength, RemoteEndpoint);
                    await ReadExactlyAsync(body, 0, bodyLength, cancellationToken).ConfigureAwait(false);

                    var response = new TResponse();
                    response.Decode(header, body);

                    if (!response.IsSuccess)
                    {
                        _logger.LogWarning("FastDFS server {Endpoint} returned error status: {Status}", RemoteEndpoint, header.Status);
                        throw new FastDFSProtocolException(
                            $"FastDFS server returned error. Status code: {header.Status}",
                            header.Status);
                    }

                    return response;
                }
                else
                {
                    // No body, just create and decode with header only
                    var response = new TResponse();
                    response.Decode(header, null);

                    if (!response.IsSuccess)
                    {
                        throw new FastDFSProtocolException(
                            $"FastDFS server returned error. Status code: {header.Status}",
                            header.Status);
                    }

                    return response;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (FastDFSException)
            {
                // Re-throw FastDFS-specific exceptions
                throw;
            }
            catch (Exception ex)
            {
                InvalidateConnection();
                throw new FastDFSNetworkException(
                    $"Failed to receive data from {RemoteEndpoint}.",
                    RemoteEndpoint,
                    ex);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(headerBuffer);
            }
        }

        private async Task ReceiveToStreamAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            byte[] headerBuffer = ArrayPool<byte>.Shared.Rent(FastDFSHeader.HeaderSize);
            try
            {
                await ReadExactlyAsync(headerBuffer, 0, FastDFSHeader.HeaderSize, cancellationToken).ConfigureAwait(false);
                var header = FastDFSHeader.Parse(headerBuffer, 0);
                ValidateResponseBodyLength(header, allowLargeBodies: true);

                if (!header.IsSuccess)
                {
                    if (header.BodyLength > 0)
                    {
                        await DrainBodyAsync(header.BodyLength, bufferSize, cancellationToken).ConfigureAwait(false);
                    }

                    throw new FastDFSProtocolException(
                        $"FastDFS server returned error. Status code: {header.Status}",
                        header.Status);
                }

                long remaining = header.BodyLength;
                if (remaining == 0)
                    return;

                byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                try
                {
                    while (remaining > 0)
                    {
                        int chunkSize = remaining > buffer.Length ? buffer.Length : (int)remaining;
                        await ReadExactlyAsync(buffer, 0, chunkSize, cancellationToken).ConfigureAwait(false);
                        await destination.WriteAsync(buffer, 0, chunkSize, cancellationToken).ConfigureAwait(false);
                        remaining -= chunkSize;
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(headerBuffer);
            }
        }

        private async Task DrainBodyAsync(long length, int bufferSize, CancellationToken cancellationToken)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                long remaining = length;
                while (remaining > 0)
                {
                    int chunkSize = remaining > buffer.Length ? buffer.Length : (int)remaining;
                    await ReadExactlyAsync(buffer, 0, chunkSize, cancellationToken).ConfigureAwait(false);
                    remaining -= chunkSize;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private async Task SendStreamAsync(Stream contentStream, long contentLength, int bufferSize, CancellationToken cancellationToken)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                long remaining = contentLength;
                while (remaining > 0)
                {
                    int chunkSize = remaining > buffer.Length ? buffer.Length : (int)remaining;
                    int read = await contentStream.ReadAsync(buffer, 0, chunkSize, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        throw new EndOfStreamException(
                            $"Content stream ended before the declared content length was transmitted. Remaining bytes: {remaining}.");
                    }

                    await SendBufferAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                    remaining -= read;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private async Task SendBufferAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_socket == null || !_socket.Connected)
                throw new InvalidOperationException("Socket is not connected.");

            int totalSent = 0;
            while (totalSent < count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var segment = new ArraySegment<byte>(buffer, offset + totalSent, count - totalSent);
                int sent = await ExecuteSocketOperationAsync(
                    () => _socket.SendAsync(segment, SocketFlags.None),
                    _sendTimeout,
                    "Send",
                    cancellationToken).ConfigureAwait(false);

                if (sent == 0)
                    throw new SocketException((int)SocketError.ConnectionReset);

                totalSent += sent;
            }
        }
 
        /// <summary>
        /// Reads exactly the specified number of bytes from the socket.
        /// </summary>
        /// <param name="buffer">The buffer to read into.</param>
        /// <param name="offset">The offset in the buffer to start writing.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task ReadExactlyAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            if (_socket == null || !_socket.Connected)
                throw new InvalidOperationException("Socket is not connected.");

            int totalRead = 0;
            while (totalRead < count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var segment = new ArraySegment<byte>(buffer, offset + totalRead, count - totalRead);
                int bytesRead = await ExecuteSocketOperationAsync(
                    () => _socket.ReceiveAsync(segment, SocketFlags.None),
                    _receiveTimeout,
                    "Receive",
                    cancellationToken).ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    throw new EndOfStreamException(
                        $"Connection closed unexpectedly. Expected {count} bytes but received {totalRead} bytes.");
                }

                totalRead += bytesRead;
            }
        }

        private async Task WaitWithTimeoutAsync(Task operation, int timeoutMilliseconds, string operationName, CancellationToken cancellationToken)
        {
            if (timeoutMilliseconds <= 0)
            {
                await operation.ConfigureAwait(false);
                return;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task timeoutTask = Task.Delay(timeoutMilliseconds, linkedCts.Token);
            Task completedTask = await Task.WhenAny(operation, timeoutTask).ConfigureAwait(false);

            if (completedTask == timeoutTask)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"{operationName} timed out after {timeoutMilliseconds} ms.");
            }

            linkedCts.Cancel();
            await operation.ConfigureAwait(false);
        }

        private async Task<T> WaitWithTimeoutAsync<T>(Task<T> operation, int timeoutMilliseconds, string operationName, CancellationToken cancellationToken)
        {
            if (timeoutMilliseconds <= 0)
                return await operation.ConfigureAwait(false);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task timeoutTask = Task.Delay(timeoutMilliseconds, linkedCts.Token);
            Task completedTask = await Task.WhenAny(operation, timeoutTask).ConfigureAwait(false);

            if (completedTask == timeoutTask)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"{operationName} timed out after {timeoutMilliseconds} ms.");
            }

            linkedCts.Cancel();
            return await operation.ConfigureAwait(false);
        }

        private async Task ExecuteSocketOperationAsync(Func<Task> operation, int timeoutMilliseconds, string operationName, CancellationToken cancellationToken)
        {
            using var timeoutCts = timeoutMilliseconds > 0 ? new CancellationTokenSource(timeoutMilliseconds) : null;
            using var linkedCts = timeoutCts != null
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
                : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            using (linkedCts.Token.Register(static state => ((FastDFSConnection)state!).AbortForCancellation(), this))
            {
                try
                {
                    await operation().ConfigureAwait(false);
                }
                catch (Exception ex) when (timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested && (ex is SocketException || ex is ObjectDisposedException))
                {
                    throw new TimeoutException($"{operationName} operation to {RemoteEndpoint} timed out after {timeoutMilliseconds} ms.", ex);
                }
                catch (Exception ex) when (cancellationToken.IsCancellationRequested && (ex is SocketException || ex is ObjectDisposedException))
                {
                    throw new OperationCanceledException($"{operationName} operation to {RemoteEndpoint} was cancelled.", ex, cancellationToken);
                }
            }
        }

        private async Task<T> ExecuteSocketOperationAsync<T>(Func<Task<T>> operation, int timeoutMilliseconds, string operationName, CancellationToken cancellationToken)
        {
            using var timeoutCts = timeoutMilliseconds > 0 ? new CancellationTokenSource(timeoutMilliseconds) : null;
            using var linkedCts = timeoutCts != null
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
                : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            using (linkedCts.Token.Register(static state => ((FastDFSConnection)state!).AbortForCancellation(), this))
            {
                try
                {
                    return await operation().ConfigureAwait(false);
                }
                catch (Exception ex) when (timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested && (ex is SocketException || ex is ObjectDisposedException))
                {
                    throw new TimeoutException($"{operationName} operation to {RemoteEndpoint} timed out after {timeoutMilliseconds} ms.", ex);
                }
                catch (Exception ex) when (cancellationToken.IsCancellationRequested && (ex is SocketException || ex is ObjectDisposedException))
                {
                    throw new OperationCanceledException($"{operationName} operation to {RemoteEndpoint} was cancelled.", ex, cancellationToken);
                }
            }
        }

        private void AbortForCancellation()
        {
            Interlocked.Exchange(ref _poisoned, 1);
            CloseSocket();
        }

        private void ValidateResponseBodyLength(FastDFSHeader header, bool allowLargeBodies = false)
        {
            if (header.BodyLength < 0)
            {
                InvalidateConnection();
                throw new FastDFSProtocolException($"Response body length cannot be negative: {header.BodyLength} bytes.");
            }

            if (!allowLargeBodies && header.BodyLength > int.MaxValue)
            {
                InvalidateConnection();
                _logger.LogError("Response body length too large from {Endpoint}: {BodyLength} bytes", RemoteEndpoint, header.BodyLength);
                throw new FastDFSProtocolException($"Response body length is too large: {header.BodyLength} bytes.");
            }
        }

        private void InvalidateConnection()
        {
            Interlocked.Exchange(ref _poisoned, 1);
            CloseSocket();
        }

        private void CloseSocket()
        {
            var socket = Interlocked.Exchange(ref _socket, null);
            if (socket == null)
                return;

            try
            {
                if (socket.Connected)
                {
                    try
                    {
                        socket.Shutdown(SocketShutdown.Both);
                    }
                    catch
                    {
                        // Ignore shutdown errors
                    }
                }

                socket.Close();
                socket.Dispose();
            }
            catch
            {
                // Suppress exceptions during socket close
            }
        }
 
        /// <summary>
        /// Closes the connection.
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        /// <summary>
        /// Disposes the connection and releases resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            CloseSocket();

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Asynchronously disposes the connection and releases resources.
        /// Note: Socket disposal is inherently synchronous on netstandard2.0, so this method
        /// returns a completed ValueTask rather than performing actual async I/O.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            if (_disposed)
                return default;

            _disposed = true;
            CloseSocket();
            GC.SuppressFinalize(this);
            return default;
        }

        /// <summary>
        /// Returns a string representation of the connection.
        /// </summary>
        public override string ToString()
        {
            return $"FastDFSConnection [{RemoteEndpoint}, Alive={IsAlive}, Created={CreatedTime:yyyy-MM-dd HH:mm:ss}, LastUsed={LastUsedTime:yyyy-MM-dd HH:mm:ss}]";
        }
    }
}
