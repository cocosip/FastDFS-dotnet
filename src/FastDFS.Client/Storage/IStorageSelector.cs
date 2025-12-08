using System.Collections.Generic;
using FastDFS.Client.Tracker;

namespace FastDFS.Client.Storage
{
    /// <summary>
    /// Interface for storage server selection strategies.
    /// Used to select the optimal storage server from multiple available servers.
    /// </summary>
    public interface IStorageSelector
    {
        /// <summary>
        /// Selects a storage server from a list of available servers.
        /// </summary>
        /// <param name="servers">List of available storage servers.</param>
        /// <returns>The selected storage server.</returns>
        StorageServerInfo Select(List<StorageServerInfo> servers);

        /// <summary>
        /// Gets the name of this storage selector strategy.
        /// </summary>
        string Name { get; }
    }
}
