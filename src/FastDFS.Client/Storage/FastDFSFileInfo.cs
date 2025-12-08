using System;

namespace FastDFS.Client.Storage
{
    /// <summary>
    /// FastDFS file information.
    /// </summary>
    public class FastDFSFileInfo
    {
        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Gets or sets the file creation timestamp (Unix timestamp).
        /// </summary>
        public long CreateTimestamp { get; set; }

        /// <summary>
        /// Gets or sets the CRC32 checksum of the file.
        /// </summary>
        public long Crc32 { get; set; }

        /// <summary>
        /// Gets or sets the source IP address of the storage server.
        /// </summary>
        public string SourceIpAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets the file creation time as DateTime (UTC).
        /// </summary>
        public DateTime CreateTimeUtc => DateTimeOffset.FromUnixTimeSeconds(CreateTimestamp).UtcDateTime;

        /// <summary>
        /// Gets the file creation time as DateTime (local).
        /// </summary>
        public DateTime CreateTimeLocal => DateTimeOffset.FromUnixTimeSeconds(CreateTimestamp).LocalDateTime;

        /// <summary>
        /// Returns a string representation of the file info.
        /// </summary>
        public override string ToString()
        {
            return $"FastDFSFileInfo [Size={FileSize}, CreateTime={CreateTimeUtc:yyyy-MM-dd HH:mm:ss} UTC, CRC32={Crc32:X8}, SourceIP={SourceIpAddress}]";
        }
    }
}
