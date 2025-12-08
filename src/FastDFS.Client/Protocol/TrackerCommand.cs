namespace FastDFS.Client.Protocol
{
    /// <summary>
    /// FastDFS Tracker server command codes.
    /// </summary>
    public static class TrackerCommand
    {
        /// <summary>
        /// Query storage server for upload without specifying group (101).
        /// Returns one available storage server.
        /// </summary>
        public const byte QueryStoreWithoutGroupOne = 101;

        /// <summary>
        /// Query storage server for upload with specified group (104).
        /// </summary>
        public const byte QueryStoreWithGroupOne = 104;

        /// <summary>
        /// Query storage server for download (102).
        /// Returns storage server information for the specified file.
        /// </summary>
        public const byte QueryFetchOne = 102;

        /// <summary>
        /// Query storage server for update/delete (103).
        /// </summary>
        public const byte QueryUpdate = 103;

        /// <summary>
        /// Query storage server for upload without specifying group, return all storage servers (105).
        /// </summary>
        public const byte QueryStoreWithoutGroupAll = 105;

        /// <summary>
        /// Query storage server for upload with specified group, return all storage servers (106).
        /// </summary>
        public const byte QueryStoreWithGroupAll = 106;

        /// <summary>
        /// Query storage server for download, return all storage servers (107).
        /// </summary>
        public const byte QueryFetchAll = 107;

        /// <summary>
        /// List all groups (91).
        /// </summary>
        public const byte ListAllGroups = 91;

        /// <summary>
        /// Query specific group information (92).
        /// </summary>
        public const byte QueryGroupInfo = 92;

        /// <summary>
        /// List storage servers in a group (93).
        /// </summary>
        public const byte ListStorageServers = 93;

        /// <summary>
        /// Query storage server status (94).
        /// </summary>
        public const byte QueryStorageServerStatus = 94;

        /// <summary>
        /// Delete storage server (95).
        /// </summary>
        public const byte DeleteStorage = 95;
    }
}
