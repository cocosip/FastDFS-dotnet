using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FastDFS.Client.Configuration;
using FastDFS.Client.Exceptions;

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
        public ConnectionPool(string host, int port, ConnectionPoolConfiguration options)
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
            _idleConnections = new ConcurrentQueue<FastDFSConnection>();
            _connectionSemaphore = new SemaphoreSlim(_options.MaxConnectionPerServer, _options.MaxConnectionPerServer);
            _totalConnections = 0;
            _activeConnections = 0;

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

            // Try to get an idle connection first
            while (_idleConnections.TryDequeue(out var connection))
            {
                // Validate the connection
                if (IsConnectionValid(connection))
                {
                    Interlocked.Increment(ref _activeConnections);
                    return connection;
                }
                else
                {
                    // Connection is invalid, dispose it
                    DisposeConnection(connection);
                }
            }

            // No idle connection available, try to create a new one
            // Wait for a slot in the connection pool
            bool acquired = await _connectionSemaphore.WaitAsync(_options.ConnectionTimeout, cancellationToken).ConfigureAwait(false);

            if (!acquired)
            {
                throw new TimeoutException($"Timeout waiting for connection to {_host}:{_port}. Max connections reached.");
            }

            try
            {
                // Create a new connection
                var newConnection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref _activeConnections);
                return newConnection;
            }
            catch
            {
                // Release the semaphore if connection creation failed
                _connectionSemaphore.Release();
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
            }
            else
            {
                // Connection is invalid, dispose it
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

                // If we have fewer than minimum connections, create more
                int currentTotal = _totalConnections;
                if (currentTotal < minToKeep)
                {
                    int toCreate = minToKeep - currentTotal;
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
                                    }
                                    catch
                                    {
                                        _connectionSemaphore.Release();
                                        // Ignore errors during prewarming
                                    }
                                }
                            }
                            catch
                            {
                                // Ignore errors during prewarming
                            }
                        }
                    });
                }
            }
            catch
            {
                // Suppress exceptions in cleanup timer
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

            // Stop the cleanup timer
            _cleanupTimer?.Dispose();

            // Dispose all idle connections
            while (_idleConnections.TryDequeue(out var connection))
            {
                try
                {
                    connection.Dispose();
                }
                catch
                {
                    // Suppress exceptions during disposal
                }
            }

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
