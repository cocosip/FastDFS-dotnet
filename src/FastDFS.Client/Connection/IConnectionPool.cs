using System;
using System.Threading;
using System.Threading.Tasks;

namespace FastDFS.Client.Connection
{
    /// <summary>
    /// Interface for managing a pool of FastDFS connections.
    /// </summary>
    public interface IConnectionPool : IDisposable
    {
        /// <summary>
        /// Gets a connection from the pool asynchronously.
        /// If no idle connection is available and the pool is not at maximum capacity, a new connection will be created.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A FastDFS connection.</returns>
        /// <exception cref="TimeoutException">Thrown when no connection is available within the timeout period.</exception>
        Task<FastDFSConnection> GetConnectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a connection to the pool.
        /// </summary>
        /// <param name="connection">The connection to return.</param>
        void ReturnConnection(FastDFSConnection connection);

        /// <summary>
        /// Gets the total number of connections (both idle and active) in the pool.
        /// </summary>
        int TotalConnections { get; }

        /// <summary>
        /// Gets the number of idle connections available in the pool.
        /// </summary>
        int IdleConnections { get; }

        /// <summary>
        /// Gets the number of active connections currently in use.
        /// </summary>
        int ActiveConnections { get; }
    }
}
