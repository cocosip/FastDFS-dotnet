namespace FastDFS.Client.Storage
{
    /// <summary>
    /// Enumeration of storage server selection strategies.
    /// </summary>
    public enum StorageSelectionStrategy
    {
        /// <summary>
        /// Always use the first available storage server returned by tracker.
        /// This is the default strategy, relying on tracker's server-side load balancing.
        /// </summary>
        FirstAvailable = 0,

        /// <summary>
        /// Randomly select a storage server from available servers.
        /// Provides simple client-side load distribution.
        /// </summary>
        Random = 1,

        /// <summary>
        /// Use round-robin algorithm to select storage servers.
        /// Distributes load evenly across all available servers.
        /// </summary>
        RoundRobin = 2,

        /// <summary>
        /// Use tracker's server-side selection (query single storage).
        /// This queries tracker for only one storage server, letting tracker handle selection.
        /// Most efficient as it reduces network overhead.
        /// </summary>
        TrackerSelection = 3
    }
}
