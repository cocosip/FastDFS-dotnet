using System;
using System.Collections.Generic;
using FastDFS.Client.Storage;

namespace FastDFS.Client.Configuration
{
    /// <summary>
    /// Configuration for a FastDFS cluster.
    /// </summary>
    public class FastDFSConfiguration
    {
        /// <summary>
        /// Gets or sets the tracker server endpoints.
        /// Format: "host:port" (e.g., "192.168.1.100:22122").
        /// </summary>
        public List<string> TrackerServers { get; set; } = [];

        /// <summary>
        /// Gets or sets the connection pool configuration.
        /// </summary>
        public ConnectionPoolConfiguration ConnectionPool { get; set; } = new ConnectionPoolConfiguration();

        /// <summary>
        /// Gets or sets the network timeout in seconds.
        /// This is a general timeout setting. Default is 30 seconds.
        /// For more granular control, use ConnectionPool timeout settings.
        /// </summary>
        public int NetworkTimeout { get; set; } = 30;

        /// <summary>
        /// Gets or sets the charset encoding name.
        /// Default is "UTF-8".
        /// </summary>
        public string Charset { get; set; } = "UTF-8";

        /// <summary>
        /// Gets or sets the default group name.
        /// This is used when file IDs don't contain group name prefix.
        /// Optional: Can be null if all file IDs contain group names.
        /// </summary>
        public string? DefaultGroupName { get; set; }

        /// <summary>
        /// Gets or sets the storage server selection strategy.
        /// Default is TrackerSelection (server-side selection, most efficient).
        /// Other options: FirstAvailable, Random, RoundRobin.
        /// </summary>
        public StorageSelectionStrategy StorageSelectionStrategy { get; set; } = StorageSelectionStrategy.TrackerSelection;

        /// <summary>
        /// Gets or sets the HTTP access configuration for FastDFS Nginx module.
        /// Configure this to enable HTTP URL generation for files.
        /// Optional: Can be null if HTTP access is not needed.
        /// </summary>
        public HttpConfiguration? HttpConfig { get; set; }

        /// <summary>
        /// Validates the configuration options.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when configuration is invalid.</exception>
        public void Validate()
        {
            if (TrackerServers == null || TrackerServers.Count == 0)
                throw new ArgumentException("At least one tracker server must be configured.", nameof(TrackerServers));

            // Validate each tracker server endpoint format
            foreach (var server in TrackerServers)
            {
                if (string.IsNullOrWhiteSpace(server))
                    throw new ArgumentException("Tracker server endpoint cannot be null or empty.", nameof(TrackerServers));

                // Basic format validation: should contain ':'
                if (!server.Contains(":"))
                    throw new ArgumentException($"Invalid tracker server endpoint format: {server}. Expected format: 'host:port'", nameof(TrackerServers));

                // Try to parse the port
                var parts = server.Split(':');
                if (parts.Length != 2)
                    throw new ArgumentException($"Invalid tracker server endpoint format: {server}. Expected format: 'host:port'", nameof(TrackerServers));

                if (!int.TryParse(parts[1], out int port) || port <= 0 || port > 65535)
                    throw new ArgumentException($"Invalid port number in tracker server endpoint: {server}", nameof(TrackerServers));
            }

            if (NetworkTimeout <= 0)
                throw new ArgumentException("NetworkTimeout must be greater than 0.", nameof(NetworkTimeout));

            if (string.IsNullOrWhiteSpace(Charset))
                throw new ArgumentException("Charset cannot be null or empty.", nameof(Charset));

            // Validate connection pool options
            ConnectionPool?.Validate();

            // Validate HTTP configuration if provided
            HttpConfig?.Validate();
        }

        /// <summary>
        /// Creates a copy of this configuration instance.
        /// </summary>
        public FastDFSConfiguration Clone()
        {
            return new FastDFSConfiguration
            {
                TrackerServers = new List<string>(TrackerServers),
                ConnectionPool = new ConnectionPoolConfiguration
                {
                    MaxConnectionPerServer = ConnectionPool.MaxConnectionPerServer,
                    MinConnectionPerServer = ConnectionPool.MinConnectionPerServer,
                    ConnectionIdleTimeout = ConnectionPool.ConnectionIdleTimeout,
                    ConnectionLifetime = ConnectionPool.ConnectionLifetime,
                    ConnectionTimeout = ConnectionPool.ConnectionTimeout,
                    SendTimeout = ConnectionPool.SendTimeout,
                    ReceiveTimeout = ConnectionPool.ReceiveTimeout
                },
                NetworkTimeout = NetworkTimeout,
                Charset = Charset,
                DefaultGroupName = DefaultGroupName,
                StorageSelectionStrategy = StorageSelectionStrategy,
                HttpConfig = HttpConfig != null ? new HttpConfiguration
                {
                    ServerUrls = new Dictionary<string, string>(HttpConfig.ServerUrls),
                    DefaultServerUrlTemplate = HttpConfig.DefaultServerUrlTemplate,
                    SecretKey = HttpConfig.SecretKey,
                    AntiStealTokenEnabled = HttpConfig.AntiStealTokenEnabled,
                    DefaultTokenExpireSeconds = HttpConfig.DefaultTokenExpireSeconds
                } : null
            };
        }
    }
}
