using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    /// Clients with identical configurations share the same underlying connection pool resources.
    /// </summary>
    public class FastDFSClientFactory : IFastDFSClientFactory, IDisposable
    {
        private readonly IOptionsMonitor<FastDFSConfiguration> _optionsMonitor;
        private readonly IConnectionPoolProviderFactory _poolProviderFactory;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly ILogger _logger;

        // name -> logical client instance
        private readonly ConcurrentDictionary<string, IFastDFSClient> _clients;
        private readonly ConcurrentDictionary<string, FastDFSConfiguration> _runtimeConfigurations;
        private readonly HashSet<string> _registeredClientNames = new(StringComparer.Ordinal);

        // Shared connection pool resources keyed by config fingerprint (protected by _lock)
        private readonly Dictionary<string, SharedClientResources> _sharedResources = new();
        private readonly Dictionary<string, int> _sharedRefCounts = new();
        private readonly Dictionary<string, string> _nameToConfigKey = new();

        private readonly object _lock = new object();
        private int _disposed;

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

            lock (_lock)
            {
                ThrowIfDisposed();

                if (_clients.TryGetValue(name, out var existingClient))
                {
                    _logger.LogDebug("Returning existing FastDFS client '{ClientName}'", name);
                    return existingClient;
                }

                _logger.LogInformation("Creating FastDFS client '{ClientName}'", name);

                FastDFSConfiguration? options;
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
                _registeredClientNames.Add(name);

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
                return _registeredClientNames.ToList();
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

                if (_registeredClientNames.Contains(name))
                    return true;

                if (_runtimeConfigurations.ContainsKey(name) || _clients.ContainsKey(name))
                {
                    _registeredClientNames.Add(name);
                    return true;
                }

                try
                {
                    var options = _optionsMonitor.Get(name);
                    if (options?.TrackerServers != null && options.TrackerServers.Count > 0)
                    {
                        _registeredClientNames.Add(name);
                        return true;
                    }

                    return false;
                }
                catch
                {
                    return false;
                }
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
                _logger.LogInformation("Registering FastDFS client '{ClientName}' at runtime", name);

                if (_clients.ContainsKey(name))
                {
                    _logger.LogWarning("Client '{ClientName}' already exists, it will be replaced", name);
                    RemoveClientInternal(name);
                }

                _runtimeConfigurations[name] = configuration;
                _registeredClientNames.Add(name);

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
                var poolProvider = _poolProviderFactory.Create(configuration.ConnectionPool);
                sharedResources = new SharedClientResources(poolProvider);
                _sharedResources[configKey] = sharedResources;
                _sharedRefCounts[configKey] = 0;

                _logger.LogInformation(
                    "Created shared connection pool resources for '{Name}' with {TrackerCount} tracker server(s)",
                    name, configuration.TrackerServers.Count);
            }

            _nameToConfigKey[name] = configKey;
            _sharedRefCounts[configKey]++;

            if (_sharedRefCounts[configKey] > 1)
            {
                _logger.LogInformation(
                    "FastDFS client '{Name}' sharing connection pool resources with {Count} existing client(s) (identical configuration)",
                    name, _sharedRefCounts[configKey] - 1);
            }

            var trackerClient = new TrackerClient(configuration.TrackerServers, sharedResources.PoolProvider, _loggerFactory);
            var storageClient = new StorageClient(sharedResources.PoolProvider, configuration.ConnectionPool.StreamCopyBufferSize, _loggerFactory);
            return new FastDFSClient(
                trackerClient,
                storageClient,
                name,
                configuration.DefaultGroupName,
                configuration.StorageSelectionStrategy,
                configuration.HttpConfig,
                _loggerFactory);
        }

        /// <summary>
        /// Removes a client by name and decrements the shared reference count,
        /// disposing the shared pool resources only when the last reference is removed.
        /// Must be called within <see cref="_lock"/>.
        /// </summary>
        private bool RemoveClientInternal(string name)
        {
            var removed = false;

            if (_clients.TryRemove(name, out var client))
            {
                removed = true;
                _logger.LogInformation("Removing FastDFS client '{ClientName}'", name);

                try
                {
                    (client as IDisposable)?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing FastDFS client '{ClientName}'", name);
                }
            }

            if (_nameToConfigKey.TryGetValue(name, out var configKey))
            {
                removed = true;
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
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error disposing shared connection pool resources for '{ClientName}'", name);
                        }
                    }

                    _logger.LogDebug("Shared connection pool resources disposed — last reference removed");
                }
                else
                {
                    _logger.LogDebug(
                        "Shared connection pool resources for '{Name}' still referenced by {Count} other client(s), not disposed",
                        name, refCount);
                }
            }

            removed |= _runtimeConfigurations.TryRemove(name, out _);
            removed |= _registeredClientNames.Remove(name);
            return removed;
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) == 1)
                throw new ObjectDisposedException(nameof(FastDFSClientFactory));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            _logger.LogInformation(
                "Disposing FastDFSClientFactory ({PhysicalCount} shared resource set(s), {NameCount} named client(s))",
                _sharedResources.Count, _clients.Count);

            lock (_lock)
            {
                foreach (var kvp in _clients)
                {
                    try
                    {
                        _logger.LogDebug("Disposing logical FastDFS client '{Name}'", kvp.Key);
                        (kvp.Value as IDisposable)?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error disposing logical FastDFS client '{Name}'", kvp.Key);
                    }
                }

                foreach (var kvp in _sharedResources)
                {
                    try
                    {
                        _logger.LogDebug("Disposing shared connection pool resources '{Key}'", kvp.Key);
                        kvp.Value.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error disposing shared connection pool resources '{Key}'", kvp.Key);
                    }
                }

                _sharedResources.Clear();
                _sharedRefCounts.Clear();
                _nameToConfigKey.Clear();
                _clients.Clear();
                _runtimeConfigurations.Clear();
                _registeredClientNames.Clear();
            }

            _logger.LogInformation("FastDFSClientFactory disposed successfully");

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
