using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FastDFS.Client.Exceptions;
using FastDFS.Client.Protocol;
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
        private readonly int _sendTimeout;
        private readonly int _receiveTimeout;
        private readonly ILogger _logger;
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
                if (_disposed || _socket == null)
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
            if (_disposed || _socket == null)
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
        /// <param name="sendTimeout">The send timeout in milliseconds (0 = infinite).</param>
        /// <param name="receiveTimeout">The receive timeout in milliseconds (0 = infinite).</param>
        /// <param name="logger">Optional logger instance.</param>
        public FastDFSConnection(string host, int port, int sendTimeout = 30000, int receiveTimeout = 30000, ILogger<FastDFSConnection>? logger = null)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host cannot be null or empty.", nameof(host));
            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");

            _host = host;
            _port = port;
            _sendTimeout = sendTimeout;
            _receiveTimeout = receiveTimeout;
            _logger = logger ?? NullLogger<FastDFSConnection>.Instance;
            RemoteEndpoint = $"{host}:{port}";
            CreatedTime = DateTime.UtcNow;
            LastUsedTime = CreatedTime;
        }

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

                // Resolve DNS asynchronously
                var addresses = await Dns.GetHostAddressesAsync(_host).ConfigureAwait(false);
                if (addresses == null || addresses.Length == 0)
                    throw new SocketException((int)SocketError.HostNotFound);

                cancellationToken.ThrowIfCancellationRequested();

                // Pick a random address to distribute load across multi-IP DNS entries
                var address = addresses.Length == 1
                    ? addresses[0]
                    : addresses[System.Threading.Interlocked.Increment(ref _dnsRoundRobinIndex) % addresses.Length];
                _socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                {
                    // Configure socket options for better performance
                    NoDelay = true, // Disable Nagle's algorithm for low latency
                    SendBufferSize = 64 * 1024, // 64KB send buffer
                    ReceiveBufferSize = 64 * 1024 // 64KB receive buffer
                };

                // Set timeouts
                if (_sendTimeout > 0)
                    _socket.SendTimeout = _sendTimeout;
                if (_receiveTimeout > 0)
                    _socket.ReceiveTimeout = _receiveTimeout;

                _logger.LogDebug("Connecting to {Endpoint} (IP: {IP})...", RemoteEndpoint, address);

                // Connect asynchronously; register cancellation to close the socket
                var endpoint = new IPEndPoint(address, _port);
                using (cancellationToken.Register(() => _socket?.Dispose()))
                {
                    await _socket.ConnectAsync(endpoint).ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();

                LastUsedTime = DateTime.UtcNow;
                _logger.LogInformation("Successfully connected to {Endpoint}", RemoteEndpoint);
            }
            catch (OperationCanceledException)
            {
                _socket?.Dispose();
                _socket = null;
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to {Endpoint}", RemoteEndpoint);

                // Clean up socket on failure
                _socket?.Dispose();
                _socket = null;

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

                // Send all bytes using Socket.SendAsync
                // netstandard2.0: register cancellation via socket disposal
                using (cancellationToken.Register(() => _socket?.Dispose()))
                {
                    int totalSent = 0;
                    while (totalSent < packetBytes.Length)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var segment = new ArraySegment<byte>(packetBytes, totalSent, packetBytes.Length - totalSent);
                        int sent = await _socket.SendAsync(segment, SocketFlags.None).ConfigureAwait(false);

                        if (sent == 0)
                            throw new SocketException((int)SocketError.ConnectionReset);

                        totalSent += sent;
                    }
                }

                _logger.LogTrace("Successfully sent {ByteCount} bytes to {Endpoint}", packetBytes.Length, RemoteEndpoint);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SocketException ex)
            {
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
                if (header.BodyLength > 0)
                {
                    // Validate body length to prevent excessive memory allocation
                    if (header.BodyLength > int.MaxValue)
                    {
                        _logger.LogError("Response body length too large from {Endpoint}: {BodyLength} bytes", RemoteEndpoint, header.BodyLength);
                        throw new FastDFSProtocolException($"Response body length is too large: {header.BodyLength} bytes.");
                    }

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
            catch (FastDFSException)
            {
                // Re-throw FastDFS-specific exceptions
                throw;
            }
            catch (Exception ex)
            {
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

            // netstandard2.0: register cancellation via socket disposal
            using (cancellationToken.Register(() => _socket?.Dispose()))
            {
                int totalRead = 0;
                while (totalRead < count)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var segment = new ArraySegment<byte>(buffer, offset + totalRead, count - totalRead);
                    int bytesRead = await _socket.ReceiveAsync(segment, SocketFlags.None).ConfigureAwait(false);

                    if (bytesRead == 0)
                    {
                        throw new EndOfStreamException(
                            $"Connection closed unexpectedly. Expected {count} bytes but received {totalRead} bytes.");
                    }

                    totalRead += bytesRead;
                }
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

            try
            {
                // Gracefully shutdown and close the socket
                if (_socket != null)
                {
                    if (_socket.Connected)
                    {
                        try
                        {
                            _socket.Shutdown(SocketShutdown.Both);
                        }
                        catch
                        {
                            // Ignore shutdown errors
                        }
                    }
                    _socket.Close();
                    _socket.Dispose();
                }
            }
            catch
            {
                // Suppress exceptions during disposal
            }

            GC.SuppressFinalize(this);
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
