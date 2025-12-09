using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <param name="configuration">The FastDFS options.</param>
        /// <param name="name">Optional: The client name. Default is "default".</param>
        /// <returns>A new IFastDFSClient instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
        /// <exception cref="ArgumentException">Thrown when options are invalid.</exception>
        public static IFastDFSClient CreateClient(FastDFSConfiguration configuration, string name = "default")
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            // Validate options
            configuration.Validate();

            // Create tracker client
            var trackerEndpoints = configuration.TrackerServers.ToList();
            var trackerClient = new TrackerClient(trackerEndpoints, configuration.ConnectionPool);

            // Create and return FastDFS client
            // FastDFSClient will manage its own storage server connection pools internally
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
        /// <param name="trackerServers">The tracker server endpoints (format: "host:port").</param>
        /// <param name="configureConnectionPool">Optional: Configure connection pool options.</param>
        /// <param name="name">Optional: The client name. Default is "default".</param>
        /// <returns>A new IFastDFSClient instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when trackerServers is null.</exception>
        /// <exception cref="ArgumentException">Thrown when trackerServers is empty or invalid.</exception>
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

            // Configure connection pool if provided
            configureConnectionPool?.Invoke(options.ConnectionPool);

            return CreateClient(options, name);
        }

        /// <summary>
        /// Creates a FastDFS client with a single tracker server.
        /// </summary>
        /// <param name="trackerServer">The tracker server endpoint (format: "host:port").</param>
        /// <param name="name">Optional: The client name. Default is "default".</param>
        /// <returns>A new IFastDFSClient instance.</returns>
        /// <exception cref="ArgumentException">Thrown when trackerServer is null or empty.</exception>
        public static IFastDFSClient CreateClient(string trackerServer, string name = "default")
        {
            if (string.IsNullOrWhiteSpace(trackerServer))
                throw new ArgumentException("Tracker server cannot be null or empty.", nameof(trackerServer));

            return CreateClient(new[] { trackerServer }, null, name);
        }
    }

    /// <summary>
    /// Manager for multiple FastDFS client instances in non-DI scenarios.
    /// Allows managing multiple named clients similar to IFastDFSClientFactory but without DI.
    /// </summary>
    public class FastDFSClientManager : IFastDFSClientFactory, IDisposable
    {
        private readonly Dictionary<string, IFastDFSClient> _clients;
        private readonly Dictionary<string, FastDFSConfiguration> _options;
        private readonly object _lock = new object();
        private bool _disposed;

        private const string DefaultClientName = "default";

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSClientManager"/> class.
        /// </summary>
        public FastDFSClientManager()
        {
            _clients = [];
            _options = [];
        }

        /// <summary>
        /// Adds a named client configuration.
        /// The client will be created lazily on first access.
        /// </summary>
        /// <param name="name">The client name.</param>
        /// <param name="options">The FastDFS options.</param>
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
        /// <param name="name">The client name.</param>
        /// <param name="trackerServers">The tracker server endpoints.</param>
        /// <param name="poolConfigurer">Optional: Configure connection pool options.</param>
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
            lock (_lock)
            {
                if (_clients.TryGetValue(name, out var existingClient))
                    return existingClient;

                // Create new client
                if (!_options.TryGetValue(name, out var options))
                {
                    throw new InvalidOperationException($"No configuration found for client '{name}'. Please call AddClient(\"{name}\", ...) first.");
                }

                var client = FastDFSClientBuilder.CreateClient(options, name);
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

        /// <summary>
        /// Throws ObjectDisposedException if the manager has been disposed.
        /// </summary>
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
                foreach (var client in _clients.Values)
                {
                    try
                    {
                        if (client is IDisposable disposableClient)
                            disposableClient.Dispose();
                    }
                    catch
                    {
                        // Suppress exceptions during disposal
                    }
                }

                _clients.Clear();
                _options.Clear();
            }

            GC.SuppressFinalize(this);
        }
    }
}
