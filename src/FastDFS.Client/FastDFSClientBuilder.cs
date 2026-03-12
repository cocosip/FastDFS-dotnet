using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FastDFS.Client.Configuration;
using FastDFS.Client.Connection;
using FastDFS.Client.Storage;
using FastDFS.Client.Tracker;

namespace FastDFS.Client
{
    /// <summary>
    /// Builder for creating FastDFS client instances in non-DI scenarios.
    /// Provides a simple factory pattern for creating clients without dependency injection.
    /// </summary>
    public static class FastDFSClientBuilder
    {
        /// <summary>
        /// Creates a FastDFS client with the specified options.
        /// </summary>
        public static IFastDFSClient CreateClient(FastDFSConfiguration configuration, string name = "default")
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            return FastDFSClientComposer.Compose(configuration, name);
        }

        /// <summary>
        /// Creates a FastDFS client with manual configuration.
        /// </summary>
        public static IFastDFSClient CreateClient(
            IEnumerable<string> trackerServers,
            Action<ConnectionPoolConfiguration>? configureConnectionPool = null,
            string name = "default")
        {
            if (trackerServers == null)
                throw new ArgumentNullException(nameof(trackerServers));

            var serverList = trackerServers.ToList();
            if (serverList.Count == 0)
                throw new ArgumentException("At least one tracker server must be specified.", nameof(trackerServers));

            var options = new FastDFSConfiguration
            {
                TrackerServers = serverList
            };

            configureConnectionPool?.Invoke(options.ConnectionPool);

            return CreateClient(options, name);
        }

        /// <summary>
        /// Creates a FastDFS client with a single tracker server.
        /// </summary>
        public static IFastDFSClient CreateClient(string trackerServer, string name = "default")
        {
            if (string.IsNullOrWhiteSpace(trackerServer))
                throw new ArgumentException("Tracker server cannot be null or empty.", nameof(trackerServer));

            return CreateClient(new[] { trackerServer }, null, name);
        }
    }

    /// <summary>
    /// Manager for multiple FastDFS client instances in non-DI scenarios.
    /// Clients with identical configurations share the same underlying connection pool resources.
    /// </summary>
    public class FastDFSClientManager : IFastDFSClientFactory, IDisposable
    {
        // name -> config (for lazy creation via AddClient)
        private readonly ConcurrentDictionary<string, FastDFSConfiguration> _options = new();

        // name -> logical client instance
        private readonly ConcurrentDictionary<string, IFastDFSClient> _clients = new();

        // Shared connection pool resources keyed by config fingerprint (protected by _lock)
        private readonly Dictionary<string, SharedClientResources> _sharedResources = new();
        private readonly Dictionary<string, int> _sharedRefCounts = new();
        private readonly Dictionary<string, string> _nameToConfigKey = new();

        private readonly object _lock = new object();
        private int _disposed; // 0 = false, 1 = true; use Interlocked for thread-safe dispose

        private const string DefaultClientName = "default";

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSClientManager"/> class.
        /// </summary>
        public FastDFSClientManager() { }

        /// <summary>
        /// Adds a named client configuration. The client will be created lazily on first access.
        /// If the client already exists, it will be replaced.
        /// </summary>
        public void AddClient(string name, FastDFSConfiguration options)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Client name cannot be null or empty.", nameof(name));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            ThrowIfDisposed();
            options.Validate();

            lock (_lock)
            {
                ThrowIfDisposed();

                if (_options.ContainsKey(name) || _clients.ContainsKey(name))
                    RemoveClientInternal(name);

                _options[name] = options;
            }
        }

        /// <summary>
        /// Adds a named client configuration with manual settings.
        /// </summary>
        public void AddClient(
            string name,
            IEnumerable<string> trackerServers,
            Action<ConnectionPoolConfiguration>? poolConfigurer = null)
        {
            if (trackerServers == null)
                throw new ArgumentNullException(nameof(trackerServers));

            var options = new FastDFSConfiguration
            {
                TrackerServers = trackerServers.ToList()
            };

            poolConfigurer?.Invoke(options.ConnectionPool);

            AddClient(name, options);
        }

        /// <inheritdoc/>
        public IFastDFSClient GetClient() => GetClient(DefaultClientName);

        /// <inheritdoc/>
        public IFastDFSClient GetClient(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Client name cannot be null or empty.", nameof(name));

            ThrowIfDisposed();

            lock (_lock)
            {
                ThrowIfDisposed();

                if (_clients.TryGetValue(name, out var existingClient))
                    return existingClient;

                if (!_options.TryGetValue(name, out var options))
                    throw new InvalidOperationException(
                        $"No configuration found for client '{name}'. Please call AddClient(\"{name}\", ...) first.");

                var client = GetOrCreateSharedClient(name, options);
                _clients[name] = client;
                return client;
            }
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetClientNames()
        {
            ThrowIfDisposed();

            lock (_lock)
            {
                ThrowIfDisposed();

                return _options.Keys
                    .Concat(_clients.Keys)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }
        }

        /// <inheritdoc/>
        public bool HasClient(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            ThrowIfDisposed();

            lock (_lock)
            {
                ThrowIfDisposed();
                return _options.ContainsKey(name) || _clients.ContainsKey(name);
            }
        }

        /// <inheritdoc/>
        public IFastDFSClient RegisterClient(string name, FastDFSConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Client name cannot be null or empty.", nameof(name));
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            ThrowIfDisposed();
            configuration.Validate();

            lock (_lock)
            {
                ThrowIfDisposed();

                if (_options.ContainsKey(name) || _clients.ContainsKey(name))
                    RemoveClientInternal(name);

                _options[name] = configuration;

                var client = GetOrCreateSharedClient(name, configuration);
                _clients[name] = client;
                return client;
            }
        }

        /// <inheritdoc/>
        public bool RemoveClient(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            ThrowIfDisposed();

            lock (_lock)
            {
                ThrowIfDisposed();
                return RemoveClientInternal(name);
            }
        }

        /// <summary>
        /// Creates a logical named client backed by shared connection pool resources when the
        /// configuration fingerprint matches an existing registration. Must be called within <see cref="_lock"/>.
        /// </summary>
        private IFastDFSClient GetOrCreateSharedClient(string name, FastDFSConfiguration configuration)
        {
            var configKey = configuration.GetConfigKey();

            if (!_sharedResources.TryGetValue(configKey, out var sharedResources))
            {
                sharedResources = new SharedClientResources(new ConnectionPoolProvider(configuration.ConnectionPool));
                _sharedResources[configKey] = sharedResources;
                _sharedRefCounts[configKey] = 0;
            }

            _nameToConfigKey[name] = configKey;
            _sharedRefCounts[configKey]++;

            var trackerClient = new TrackerClient(configuration.TrackerServers, sharedResources.PoolProvider);
            var storageClient = new StorageClient(sharedResources.PoolProvider, configuration.ConnectionPool.StreamCopyBufferSize);
            return new FastDFSClient(
                trackerClient,
                storageClient,
                name,
                configuration.DefaultGroupName,
                configuration.StorageSelectionStrategy,
                configuration.HttpConfig);
        }

        /// <summary>
        /// Removes a client by name and decrements the shared reference count,
        /// disposing the shared pool resources only when the last reference is removed.
        /// Must be called within <see cref="_lock"/>.
        /// </summary>
        private bool RemoveClientInternal(string name)
        {
            var removedClient = _clients.TryRemove(name, out var client);
            var removedConfiguration = _options.TryRemove(name, out _);

            if (!removedClient && !removedConfiguration)
                return false;

            try
            {
                (client as IDisposable)?.Dispose();
            }
            catch
            {
                // Suppress exceptions during disposal
            }

            if (_nameToConfigKey.TryGetValue(name, out var configKey))
            {
                _nameToConfigKey.Remove(name);

                var refCount = --_sharedRefCounts[configKey];
                if (refCount <= 0)
                {
                    _sharedRefCounts.Remove(configKey);

                    if (_sharedResources.TryGetValue(configKey, out var sharedResources))
                    {
                        _sharedResources.Remove(configKey);
                        try
                        {
                            sharedResources.Dispose();
                        }
                        catch
                        {
                            // Suppress exceptions during disposal
                        }
                    }
                }
            }

            return true;
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) == 1)
                throw new ObjectDisposedException(nameof(FastDFSClientManager));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            lock (_lock)
            {
                foreach (var client in _clients.Values)
                {
                    try { (client as IDisposable)?.Dispose(); }
                    catch { }
                }

                foreach (var sharedResources in _sharedResources.Values)
                {
                    try { sharedResources.Dispose(); }
                    catch { }
                }

                _sharedResources.Clear();
                _sharedRefCounts.Clear();
                _nameToConfigKey.Clear();
                _clients.Clear();
                _options.Clear();
            }

            GC.SuppressFinalize(this);
        }

        private sealed class SharedClientResources : IDisposable
        {
            public SharedClientResources(IConnectionPoolProvider poolProvider)
            {
                PoolProvider = poolProvider ?? throw new ArgumentNullException(nameof(poolProvider));
            }

            public IConnectionPoolProvider PoolProvider { get; }

            public void Dispose()
            {
                PoolProvider.Dispose();
            }
        }
    }
}
