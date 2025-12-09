using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using FastDFS.Client.Configuration;
using FastDFS.Client.Tracker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FastDFS.Client.DependencyInjection
{
    /// <summary>
    /// Default implementation of IFastDFSClientFactory.
    /// Manages multiple named FastDFS client instances for multi-cluster scenarios.
    /// </summary>
    public class FastDFSClientFactory : IFastDFSClientFactory, IDisposable
    {
        private readonly IOptionsMonitor<FastDFSConfiguration> _optionsMonitor;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, IFastDFSClient> _clients;
        private readonly ConcurrentDictionary<string, ITrackerClient> _trackerClients;
        private readonly object _lock = new object();
        private bool _disposed;

        private const string DefaultClientName = "default";

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSClientFactory"/> class.
        /// </summary>
        /// <param name="optionsMonitor">The options monitor for accessing named configurations.</param>
        /// <param name="loggerFactory">Optional logger factory for creating loggers.</param>
        public FastDFSClientFactory(
            IOptionsMonitor<FastDFSConfiguration> optionsMonitor,
            ILoggerFactory? loggerFactory = null)
        {
            _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
            _loggerFactory = loggerFactory;
            _logger = loggerFactory?.CreateLogger<FastDFSClientFactory>() ?? NullLogger<FastDFSClientFactory>.Instance;
            _clients = new ConcurrentDictionary<string, IFastDFSClient>();
            _trackerClients = new ConcurrentDictionary<string, ITrackerClient>();

            _logger.LogInformation("FastDFSClientFactory initialized");
        }

        /// <inheritdoc/>
        public IFastDFSClient GetClient()
        {
            return GetClient(DefaultClientName);
        }

        /// <inheritdoc/>
        public IFastDFSClient GetClient(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Client name cannot be null or empty.", nameof(name));

            ThrowIfDisposed();

            // Try to get existing client
            if (_clients.TryGetValue(name, out var existingClient))
            {
                _logger.LogDebug("Returning existing FastDFS client '{ClientName}'", name);
                return existingClient;
            }

            // Create new client (thread-safe)
            lock (_lock)
            {
                // Double-check after acquiring lock
                if (_clients.TryGetValue(name, out existingClient))
                    return existingClient;

                _logger.LogInformation("Creating new FastDFS client '{ClientName}'", name);

                // Get configuration for this named client
                var options = _optionsMonitor.Get(name);
                if (options == null || options.TrackerServers == null || options.TrackerServers.Count == 0)
                {
                    _logger.LogError("No configuration found for FastDFS client '{ClientName}'", name);
                    throw new InvalidOperationException($"No configuration found for FastDFS client '{name}'. Please ensure AddFastDFS(\"{name}\", ...) was called.");
                }

                // Validate configuration
                options.Validate();

                // Create TrackerClient for this named client
                var trackerClient = CreateTrackerClient(name, options);
                _trackerClients[name] = trackerClient;

                // Create FastDFSClient
                // FastDFSClient will manage its own storage server connection pools internally
                var client = new FastDFSClient(
                    trackerClient,
                    options.ConnectionPool,
                    name,
                    options.DefaultGroupName,
                    options.StorageSelectionStrategy,
                    options.HttpConfig,
                    _loggerFactory);
                _clients[name] = client;

                _logger.LogInformation("Successfully created FastDFS client '{ClientName}' with {TrackerCount} tracker server(s)",
                    name, options.TrackerServers.Count);

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

        /// <summary>
        /// Creates a TrackerClient for the specified named configuration.
        /// </summary>
        private ITrackerClient CreateTrackerClient(string name, FastDFSConfiguration options)
        {
            var trackerEndpoints = options.TrackerServers.ToList();
            var trackerClient = new TrackerClient(
                trackerEndpoints,
                options.ConnectionPool,
                _loggerFactory);
            return trackerClient;
        }

        /// <summary>
        /// Throws ObjectDisposedException if the factory has been disposed.
        /// </summary>
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

            _logger.LogInformation("Disposing FastDFSClientFactory with {ClientCount} client(s)", _clients.Count);

            // Dispose all clients (FastDFSClient will dispose its own storage connection pools)
            foreach (var kvp in _clients)
            {
                try
                {
                    _logger.LogDebug("Disposing FastDFS client '{ClientName}'", kvp.Key);
                    if (kvp.Value is IDisposable disposableClient)
                        disposableClient.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing FastDFS client '{ClientName}'", kvp.Key);
                }
            }

            // Dispose all tracker clients
            foreach (var kvp in _trackerClients)
            {
                try
                {
                    _logger.LogDebug("Disposing TrackerClient for '{ClientName}'", kvp.Key);
                    if (kvp.Value is IDisposable disposableTracker)
                        disposableTracker.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing TrackerClient for '{ClientName}'", kvp.Key);
                }
            }

            _clients.Clear();
            _trackerClients.Clear();

            _logger.LogInformation("FastDFSClientFactory disposed successfully");

            GC.SuppressFinalize(this);
        }
    }
}
