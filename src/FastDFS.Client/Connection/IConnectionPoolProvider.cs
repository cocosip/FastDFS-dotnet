using System;
using System.Collections.Generic;

namespace FastDFS.Client.Connection
{
    /// <summary>
    /// Provides connection pools for FastDFS server endpoints.
    /// </summary>
    public interface IConnectionPoolProvider : IDisposable
    {
        /// <summary>
        /// Gets an existing connection pool for the specified endpoint or creates one if it does not exist.
        /// </summary>
        /// <param name="endpoint">The server endpoint.</param>
        /// <returns>The connection pool for the endpoint.</returns>
        IConnectionPool GetOrCreate(ConnectionEndpoint endpoint);

        /// <summary>
        /// Tries to get an existing connection pool for the specified endpoint.
        /// </summary>
        /// <param name="endpoint">The server endpoint.</param>
        /// <param name="pool">When this method returns, contains the connection pool if found; otherwise, null.</param>
        /// <returns><see langword="true"/> if a pool exists; otherwise, <see langword="false"/>.</returns>
        bool TryGet(ConnectionEndpoint endpoint, out IConnectionPool? pool);

        /// <summary>
        /// Gets the endpoints that currently have a created connection pool.
        /// </summary>
        /// <returns>A snapshot of endpoints with active pools.</returns>
        IReadOnlyCollection<ConnectionEndpoint> GetEndpoints();
    }
}
