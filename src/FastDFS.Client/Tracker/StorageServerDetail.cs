using System;

namespace FastDFS.Client.Tracker
{
    /// <summary>
    /// Detailed information about a FastDFS storage server.
    /// Used for management and monitoring purposes.
    /// </summary>
    public class StorageServerDetail
    {
        /// <summary>
        /// Gets or sets the server status.
        /// </summary>
        public StorageServerStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the server ID (IP address).
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the server IP address (old format for backward compatibility).
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the source IP address (if different from IpAddress).
        /// </summary>
        public string SourceIpAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the domain name.
        /// </summary>
        public string DomainName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the FastDFS version.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the join time (when the server joined the cluster).
        /// </summary>
        public DateTime JoinTime { get; set; }

        /// <summary>
        /// Gets or sets the last heartbeat time.
        /// </summary>
        public DateTime LastHeartbeatTime { get; set; }

        /// <summary>
        /// Gets or sets the last sync time (last time synced with other storage servers).
        /// </summary>
        public DateTime LastSyncTime { get; set; }

        /// <summary>
        /// Gets or sets the sync until timestamp (synced up to this timestamp).
        /// </summary>
        public DateTime SyncUntilTimestamp { get; set; }

        /// <summary>
        /// Gets or sets the sync delay in seconds.
        /// </summary>
        public long SyncDelaySeconds { get; set; }

        /// <summary>
        /// Gets or sets the total disk space in MB.
        /// </summary>
        public long TotalMB { get; set; }

        /// <summary>
        /// Gets or sets the free disk space in MB.
        /// </summary>
        public long FreeMB { get; set; }

        /// <summary>
        /// Gets or sets the upload priority (higher value = higher priority).
        /// </summary>
        public int UploadPriority { get; set; }

        /// <summary>
        /// Gets or sets the store path count.
        /// </summary>
        public int StorePathCount { get; set; }

        /// <summary>
        /// Gets or sets the subdir count per path.
        /// </summary>
        public int SubdirCountPerPath { get; set; }

        /// <summary>
        /// Gets or sets the current write path index.
        /// </summary>
        public int CurrentWritePath { get; set; }

        /// <summary>
        /// Gets or sets the storage server port.
        /// </summary>
        public int StoragePort { get; set; }

        /// <summary>
        /// Gets or sets the storage HTTP port.
        /// </summary>
        public int StorageHttpPort { get; set; }

        /// <summary>
        /// Gets or sets the total upload count.
        /// </summary>
        public long TotalUploadCount { get; set; }

        /// <summary>
        /// Gets or sets the success upload count.
        /// </summary>
        public long SuccessUploadCount { get; set; }

        /// <summary>
        /// Gets or sets the total append count.
        /// </summary>
        public long TotalAppendCount { get; set; }

        /// <summary>
        /// Gets or sets the success append count.
        /// </summary>
        public long SuccessAppendCount { get; set; }

        /// <summary>
        /// Gets or sets the total modify count.
        /// </summary>
        public long TotalModifyCount { get; set; }

        /// <summary>
        /// Gets or sets the success modify count.
        /// </summary>
        public long SuccessModifyCount { get; set; }

        /// <summary>
        /// Gets or sets the total truncate count.
        /// </summary>
        public long TotalTruncateCount { get; set; }

        /// <summary>
        /// Gets or sets the success truncate count.
        /// </summary>
        public long SuccessTruncateCount { get; set; }

        /// <summary>
        /// Gets or sets the total set metadata count.
        /// </summary>
        public long TotalSetMetadataCount { get; set; }

        /// <summary>
        /// Gets or sets the success set metadata count.
        /// </summary>
        public long SuccessSetMetadataCount { get; set; }

        /// <summary>
        /// Gets or sets the total delete count.
        /// </summary>
        public long TotalDeleteCount { get; set; }

        /// <summary>
        /// Gets or sets the success delete count.
        /// </summary>
        public long SuccessDeleteCount { get; set; }

        /// <summary>
        /// Gets or sets the total download count.
        /// </summary>
        public long TotalDownloadCount { get; set; }

        /// <summary>
        /// Gets or sets the success download count.
        /// </summary>
        public long SuccessDownloadCount { get; set; }

        /// <summary>
        /// Gets or sets the total get metadata count.
        /// </summary>
        public long TotalGetMetadataCount { get; set; }

        /// <summary>
        /// Gets or sets the success get metadata count.
        /// </summary>
        public long SuccessGetMetadataCount { get; set; }

        /// <summary>
        /// Gets or sets the last source update timestamp.
        /// </summary>
        public DateTime LastSourceUpdate { get; set; }

        /// <summary>
        /// Gets or sets the last sync update timestamp.
        /// </summary>
        public DateTime LastSyncUpdate { get; set; }

        /// <summary>
        /// Gets or sets whether this server is a trunk server.
        /// </summary>
        public bool IsTrunkServer { get; set; }

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
        /// Gets whether the server is online (based on heartbeat).
        /// </summary>
        public bool IsOnline
        {
            get
            {
                var heartbeatAge = DateTime.Now - LastHeartbeatTime;
                return heartbeatAge.TotalSeconds < 120; // Consider offline if no heartbeat for 2 minutes
            }
        }

        /// <summary>
        /// Returns a string representation of the storage server detail.
        /// </summary>
        public override string ToString()
        {
            return $"StorageServer [IP={IpAddress}:{StoragePort}, Status={Status}, Online={IsOnline}, Total={TotalMB}MB, Free={FreeMB}MB, Usage={DiskUsagePercentage:F2}%]";
        }
    }

    /// <summary>
    /// Storage server status enumeration.
    /// </summary>
    public enum StorageServerStatus : byte
    {
        /// <summary>
        /// Server is initializing.
        /// </summary>
        Init = 0,

        /// <summary>
        /// Server is waiting for sync.
        /// </summary>
        WaitSync = 1,

        /// <summary>
        /// Server is syncing.
        /// </summary>
        Syncing = 2,

        /// <summary>
        /// Server is in recovery mode.
        /// </summary>
        Recovery = 3,

        /// <summary>
        /// Server is offline.
        /// </summary>
        Offline = 4,

        /// <summary>
        /// Server is online (normal operation).
        /// </summary>
        Online = 5,

        /// <summary>
        /// Server is active (can accept uploads).
        /// </summary>
        Active = 6,

        /// <summary>
        /// Server status is unknown.
        /// </summary>
        Unknown = 99
    }
}
