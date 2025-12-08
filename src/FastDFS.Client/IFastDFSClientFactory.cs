using System.Collections.Generic;

namespace FastDFS.Client
{
    /// <summary>
    /// Factory for creating and managing named FastDFS client instances.
    /// Used in multi-cluster scenarios where multiple FastDFS clusters need to be accessed.
    /// </summary>
    public interface IFastDFSClientFactory
    {
        /// <summary>
        /// Gets the default FastDFS client instance.
        /// </summary>
        /// <returns>The default client instance.</returns>
        IFastDFSClient GetClient();

        /// <summary>
        /// Gets a named FastDFS client instance.
        /// </summary>
        /// <param name="name">The client name.</param>
        /// <returns>The named client instance.</returns>
        IFastDFSClient GetClient(string name);

        /// <summary>
        /// Gets all configured client names.
        /// </summary>
        /// <returns>A collection of client names.</returns>
        IEnumerable<string> GetClientNames();

        /// <summary>
        /// Checks if a client with the specified name exists.
        /// </summary>
        /// <param name="name">The client name.</param>
        /// <returns>True if the client exists; otherwise, false.</returns>
        bool HasClient(string name);
    }
}
