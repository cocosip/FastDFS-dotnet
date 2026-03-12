using System;
using FastDFS.Client.Configuration;
using FastDFS.Client.Connection;
using FastDFS.Client.Storage;
using FastDFS.Client.Tracker;

namespace FastDFS.Client
{
    /// <summary>
    /// Builder for creating FastDFS client instances in non-DI scenarios.
    /// Provides a simple factory pattern for creating clients without dependency injection.
    /// </summary>
    public static class FastDFSClientComposer
    {
        /// <summary>
        /// Creates a FastDFS client with shared tracker and storage dependencies.
        /// </summary>
        /// <param name="configuration">The client configuration.</param>
        /// <param name="name">The client name.</param>
        /// <param name="providerFactory">Optional connection pool provider factory.</param>
        /// <returns>A fully composed FastDFS client.</returns>
        public static IFastDFSClient Compose(
            FastDFSConfiguration configuration,
            string name = "default",
            IConnectionPoolProviderFactory? providerFactory = null)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            configuration.Validate();

            providerFactory ??= new DefaultConnectionPoolProviderFactory();
            var poolProvider = providerFactory.Create(configuration.ConnectionPool);
            var trackerClient = new TrackerClient(configuration.TrackerServers, poolProvider);
            var storageClient = new StorageClient(poolProvider, configuration.ConnectionPool.StreamCopyBufferSize);

            return new FastDFSClient(
                trackerClient,
                storageClient,
                name,
                configuration.DefaultGroupName,
                configuration.StorageSelectionStrategy,
                configuration.HttpConfig);
        }
    }
}
