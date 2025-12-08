using System;
using System.Collections.Generic;
using System.Threading;
using FastDFS.Client.Tracker;

namespace FastDFS.Client.Storage
{
    /// <summary>
    /// Storage selector that uses round-robin algorithm to select storage servers.
    /// Distributes load evenly across all available storage servers.
    /// </summary>
    public class RoundRobinStorageSelector : IStorageSelector
    {
        private int _currentIndex;

        /// <summary>
        /// Initializes a new instance of the <see cref="RoundRobinStorageSelector"/> class.
        /// </summary>
        public RoundRobinStorageSelector()
        {
            _currentIndex = -1;
        }

        /// <inheritdoc/>
        public string Name => "RoundRobin";

        /// <inheritdoc/>
        public StorageServerInfo Select(List<StorageServerInfo> servers)
        {
            if (servers == null || servers.Count == 0)
                throw new ArgumentException("Server list cannot be null or empty.", nameof(servers));

            if (servers.Count == 1)
                return servers[0];

            // Thread-safe increment and wrap around
            int index = Interlocked.Increment(ref _currentIndex) % servers.Count;
            return servers[index];
        }
    }
}
