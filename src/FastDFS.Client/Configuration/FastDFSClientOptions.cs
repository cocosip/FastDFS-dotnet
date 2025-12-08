using System;
using System.Collections.Generic;
using System.Linq;

namespace FastDFS.Client.Configuration
{
    /// <summary>
    /// Configuration options for multiple FastDFS clusters.
    /// Supports named client configuration for multi-cluster scenarios.
    /// </summary>
    public class FastDFSClientOptions
    {
        /// <summary>
        /// Gets or sets the named cluster configurations.
        /// Key: Cluster name (e.g., "default", "backup", "cdn").
        /// Value: Cluster-specific FastDFS configuration.
        /// </summary>
        public Dictionary<string, FastDFSConfiguration> Clusters { get; set; } = [];

        /// <summary>
        /// Validates the configuration options.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when configuration is invalid.</exception>
        public void Validate()
        {
            if (Clusters == null || Clusters.Count == 0)
                throw new ArgumentException("At least one cluster must be configured.", nameof(Clusters));

            foreach (var cluster in Clusters)
            {
                if (string.IsNullOrWhiteSpace(cluster.Key))
                    throw new ArgumentException("Cluster name cannot be null or empty.", nameof(Clusters));

                if (cluster.Value == null)
                    throw new ArgumentException($"Cluster '{cluster.Key}' options cannot be null.", nameof(Clusters));

                try
                {
                    cluster.Value.Validate();
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Invalid configuration for cluster '{cluster.Key}': {ex.Message}", nameof(Clusters), ex);
                }
            }
        }

        /// <summary>
        /// Gets the configuration for a specific cluster.
        /// </summary>
        /// <param name="name">The cluster name.</param>
        /// <returns>The cluster configuration, or null if not found.</returns>
        public FastDFSConfiguration? GetClusterConfiguration(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            return Clusters.TryGetValue(name, out var config) ? config : null;
        }

        /// <summary>
        /// Adds or updates a cluster configuration.
        /// </summary>
        /// <param name="name">The cluster name.</param>
        /// <param name="configuration">The cluster configuration.</param>
        public void SetClusterConfiguration(string name, FastDFSConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Cluster name cannot be null or empty.", nameof(name));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            Clusters[name] = configuration;
        }

        /// <summary>
        /// Gets all configured cluster names.
        /// </summary>
        public IEnumerable<string> GetClusterNames()
        {
            return Clusters.Keys;
        }
    }
}
