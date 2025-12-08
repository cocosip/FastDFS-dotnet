using System;
using System.Collections.Generic;
using FastDFS.Client.Tracker;

namespace FastDFS.Client.Storage
{
    /// <summary>
    /// Storage selector that randomly selects a storage server from available servers.
    /// Provides simple load distribution across storage servers.
    /// </summary>
    public class RandomStorageSelector : IStorageSelector
    {
        private readonly Random _random;
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="RandomStorageSelector"/> class.
        /// </summary>
        public RandomStorageSelector()
        {
            _random = new Random();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RandomStorageSelector"/> class with a seed.
        /// </summary>
        /// <param name="seed">The seed for random number generation.</param>
        public RandomStorageSelector(int seed)
        {
            _random = new Random(seed);
        }

        /// <inheritdoc/>
        public string Name => "Random";

        /// <inheritdoc/>
        public StorageServerInfo Select(List<StorageServerInfo> servers)
        {
            if (servers == null || servers.Count == 0)
                throw new ArgumentException("Server list cannot be null or empty.", nameof(servers));

            if (servers.Count == 1)
                return servers[0];

            lock (_lock)
            {
                int index = _random.Next(servers.Count);
                return servers[index];
            }
        }
    }
}
