using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FastDFS.Client.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FastDFS.Client.Connection
{
    /// <summary>
    /// Default implementation of <see cref="IConnectionPoolProvider"/>.
    /// Manages connection pools for multiple FastDFS endpoints.
    /// </summary>
    public class ConnectionPoolProvider : IConnectionPoolProvider
    {
        private readonly ConnectionPoolConfiguration _poolOptions;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<ConnectionEndpoint, IConnectionPool> _pools;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionPoolProvider"/> class.
        /// </summary>
        /// <param name="poolOptions">The connection pool configuration.</param>
        /// <param name="loggerFactory">Optional logger factory.</param>
        public ConnectionPoolProvider(ConnectionPoolConfiguration poolOptions, ILoggerFactory? loggerFactory = null)
        {
            _poolOptions = poolOptions ?? throw new ArgumentNullException(nameof(poolOptions));
            _poolOptions.Validate();

            _loggerFactory = loggerFactory;
            _logger = loggerFactory?.CreateLogger<ConnectionPoolProvider>() ?? NullLogger<ConnectionPoolProvider>.Instance;
            _pools = new ConcurrentDictionary<ConnectionEndpoint, IConnectionPool>();
        }

        /// <inheritdoc/>
        public IConnectionPool GetOrCreate(ConnectionEndpoint endpoint)
        {
            ThrowIfDisposed();

            return _pools.GetOrAdd(endpoint, CreatePool);
        }

        /// <inheritdoc/>
        public bool TryGet(ConnectionEndpoint endpoint, out IConnectionPool? pool)
        {
            ThrowIfDisposed();
            return _pools.TryGetValue(endpoint, out pool);
        }

        /// <inheritdoc/>
        public IReadOnlyCollection<ConnectionEndpoint> GetEndpoints()
        {
            ThrowIfDisposed();
            return _pools.Keys.ToList();
        }

        private IConnectionPool CreatePool(ConnectionEndpoint endpoint)
        {
            _logger.LogInformation("Creating connection pool for {Endpoint}", endpoint.Key);
            var poolLogger = _loggerFactory?.CreateLogger<ConnectionPool>();
            return new ConnectionPool(endpoint.Host, endpoint.Port, _poolOptions, poolLogger);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConnectionPoolProvider));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            foreach (var pool in _pools.Values)
            {
                try { pool.Dispose(); }
                catch (Exception ex) { _logger.LogWarning(ex, "Error disposing connection pool"); }
            }

            _pools.Clear();
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;

            foreach (var pool in _pools.Values)
            {
                try
                {
                    if (pool is IAsyncDisposable asyncDisposable)
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    else
                        pool.Dispose();
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Error disposing connection pool asynchronously"); }
            }

            _pools.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
