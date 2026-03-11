using System;
using System.Threading;
using System.Threading.Tasks;

namespace FastDFS.Client.Connection
{
    /// <summary>
    /// Helper methods for executing work with pooled FastDFS connections.
    /// </summary>
    public static class ConnectionPoolExtensions
    {
        /// <summary>
        /// Executes an operation with a connection borrowed from the pool and always returns it.
        /// </summary>
        /// <typeparam name="T">The operation result type.</typeparam>
        /// <param name="pool">The connection pool.</param>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The operation result.</returns>
        public static async Task<T> ExecuteAsync<T>(
            this IConnectionPool pool,
            Func<FastDFSConnection, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await operation(connection).ConfigureAwait(false);
            }
            finally
            {
                pool.ReturnConnection(connection);
            }
        }

        /// <summary>
        /// Executes an operation with a connection borrowed from the pool and always returns it.
        /// </summary>
        /// <param name="pool">The connection pool.</param>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the operation.</returns>
        public static async Task ExecuteAsync(
            this IConnectionPool pool,
            Func<FastDFSConnection, Task> operation,
            CancellationToken cancellationToken = default)
        {
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await operation(connection).ConfigureAwait(false);
            }
            finally
            {
                pool.ReturnConnection(connection);
            }
        }
    }
}
