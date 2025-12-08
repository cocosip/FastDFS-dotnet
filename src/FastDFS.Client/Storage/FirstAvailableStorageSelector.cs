using System;
using System.Collections.Generic;
using FastDFS.Client.Tracker;

namespace FastDFS.Client.Storage
{
    /// <summary>
    /// Storage selector that always selects the first available storage server.
    /// This is the default and simplest strategy, relying on tracker's ordering.
    /// </summary>
    public class FirstAvailableStorageSelector : IStorageSelector
    {
        /// <inheritdoc/>
        public string Name => "FirstAvailable";

        /// <inheritdoc/>
        public StorageServerInfo Select(List<StorageServerInfo> servers)
        {
            if (servers == null || servers.Count == 0)
                throw new ArgumentException("Server list cannot be null or empty.", nameof(servers));

            return servers[0];
        }
    }
}
