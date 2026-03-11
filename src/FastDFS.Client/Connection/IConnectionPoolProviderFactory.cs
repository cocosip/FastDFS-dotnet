using System;
using FastDFS.Client.Configuration;

namespace FastDFS.Client.Connection
{
    /// <summary>
    /// Creates <see cref="IConnectionPoolProvider"/> instances for a specific connection pool configuration.
    /// </summary>
    public interface IConnectionPoolProviderFactory
    {
        /// <summary>
        /// Creates a connection pool provider for the specified configuration.
        /// </summary>
        /// <param name="configuration">The connection pool configuration.</param>
        /// <returns>A connection pool provider.</returns>
        IConnectionPoolProvider Create(ConnectionPoolConfiguration configuration);
    }
}
