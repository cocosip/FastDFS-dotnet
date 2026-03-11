using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using FastDFS.Client.Configuration;
using FastDFS.Client.Connection;
using FastDFS.Client.Storage;
using FastDFS.Client.Tracker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FastDFS.Client.DependencyInjection
{
    /// <summary>
    /// Default implementation of IFastDFSClientFactory.
    /// Manages multiple named FastDFS client instances for multi-cluster scenarios.
    /// Clients with identical configurations share the same underlying connection pool.
    /// </summary>
    public class FastDFSClientFactory : IFastDFSClientFactory, IDisposable
    {
        private readonly IOptionsMonitor<FastDFSConfiguration> _optionsMonitor;
        private readonly IConnectionPoolProviderFactory _poolProviderFactory;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly ILogger _logger;

        // name -> client instance (multiple names may point to the same physical instance)
        private readonly ConcurrentDictionary<string, IFastDFSClient> _clients;
        private readonly ConcurrentDictionary<string, FastDFSConfiguration> _runtimeConfigurations;

        // Shared physical clients keyed by config fingerprint (protected by _lock)
        private readonly Dictionary<string, IFastDFSClient> _sharedClients = new();
        private readonly Dictionary<string, int> _sharedRefCounts = new();
        private readonly Dictionary<string, string> _nameToConfigKey = new();

        private readonly object _lock = new object();
        private bool _disposed;

        private const string DefaultClientName = "default";

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSClientFactory"/> class.
        /// </summary>
        public FastDFSClientFactory(
            IOptionsMonitor<FastDFSConfiguration> optionsMonitor,
            IConnectionPoolProviderFactory poolProviderFactory,
            ILoggerFactory? loggerFactory = null)
        {
            _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
            _poolProviderFactory = poolProviderFactory ?? throw new ArgumentNullException(nameof(poolProviderFactory));
            _loggerFactory = loggerFactory;
            _logger = loggerFactory?.CreateLogger<FastDFSClientFactory>() ?? NullLogger<FastDFSClientFactory>.Instance;
            _clients = new ConcurrentDictionary<string, IFastDFSClient>();
            _runtimeConfigurations = new ConcurrentDictionary<string, FastDFSConfiguration>();

            _logger.LogInformation("FastDFSClientFactory initialized");
        }

        /// <inheritdoc/>
        public IFastDFSClient GetClient() => GetClient(DefaultClientName);

        /// <inheritdoc/>
        public IFastDFSClient GetClient(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Client name cannot be null or empty.", nameof(name));

            ThrowIfDisposed();

            if (_clients.TryGetValue(name, out var existingClient))
            {
                _logger.LogDebug("Returning existing FastDFS client '{ClientName}'", name);
                return existingClient;
            }

            lock (_lock)
            {
                if (_clients.TryGetValue(name, out existingClient))
                    return existingClient;

                _logger.LogInformation("Creating FastDFS client '{ClientName}'", name);

                FastDFSConfiguration? options = null;
                if (_runtimeConfigurations.TryGetValue(name, out var runtimeConfig))
                {
                    options = runtimeConfig;
                    _logger.LogDebug("Using runtime configuration for client '{ClientName}'", name);
                }
                else
                {
                    options = _optionsMonitor.Get(name);
                }

                if (options == null || options.TrackerServers == null || options.TrackerServers.Count == 0)
                {
                    _logger.LogError("No configuration found for FastDFS client '{ClientName}'", name);
                    throw new InvalidOperationException(
                        $"No configuration found for FastDFS client '{name}'. " +
                        $"Please ensure AddFastDFS(\"{name}\", ...) was called or RegisterClient(\"{name}\", ...) was used.");
                }

                options.Validate();

                var client = GetOrCreateSharedClient(name, options);
                _clients[name] = client;

                return client;
            }
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetClientNames()
        {
            ThrowIfDisposed();
            return _clients.Keys.ToList();
        }

        /// <inheritdoc/>
        public bool HasClient(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            ThrowIfDisposed();
            return _clients.ContainsKey(name);
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
                _logger.LogInformation("Registering FastDFS client '{ClientName}' at runtime", name);

                if (_clients.ContainsKey(name))
                {
                    _logger.LogWarning("Client '{ClientName}' already exists, it will be replaced", name);
                    RemoveClientInternal(name);
                }

                _runtimeConfigurations[name] = configuration;

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
                _logger.LogInformation(
                    "FastDFS client '{Name}' sharing connection pool with {Count} existing client(s) (identical configuration)",
                    name, _sharedRefCounts[configKey] - 1);
                return existing;
            }

            // Create new physical client
            var poolProvider = _poolProviderFactory.Create(configuration.ConnectionPool);
            var trackerClient = new TrackerClient(configuration.TrackerServers, poolProvider, _loggerFactory);
            var storageClient = new StorageClient(poolProvider, _loggerFactory);
            var client = new FastDFSClient(
                trackerClient,
                storageClient,
                name,
                configuration.DefaultGroupName,
                configuration.StorageSelectionStrategy,
                configuration.HttpConfig,
                configuration.ConnectionPool.StreamCopyBufferSize,
                _loggerFactory);

            _sharedClients[configKey] = client;
            _sharedRefCounts[configKey] = 1;
            _nameToConfigKey[name] = configKey;

            _logger.LogInformation(
                "Created new physical FastDFS client for '{Name}' with {TrackerCount} tracker server(s)",
                name, configuration.TrackerServers.Count);

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

            _logger.LogInformation("Removing FastDFS client '{ClientName}'", name);

            if (_nameToConfigKey.TryGetValue(name, out var configKey))
            {
                _nameToConfigKey.Remove(name);

                var refCount = --_sharedRefCounts[configKey];
                if (refCount <= 0)
                {
                    _sharedRefCounts.Remove(configKey);

                    if (_sharedClients.TryGetValue(configKey, out var sharedClient))
                    {
                        _sharedClients.Remove(configKey);
                        try
                        {
                            (sharedClient as IDisposable)?.Dispose();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error disposing shared FastDFS client for '{ClientName}'", name);
                        }
                    }

                    _logger.LogDebug("Physical client disposed — last reference removed");
                }
                else
                {
                    _logger.LogDebug(
                        "Physical client for '{Name}' still referenced by {Count} other client(s), not disposed",
                        name, refCount);
                }
            }

            _runtimeConfigurations.TryRemove(name, out _);
            return true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FastDFSClientFactory));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _logger.LogInformation(
                "Disposing FastDFSClientFactory ({PhysicalCount} physical client(s), {NameCount} named client(s))",
                _sharedClients.Count, _clients.Count);

            lock (_lock)
            {
                foreach (var kvp in _sharedClients)
                {
                    try
                    {
                        _logger.LogDebug("Disposing physical FastDFS client '{Key}'", kvp.Key);
                        (kvp.Value as IDisposable)?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error disposing physical FastDFS client '{Key}'", kvp.Key);
                    }
                }

                _sharedClients.Clear();
                _sharedRefCounts.Clear();
                _nameToConfigKey.Clear();
            }

            _clients.Clear();
            _runtimeConfigurations.Clear();

            _logger.LogInformation("FastDFSClientFactory disposed successfully");

            GC.SuppressFinalize(this);
        }
    }
}
