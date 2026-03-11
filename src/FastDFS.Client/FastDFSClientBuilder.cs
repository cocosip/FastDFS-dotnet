using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FastDFS.Client.Configuration;
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

            configuration.Validate();

            var trackerEndpoints = configuration.TrackerServers.ToList();
            var trackerClient = new TrackerClient(trackerEndpoints, configuration.ConnectionPool);

            return new FastDFSClient(
                trackerClient,
                configuration.ConnectionPool,
                name,
                configuration.DefaultGroupName,
                configuration.StorageSelectionStrategy,
                configuration.HttpConfig);
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
        private readonly Dictionary<string, FastDFSConfiguration> _options = new();

        // name -> client instance (multiple names may point to the same physical instance)
        private readonly Dictionary<string, IFastDFSClient> _clients = new();

        // Shared physical clients keyed by config fingerprint (all protected by _lock)
        private readonly Dictionary<string, IFastDFSClient> _sharedClients = new();
        private readonly Dictionary<string, int> _sharedRefCounts = new();
        private readonly Dictionary<string, string> _nameToConfigKey = new();

        private readonly object _lock = new object();
        private bool _disposed;

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

            lock (_lock)
            {
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
            var configKey = ComputeConfigKey(configuration);

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
            if (!_clients.ContainsKey(name))
                return false;

            _clients.Remove(name);
            _options.Remove(name);

            if (_nameToConfigKey.TryGetValue(name, out var configKey))
            {
                _nameToConfigKey.Remove(name);

                var refCount = --_sharedRefCounts[configKey];
                if (refCount <= 0)
                {
                    _sharedRefCounts.Remove(configKey);

                    if (_sharedClients.TryGetValue(configKey, out var client))
                    {
                        _sharedClients.Remove(configKey);
                        try { (client as IDisposable)?.Dispose(); }
                        catch { }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Computes a fingerprint string that uniquely identifies a configuration.
        /// Two configurations that produce the same key will share a physical client.
        /// </summary>
        private static string ComputeConfigKey(FastDFSConfiguration config)
        {
            var trackers = string.Join(",", config.TrackerServers
                .Select(s => s.Trim().ToLowerInvariant())
                .OrderBy(s => s));

            var pool = config.ConnectionPool;
            var sb = new StringBuilder()
                .Append(trackers).Append('|')
                .Append(pool.MaxConnectionPerServer).Append(',')
                .Append(pool.MinConnectionPerServer).Append(',')
                .Append(pool.ConnectionTimeout).Append(',')
                .Append(pool.SendTimeout).Append(',')
                .Append(pool.ReceiveTimeout).Append(',')
                .Append(pool.ConnectionIdleTimeout).Append(',')
                .Append(pool.ConnectionLifetime).Append('|')
                .Append(config.NetworkTimeout).Append('|')
                .Append(config.Charset ?? string.Empty).Append('|')
                .Append(config.DefaultGroupName ?? string.Empty).Append('|')
                .Append((int)config.StorageSelectionStrategy);

            if (config.HttpConfig != null)
            {
                var urls = string.Join(",", config.HttpConfig.ServerUrls
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp => $"{kvp.Key}={kvp.Value}"));
                sb.Append('|')
                  .Append(urls).Append('|')
                  .Append(config.HttpConfig.SecretKey ?? string.Empty).Append('|')
                  .Append(config.HttpConfig.AntiStealTokenEnabled).Append('|')
                  .Append(config.HttpConfig.DefaultTokenExpireSeconds).Append('|')
                  .Append(config.HttpConfig.DefaultServerUrlTemplate ?? string.Empty);
            }

            return sb.ToString();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FastDFSClientManager));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

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
