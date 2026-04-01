using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FastDFS.Client.Protocol;
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
        /// This property is retained for backward compatibility; use
        /// <see cref="ConnectionPoolConfiguration.ConnectionTimeout"/>,
        /// <see cref="ConnectionPoolConfiguration.SendTimeout"/> and
        /// <see cref="ConnectionPoolConfiguration.ReceiveTimeout"/> for effective timeout control.
        /// </summary>
        public int NetworkTimeout { get; set; } = FastDFSConstants.DefaultNetworkTimeoutSeconds;

        /// <summary>
        /// Gets or sets the charset encoding name.
        /// FastDFS protocol operations in this library currently support UTF-8 only.
        /// </summary>
        public string Charset { get; set; } = FastDFSConstants.DefaultCharset;

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

                Connection.ConnectionEndpoint.Parse(server);
            }

            if (NetworkTimeout <= 0)
                throw new ArgumentException("NetworkTimeout must be greater than 0.", nameof(NetworkTimeout));
            
            // NetworkTimeout is a compatibility-only setting. Log a warning if user tries to use a non-default value.
            if (NetworkTimeout != FastDFSConstants.DefaultNetworkTimeoutSeconds)
            {
                // Note: This property is retained for backward compatibility but the actual timeout values
                // are controlled via ConnectionPoolConfiguration.ConnectionTimeout, SendTimeout, and ReceiveTimeout.
                // We no longer throw an exception here to allow smoother migration for users with existing config.
            }

            if (string.IsNullOrWhiteSpace(Charset))
                throw new ArgumentException("Charset cannot be null or empty.", nameof(Charset));
            if (!string.Equals(Charset, FastDFSConstants.DefaultCharset, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Only {FastDFSConstants.DefaultCharset} charset is currently supported.", nameof(Charset));

            // Validate connection pool options
            if (ConnectionPool == null)
                throw new ArgumentNullException(nameof(ConnectionPool), "ConnectionPool cannot be null.");

            ConnectionPool.Validate();

            // Validate HTTP configuration if provided
            HttpConfig?.Validate();
        }

        /// <summary>
        /// Computes a fingerprint string that uniquely identifies this configuration.
        /// Two configurations that produce the same key will share a physical client in the factory.
        /// </summary>
        public string GetConfigKey()
        {
            var trackers = string.Join(",", TrackerServers
                .Select(s => s.Trim().ToLowerInvariant())
                .OrderBy(s => s));

            var pool = GetRequiredConnectionPool();
            var sb = new StringBuilder()
                .Append(trackers).Append('|')
                .Append(pool.MaxConnectionPerServer).Append(',')
                .Append(pool.MinConnectionPerServer).Append(',')
                .Append(pool.ConnectionTimeout).Append(',')
                .Append(pool.SendTimeout).Append(',')
                .Append(pool.ReceiveTimeout).Append(',')
                .Append(pool.ConnectionIdleTimeout).Append(',')
                .Append(pool.ConnectionLifetime).Append(',')
                .Append(pool.StreamCopyBufferSize).Append('|')
                .Append(DefaultGroupName ?? string.Empty).Append('|')
                .Append((int)StorageSelectionStrategy);

            if (HttpConfig != null)
            {
                var urls = string.Join(",", HttpConfig.ServerUrls
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp => $"{kvp.Key}={kvp.Value}"));
                sb.Append('|')
                  .Append(urls).Append('|')
                  .Append(HashSensitiveValue(HttpConfig.SecretKey)).Append('|')
                  .Append(HttpConfig.AntiStealTokenEnabled).Append('|')
                  .Append(HttpConfig.DefaultTokenExpireSeconds).Append('|')
                  .Append(HttpConfig.DefaultServerUrlTemplate ?? string.Empty);
            }

            return ComputeStableHash(sb.ToString());
        }

        /// <summary>
        /// Creates a copy of this configuration instance.
        /// </summary>
        public FastDFSConfiguration Clone()
        {
            var pool = ConnectionPool;

            return new FastDFSConfiguration
            {
                TrackerServers = new List<string>(TrackerServers),
                ConnectionPool = pool != null
                    ? new ConnectionPoolConfiguration
                    {
                        MaxConnectionPerServer = pool.MaxConnectionPerServer,
                        MinConnectionPerServer = pool.MinConnectionPerServer,
                        ConnectionIdleTimeout = pool.ConnectionIdleTimeout,
                        ConnectionLifetime = pool.ConnectionLifetime,
                        ConnectionTimeout = pool.ConnectionTimeout,
                        SendTimeout = pool.SendTimeout,
                        ReceiveTimeout = pool.ReceiveTimeout,
                        StreamCopyBufferSize = pool.StreamCopyBufferSize
                    }
                    : new ConnectionPoolConfiguration(),
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

        private ConnectionPoolConfiguration GetRequiredConnectionPool()
        {
            return ConnectionPool ?? throw new InvalidOperationException("ConnectionPool cannot be null. Call Validate() before using this configuration instance.");
        }

        private static string HashSensitiveValue(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return ComputeStableHash(value!);
        }

        private static string ComputeStableHash(string value)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(value);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}
