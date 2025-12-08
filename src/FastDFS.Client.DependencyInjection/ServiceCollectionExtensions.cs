using System;
using FastDFS.Client.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FastDFS.Client.DependencyInjection
{
    /// <summary>
    /// Extension methods for registering FastDFS client services with dependency injection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        private const string DefaultClientName = "default";

        /// <summary>
        /// Adds FastDFS client services to the service collection with default client name.
        /// This registers a single-cluster FastDFS client.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">The configuration delegate.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddFastDFS(
            this IServiceCollection services,
            Action<FastDFSConfiguration> configure)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            return services.AddFastDFS(DefaultClientName, configure);
        }

        /// <summary>
        /// Adds FastDFS client services to the service collection with a named client.
        /// This allows multiple FastDFS clusters to be configured in a single application.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="name">The client name (e.g., "default", "backup", "cdn").</param>
        /// <param name="configure">The configuration delegate.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddFastDFS(
            this IServiceCollection services,
            string name,
            Action<FastDFSConfiguration> configure)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Client name cannot be null or empty.", nameof(name));
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            // Register named options
            services.Configure(name, configure);

            // Register factory as singleton (only once)
            services.TryAddSingleton<IFastDFSClientFactory, FastDFSClientFactory>();

            // If this is the default client, also register IFastDFSClient for direct injection
            if (name == DefaultClientName)
            {
                services.TryAddSingleton<IFastDFSClient>(sp =>
                {
                    var factory = sp.GetRequiredService<IFastDFSClientFactory>();
                    return factory.GetClient(DefaultClientName);
                });
            }

            return services;
        }

        /// <summary>
        /// Adds FastDFS client services from configuration section.
        /// Supports both single-cluster and multi-cluster configurations.
        ///
        /// For single-cluster: Configuration should contain TrackerServers, ConnectionPool, etc.
        /// For multi-cluster: Configuration should contain a "Clusters" dictionary.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration section.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddFastDFS(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            // Check if configuration contains "Clusters" section (multi-cluster mode)
            var clustersSection = configuration.GetSection("Clusters");
            if (clustersSection.Exists())
            {
                // Multi-cluster mode
                foreach (var clusterSection in clustersSection.GetChildren())
                {
                    var clusterName = clusterSection.Key;
                    services.Configure<FastDFSConfiguration>(clusterName, clusterSection);
                }

                // Register factory
                services.TryAddSingleton<IFastDFSClientFactory, FastDFSClientFactory>();

                // Register default client if it exists
                var defaultSection = clustersSection.GetSection(DefaultClientName);
                if (defaultSection.Exists())
                {
                    services.TryAddSingleton<IFastDFSClient>(sp =>
                    {
                        var factory = sp.GetRequiredService<IFastDFSClientFactory>();
                        return factory.GetClient(DefaultClientName);
                    });
                }
            }
            else
            {
                // Single-cluster mode (direct configuration)
                services.Configure<FastDFSConfiguration>(DefaultClientName, configuration);

                // Register factory
                services.TryAddSingleton<IFastDFSClientFactory, FastDFSClientFactory>();

                // Register default client
                services.TryAddSingleton<IFastDFSClient>(sp =>
                {
                    var factory = sp.GetRequiredService<IFastDFSClientFactory>();
                    return factory.GetClient(DefaultClientName);
                });
            }

            return services;
        }

        /// <summary>
        /// Adds FastDFS client services from a named configuration section.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="name">The client name.</param>
        /// <param name="configuration">The configuration section.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddFastDFS(
            this IServiceCollection services,
            string name,
            IConfiguration configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Client name cannot be null or empty.", nameof(name));
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            // Register named options from configuration
            services.Configure<FastDFSConfiguration>(name, configuration);

            // Register factory as singleton (only once)
            services.TryAddSingleton<IFastDFSClientFactory, FastDFSClientFactory>();

            // If this is the default client, also register IFastDFSClient for direct injection
            if (name == DefaultClientName)
            {
                services.TryAddSingleton<IFastDFSClient>(sp =>
                {
                    var factory = sp.GetRequiredService<IFastDFSClientFactory>();
                    return factory.GetClient(DefaultClientName);
                });
            }

            return services;
        }
    }
}
