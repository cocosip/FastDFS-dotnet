using System;
using System.Buffers;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FastDFS.Client.Exceptions;
using FastDFS.Client.Protocol;

namespace FastDFS.Client.Connection
{
    /// <summary>
    /// Represents a TCP connection to a FastDFS server (Tracker or Storage).
    /// </summary>
    public class FastDFSConnection : IDisposable
    {
        private readonly TcpClient _tcpClient;
        private NetworkStream? _stream;
        private readonly string _host;
        private readonly int _port;
        private readonly int _sendTimeout;
        private readonly int _receiveTimeout;
        private bool _disposed;

        /// <summary>
        /// Gets the remote endpoint in the format "host:port".
        /// </summary>
        public string RemoteEndpoint => $"{_host}:{_port}";

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
        /// </summary>
        public bool IsAlive
        {
            get
            {
                if (_disposed || _tcpClient == null)
                    return false;

                try
                {
                    // Check if the socket is connected and available
                    if (!_tcpClient.Connected)
                        return false;

                    // Additional check: try to peek at the data
                    var socket = _tcpClient.Client;
                    if (socket == null || !socket.Connected)
                        return false;

                    // Poll for read with zero timeout to detect if the connection is closed
                    bool poll = socket.Poll(1000, SelectMode.SelectRead);
                    bool available = socket.Available == 0;

                    // If Poll returns true and there's no data available, the connection is likely closed
                    if (poll && available)
                        return false;

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSConnection"/> class.
        /// </summary>
        /// <param name="host">The server host.</param>
        /// <param name="port">The server port.</param>
        /// <param name="sendTimeout">The send timeout in milliseconds (0 = infinite).</param>
        /// <param name="receiveTimeout">The receive timeout in milliseconds (0 = infinite).</param>
        public FastDFSConnection(string host, int port, int sendTimeout = 30000, int receiveTimeout = 30000)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host cannot be null or empty.", nameof(host));
            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");

            _host = host;
            _port = port;
            _sendTimeout = sendTimeout;
            _receiveTimeout = receiveTimeout;
            _tcpClient = new TcpClient();
            CreatedTime = DateTime.UtcNow;
            LastUsedTime = CreatedTime;
        }

        /// <summary>
        /// Connects to the FastDFS server asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FastDFSConnection));

            try
            {
                await _tcpClient.ConnectAsync(_host, _port).ConfigureAwait(false);
                _stream = _tcpClient.GetStream();

                // Set timeouts
                if (_sendTimeout > 0)
                    _stream.WriteTimeout = _sendTimeout;
                if (_receiveTimeout > 0)
                    _stream.ReadTimeout = _receiveTimeout;

                LastUsedTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
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
            if (_stream == null)
                throw new InvalidOperationException("Connection is not established. Call ConnectAsync first.");
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                // Send the request
                await SendAsync(request, cancellationToken).ConfigureAwait(false);

                // Receive the response
                var response = await ReceiveAsync<TResponse>(cancellationToken).ConfigureAwait(false);

                LastUsedTime = DateTime.UtcNow;

                return response;
            }
            catch (FastDFSException)
            {
                // Re-throw FastDFS-specific exceptions
                throw;
            }
            catch (Exception ex)
            {
                throw new FastDFSNetworkException(
                    $"Error communicating with {RemoteEndpoint}.",
                    RemoteEndpoint,
                    ex);
            }
        }

        /// <summary>
        /// Sends a request packet to the server asynchronously.
        /// </summary>
        /// <param name="request">The request to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task SendAsync(IFastDFSRequest request, CancellationToken cancellationToken)
        {
            if (_stream == null)
                throw new InvalidOperationException("Network stream is not available.");

            byte[] packetBytes = request.Encode();

            try
            {
                await _stream.WriteAsync(packetBytes, 0, packetBytes.Length, cancellationToken).ConfigureAwait(false);
                await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                throw new FastDFSNetworkException(
                    $"Failed to send data to {RemoteEndpoint}.",
                    RemoteEndpoint,
                    ex);
            }
        }

        /// <summary>
        /// Receives a response packet from the server asynchronously.
        /// </summary>
        /// <typeparam name="TResponse">The response type.</typeparam>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The parsed response.</returns>
        private async Task<TResponse> ReceiveAsync<TResponse>(CancellationToken cancellationToken)
            where TResponse : IFastDFSResponse, new()
        {
            if (_stream == null)
                throw new InvalidOperationException("Network stream is not available.");

            // Read the header (10 bytes)
            byte[] headerBuffer = ArrayPool<byte>.Shared.Rent(FastDFSHeader.HeaderSize);
            try
            {
                await ReadExactlyAsync(_stream, headerBuffer, 0, FastDFSHeader.HeaderSize, cancellationToken).ConfigureAwait(false);

                // Parse the header
                var header = FastDFSHeader.Parse(headerBuffer, 0);

                // Read the body if present
                byte[]? bodyBuffer = null;
                if (header.BodyLength > 0)
                {
                    // Validate body length to prevent excessive memory allocation
                    if (header.BodyLength > int.MaxValue)
                        throw new FastDFSProtocolException($"Response body length is too large: {header.BodyLength} bytes.");

                    int bodyLength = (int)header.BodyLength;
                    bodyBuffer = ArrayPool<byte>.Shared.Rent(bodyLength);

                    try
                    {
                        await ReadExactlyAsync(_stream, bodyBuffer, 0, bodyLength, cancellationToken).ConfigureAwait(false);

                        // Create the response and decode it
                        var response = new TResponse();

                        // Copy the body data to a properly sized array before decoding
                        byte[] actualBody = new byte[bodyLength];
                        Array.Copy(bodyBuffer, 0, actualBody, 0, bodyLength);

                        response.Decode(header, actualBody);

                        // Check if the response indicates an error
                        if (!response.IsSuccess)
                        {
                            throw new FastDFSProtocolException(
                                $"FastDFS server returned error. Status code: {header.Status}",
                                header.Status);
                        }

                        return response;
                    }
                    finally
                    {
                        if (bodyBuffer != null)
                            ArrayPool<byte>.Shared.Return(bodyBuffer);
                    }
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
        /// Reads exactly the specified number of bytes from the stream.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="buffer">The buffer to read into.</param>
        /// <param name="offset">The offset in the buffer to start writing.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private static async Task ReadExactlyAsync(
            Stream stream,
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int bytesRead = await stream.ReadAsync(
                    buffer,
                    offset + totalRead,
                    count - totalRead,
                    cancellationToken).ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    throw new EndOfStreamException(
                        $"Connection closed unexpectedly. Expected {count} bytes but received {totalRead} bytes.");
                }

                totalRead += bytesRead;
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
                _stream?.Close();
                _stream?.Dispose();
                _tcpClient?.Close();
                _tcpClient?.Dispose();
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
