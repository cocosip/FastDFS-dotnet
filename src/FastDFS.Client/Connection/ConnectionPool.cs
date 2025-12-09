using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FastDFS.Client.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FastDFS.Client.Connection
{
    /// <summary>
    /// Connection pool for managing FastDFS connections to a specific server.
    /// </summary>
    public class ConnectionPool : IConnectionPool
    {
        private readonly string _host;
        private readonly int _port;
        private readonly ConnectionPoolConfiguration _options;
        private readonly ConcurrentQueue<FastDFSConnection> _idleConnections;
        private readonly SemaphoreSlim _connectionSemaphore;
        private readonly Timer _cleanupTimer;
        private readonly ILogger _logger;
        private int _totalConnections;
        private int _activeConnections;
        private bool _disposed;

        /// <summary>
        /// Gets the total number of connections (both idle and active) in the pool.
        /// </summary>
        public int TotalConnections => _totalConnections;

        /// <summary>
        /// Gets the number of idle connections available in the pool.
        /// </summary>
        public int IdleConnections => _idleConnections.Count;

        /// <summary>
        /// Gets the number of active connections currently in use.
        /// </summary>
        public int ActiveConnections => _activeConnections;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionPool"/> class.
        /// </summary>
        /// <param name="host">The server host.</param>
        /// <param name="port">The server port.</param>
        /// <param name="options">The connection pool options.</param>
        /// <param name="logger">Optional logger instance.</param>
        public ConnectionPool(string host, int port, ConnectionPoolConfiguration options, ILogger<ConnectionPool>? logger = null)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host cannot be null or empty.", nameof(host));
            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            options.Validate();

            _host = host;
            _port = port;
            _options = options;
            _logger = logger ?? NullLogger<ConnectionPool>.Instance;
            _idleConnections = new ConcurrentQueue<FastDFSConnection>();
            _connectionSemaphore = new SemaphoreSlim(_options.MaxConnectionPerServer, _options.MaxConnectionPerServer);
            _totalConnections = 0;
            _activeConnections = 0;

            _logger.LogInformation("ConnectionPool created for {Host}:{Port} with MaxConnections={MaxConnections}, MinConnections={MinConnections}",
                _host, _port, _options.MaxConnectionPerServer, _options.MinConnectionPerServer);

            // Start cleanup timer (run every 30 seconds)
            _cleanupTimer = new Timer(
                CleanupIdleConnections,
                null,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// Gets a connection from the pool asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A FastDFS connection.</returns>
        public async Task<FastDFSConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConnectionPool));

            _logger.LogDebug("Requesting connection from pool {Host}:{Port} (Total={Total}, Idle={Idle}, Active={Active})",
                _host, _port, _totalConnections, _idleConnections.Count, _activeConnections);

            // Try to get an idle connection first
            while (_idleConnections.TryDequeue(out var connection))
            {
                // Validate the connection
                if (IsConnectionValid(connection))
                {
                    Interlocked.Increment(ref _activeConnections);
                    _logger.LogDebug("Reused idle connection from pool {Host}:{Port}", _host, _port);
                    return connection;
                }
                else
                {
                    // Connection is invalid, dispose it
                    _logger.LogDebug("Disposing invalid connection from pool {Host}:{Port}", _host, _port);
                    DisposeConnection(connection);
                }
            }

            // No idle connection available, try to create a new one
            // Wait for a slot in the connection pool
            bool acquired = await _connectionSemaphore.WaitAsync(_options.ConnectionTimeout, cancellationToken).ConfigureAwait(false);

            if (!acquired)
            {
                _logger.LogWarning("Timeout waiting for connection to {Host}:{Port}. Max connections ({MaxConnections}) reached.",
                    _host, _port, _options.MaxConnectionPerServer);
                throw new TimeoutException($"Timeout waiting for connection to {_host}:{_port}. Max connections reached.");
            }

            try
            {
                // Create a new connection
                _logger.LogDebug("Creating new connection to {Host}:{Port}", _host, _port);
                var newConnection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref _activeConnections);
                _logger.LogInformation("Successfully created connection to {Host}:{Port} (Total={Total}, Active={Active})",
                    _host, _port, _totalConnections, _activeConnections);
                return newConnection;
            }
            catch (Exception ex)
            {
                // Release the semaphore if connection creation failed
                _connectionSemaphore.Release();
                _logger.LogError(ex, "Failed to create connection to {Host}:{Port}", _host, _port);
                throw;
            }
        }

        /// <summary>
        /// Returns a connection to the pool.
        /// </summary>
        /// <param name="connection">The connection to return.</param>
        public void ReturnConnection(FastDFSConnection connection)
        {
            if (_disposed)
            {
                // Pool is disposed, just dispose the connection
                _logger.LogDebug("Pool disposed, disposing returned connection to {Host}:{Port}", _host, _port);
                DisposeConnection(connection);
                return;
            }

            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            Interlocked.Decrement(ref _activeConnections);

            // Validate the connection before returning to pool
            if (IsConnectionValid(connection))
            {
                _idleConnections.Enqueue(connection);
                _logger.LogDebug("Returned connection to pool {Host}:{Port} (Idle={Idle}, Active={Active})",
                    _host, _port, _idleConnections.Count, _activeConnections);
            }
            else
            {
                // Connection is invalid, dispose it
                _logger.LogDebug("Returned connection is invalid, disposing connection to {Host}:{Port}", _host, _port);
                DisposeConnection(connection);
            }
        }

        /// <summary>
        /// Creates a new connection to the server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A new connected FastDFS connection.</returns>
        private async Task<FastDFSConnection> CreateConnectionAsync(CancellationToken cancellationToken)
        {
            var connection = new FastDFSConnection(_host, _port, _options.SendTimeout, _options.ReceiveTimeout);

            try
            {
                await connection.ConnectAsync(cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref _totalConnections);
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Validates if a connection is still usable.
        /// </summary>
        /// <param name="connection">The connection to validate.</param>
        /// <returns>True if the connection is valid; otherwise, false.</returns>
        private bool IsConnectionValid(FastDFSConnection connection)
        {
            if (connection == null)
                return false;

            // Check if connection is alive
            if (!connection.IsAlive)
                return false;

            var now = DateTime.UtcNow;

            // Check connection lifetime
            if (_options.ConnectionLifetime > 0)
            {
                var age = (now - connection.CreatedTime).TotalSeconds;
                if (age > _options.ConnectionLifetime)
                    return false;
            }

            // Check idle timeout
            if (_options.ConnectionIdleTimeout > 0)
            {
                var idleTime = (now - connection.LastUsedTime).TotalSeconds;
                if (idleTime > _options.ConnectionIdleTimeout)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Cleanup timer callback to remove idle and expired connections.
        /// </summary>
        /// <param name="state">Timer state (not used).</param>
        private void CleanupIdleConnections(object? state)
        {
            if (_disposed)
                return;

            try
            {
                int idleCount = _idleConnections.Count;
                int minToKeep = _options.MinConnectionPerServer;
                int removed = 0;

                _logger.LogDebug("Starting connection cleanup for {Host}:{Port} (Idle={Idle}, Total={Total})",
                    _host, _port, idleCount, _totalConnections);

                // Create a temporary list to hold valid connections
                var validConnections = new ConcurrentQueue<FastDFSConnection>();

                // Process all idle connections
                while (_idleConnections.TryDequeue(out var connection))
                {
                    bool shouldKeep = false;

                    // Always keep minimum number of connections if they're valid
                    if (validConnections.Count < minToKeep && connection.IsAlive)
                    {
                        shouldKeep = true;
                    }
                    // For connections beyond minimum, check if they're still valid
                    else if (IsConnectionValid(connection))
                    {
                        shouldKeep = true;
                    }

                    if (shouldKeep)
                    {
                        validConnections.Enqueue(connection);
                    }
                    else
                    {
                        // Connection is invalid or excessive, dispose it
                        DisposeConnection(connection);
                        removed++;
                    }
                }

                // Put valid connections back to the pool
                while (validConnections.TryDequeue(out var connection))
                {
                    _idleConnections.Enqueue(connection);
                }

                if (removed > 0)
                {
                    _logger.LogInformation("Cleanup removed {RemovedCount} expired connections from {Host}:{Port} (Remaining={Remaining})",
                        removed, _host, _port, _totalConnections);
                }

                // If we have fewer than minimum connections, create more
                int currentTotal = _totalConnections;
                if (currentTotal < minToKeep)
                {
                    int toCreate = minToKeep - currentTotal;
                    _logger.LogInformation("Prewarming connection pool {Host}:{Port}, creating {Count} connections to reach minimum",
                        _host, _port, toCreate);

                    _ = Task.Run(async () =>
                    {
                        for (int i = 0; i < toCreate; i++)
                        {
                            try
                            {
                                bool acquired = await _connectionSemaphore.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                                if (acquired)
                                {
                                    try
                                    {
                                        var connection = await CreateConnectionAsync(CancellationToken.None).ConfigureAwait(false);
                                        _idleConnections.Enqueue(connection);
                                        _logger.LogDebug("Prewarmed connection {Index}/{Total} to {Host}:{Port}",
                                            i + 1, toCreate, _host, _port);
                                    }
                                    catch (Exception ex)
                                    {
                                        _connectionSemaphore.Release();
                                        _logger.LogWarning(ex, "Failed to prewarm connection {Index}/{Total} to {Host}:{Port}",
                                            i + 1, toCreate, _host, _port);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to acquire semaphore for prewarming connection to {Host}:{Port}",
                                    _host, _port);
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during connection cleanup for {Host}:{Port}", _host, _port);
            }
        }

        /// <summary>
        /// Disposes a connection and updates the pool counters.
        /// </summary>
        /// <param name="connection">The connection to dispose.</param>
        private void DisposeConnection(FastDFSConnection connection)
        {
            if (connection == null)
                return;

            try
            {
                connection.Dispose();
                Interlocked.Decrement(ref _totalConnections);
                _connectionSemaphore.Release();
            }
            catch
            {
                // Suppress exceptions during disposal
            }
        }

        /// <summary>
        /// Disposes the connection pool and all connections.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _logger.LogInformation("Disposing ConnectionPool for {Host}:{Port} (Total={Total}, Idle={Idle}, Active={Active})",
                _host, _port, _totalConnections, _idleConnections.Count, _activeConnections);

            // Stop the cleanup timer
            _cleanupTimer?.Dispose();

            // Dispose all idle connections
            int disposedCount = 0;
            while (_idleConnections.TryDequeue(out var connection))
            {
                try
                {
                    connection.Dispose();
                    disposedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing connection to {Host}:{Port}", _host, _port);
                }
            }

            _logger.LogInformation("Disposed {Count} idle connections to {Host}:{Port}", disposedCount, _host, _port);

            // Dispose the semaphore
            _connectionSemaphore?.Dispose();

            _totalConnections = 0;
            _activeConnections = 0;

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Returns a string representation of the connection pool.
        /// </summary>
        public override string ToString()
        {
            return $"ConnectionPool [{_host}:{_port}, Total={TotalConnections}, Idle={IdleConnections}, Active={ActiveConnections}]";
        }
    }
}
