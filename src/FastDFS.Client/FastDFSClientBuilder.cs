using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FastDFS.Client.Configuration;
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
    /// Clients with identical configurations share the same underlying connection pool.
    /// </summary>
    public class FastDFSClientManager : IFastDFSClientFactory, IDisposable
    {
        // name -> config (for lazy creation via AddClient)
        private readonly ConcurrentDictionary<string, FastDFSConfiguration> _options = new();

        // name -> client instance (multiple names may point to the same physical instance)
        private readonly ConcurrentDictionary<string, IFastDFSClient> _clients = new();

        // Shared physical clients keyed by config fingerprint
        private readonly ConcurrentDictionary<string, IFastDFSClient> _sharedClients = new();
        private readonly ConcurrentDictionary<string, int> _sharedRefCounts = new();
        private readonly ConcurrentDictionary<string, string> _nameToConfigKey = new();

        private readonly object _lock = new object();
        private int _disposed; // 0 = false, 1 = true; use Interlocked for thread-safe dispose

        private const string DefaultClientName = "default";

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSClientManager"/> class.
        /// </summary>
        public FastDFSClientManager() { }

        /// <summary>
        /// Adds a named client configuration. The client will be created lazily on first access.
        /// </summary>
        public void AddClient(string name, FastDFSConfiguration options)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Client name cannot be null or empty.", nameof(name));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            ThrowIfDisposed();

            lock (_lock)
            {
                options.Validate();
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

            // Fast path: try to get existing client without lock
            if (_clients.TryGetValue(name, out var existingClient))
                return existingClient;

            // Slow path: create new client with lock
            lock (_lock)
            {
                // Double-check after acquiring lock
                if (_clients.TryGetValue(name, out existingClient))
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
                return _options.Keys.ToList();
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
                return _options.ContainsKey(name);
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
                if (_clients.ContainsKey(name))
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
                return RemoveClientInternal(name);
            }
        }

        /// <summary>
        /// Returns an existing shared physical client if one with the same configuration exists,
        /// otherwise creates a new one. Must be called within <see cref="_lock"/>.
        /// </summary>
        private IFastDFSClient GetOrCreateSharedClient(string name, FastDFSConfiguration configuration)
        {
            var configKey = configuration.GetConfigKey();

            if (_sharedClients.TryGetValue(configKey, out var existing))
            {
                _nameToConfigKey[name] = configKey;
                _sharedRefCounts[configKey]++;
                return existing;
            }

            var client = FastDFSClientBuilder.CreateClient(configuration, name);
            _sharedClients[configKey] = client;
            _sharedRefCounts[configKey] = 1;
            _nameToConfigKey[name] = configKey;
            return client;
        }

        /// <summary>
        /// Removes a client by name and decrements the shared ref count,
        /// disposing the physical client only when the last reference is removed.
        /// Must be called within <see cref="_lock"/>.
        /// </summary>
        private bool RemoveClientInternal(string name)
        {
            if (!_clients.TryRemove(name, out _))
                return false;

            _options.TryRemove(name, out _);

            if (_nameToConfigKey.TryRemove(name, out var configKey))
            {
                if (_sharedRefCounts.TryRemove(configKey, out var refCount) && refCount <= 1)
                {
                    if (_sharedClients.TryRemove(configKey, out var client))
                    {
                        try { (client as IDisposable)?.Dispose(); }
                        catch { }
                    }
                }
                else if (refCount > 1)
                {
                    // Decrement ref count
                    _sharedRefCounts[configKey] = refCount - 1;
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
                foreach (var client in _sharedClients.Values)
                {
                    try { (client as IDisposable)?.Dispose(); }
                    catch { }
                }

                _sharedClients.Clear();
                _sharedRefCounts.Clear();
                _nameToConfigKey.Clear();
                _clients.Clear();
                _options.Clear();
            }

            GC.SuppressFinalize(this);
        }
    }
}
