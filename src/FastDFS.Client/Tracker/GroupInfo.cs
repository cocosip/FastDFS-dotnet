namespace FastDFS.Client.Tracker
{
    /// <summary>
    /// FastDFS storage group information.
    /// Contains information about a storage group including capacity and server status.
    /// </summary>
    public class GroupInfo
    {
        /// <summary>
        /// Gets or sets the group name.
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total disk space in MB.
        /// </summary>
        public long TotalMB { get; set; }

        /// <summary>
        /// Gets or sets the free disk space in MB.
        /// </summary>
        public long FreeMB { get; set; }

        /// <summary>
        /// Gets or sets the trunk free space in MB.
        /// </summary>
        public long TrunkFreeMB { get; set; }

        /// <summary>
        /// Gets or sets the number of storage servers in this group.
        /// </summary>
        public int StorageServerCount { get; set; }

        /// <summary>
        /// Gets or sets the storage server port.
        /// </summary>
        public int StoragePort { get; set; }

        /// <summary>
        /// Gets or sets the storage HTTP port.
        /// </summary>
        public int StorageHttpPort { get; set; }

        /// <summary>
        /// Gets or sets the number of active servers.
        /// </summary>
        public int ActiveServerCount { get; set; }

        /// <summary>
        /// Gets or sets the current write server index.
        /// </summary>
        public int CurrentWriteServer { get; set; }

        /// <summary>
        /// Gets or sets the store path count.
        /// </summary>
        public int StorePathCount { get; set; }

        /// <summary>
        /// Gets or sets the subdir count per path.
        /// </summary>
        public int SubdirCountPerPath { get; set; }

        /// <summary>
        /// Gets or sets the current trunk file ID.
        /// </summary>
        public int CurrentTrunkFileId { get; set; }

        /// <summary>
        /// Gets the disk usage percentage.
        /// </summary>
        public double DiskUsagePercentage
        {
            get
            {
                if (TotalMB == 0) return 0;
                return (double)(TotalMB - FreeMB) / TotalMB * 100;
            }
        }

        /// <summary>
        /// Returns a string representation of the group information.
        /// </summary>
        public override string ToString()
        {
            return $"GroupInfo [Name={GroupName}, Total={TotalMB}MB, Free={FreeMB}MB, Usage={DiskUsagePercentage:F2}%, Servers={StorageServerCount}, Active={ActiveServerCount}]";
        }
    }
}
