using System.Collections.Generic;
using FastDFS.Client.Configuration;

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

        /// <summary>
        /// Registers a new FastDFS client configuration at runtime.
        /// This allows dynamic registration of clients after application startup.
        /// If a client with the same name already exists, it will be replaced.
        /// </summary>
        /// <param name="name">The client name.</param>
        /// <param name="configuration">The FastDFS configuration.</param>
        /// <returns>The newly created or replaced client instance.</returns>
        /// <remarks>
        /// This method is useful when client configurations are loaded from external sources
        /// (e.g., database, configuration center) after the application has started.
        /// The configuration will be validated before creating the client.
        /// If a client with the same name exists, it will be disposed and replaced.
        /// </remarks>
        IFastDFSClient RegisterClient(string name, FastDFSConfiguration configuration);

        /// <summary>
        /// Removes a client with the specified name.
        /// The client will be disposed if it implements IDisposable.
        /// </summary>
        /// <param name="name">The client name.</param>
        /// <returns>True if the client was removed; false if no client with that name exists.</returns>
        bool RemoveClient(string name);
    }
}
