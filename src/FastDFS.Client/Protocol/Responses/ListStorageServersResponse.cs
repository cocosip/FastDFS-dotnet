using System;
using System.Collections.Generic;
using System.Text;
using FastDFS.Client.Tracker;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Protocol.Responses
{
    /// <summary>
    /// Response for list storage servers operation.
    /// Command: TRACKER_PROTO_CMD_SERVER_LIST_STORAGE (93).
    /// Returns detailed information about storage servers in a group.
    /// </summary>
    public class ListStorageServersResponse : FastDFSResponse
    {
        // Each storage server info block size (this may vary by FastDFS version)
        // Common sizes: 592, 600, or similar - needs to be determined from actual protocol
        private const int ServerInfoBlockSize = 600;

        /// <summary>
        /// Gets the list of storage server details.
        /// </summary>
        public List<StorageServerDetail> Servers { get; private set; } = new List<StorageServerDetail>();

        /// <summary>
        /// Decodes the response body containing storage server information.
        /// Body format: multiple fixed-size storage server info blocks.
        /// </summary>
        protected override void DecodeBody(byte[]? body)
        {
            if (body == null || body.Length == 0)
            {
                Servers = new List<StorageServerDetail>();
                return;
            }

            // Calculate number of servers
            int serverCount = body.Length / ServerInfoBlockSize;
            Servers = new List<StorageServerDetail>(serverCount);

            for (int i = 0; i < serverCount; i++)
            {
                int offset = i * ServerInfoBlockSize;

                try
                {
                    var server = new StorageServerDetail
                    {
                        // Server status (1 byte)
                        Status = (StorageServerStatus)body[offset],

                        // Server ID/IP address (16 bytes, FDFS_IPADDR_SIZE)
                        Id = body.ReadFixedString(offset + 1, 16, Encoding.UTF8).TrimEnd('\0'),

                        // Source IP address (16 bytes)
                        SourceIpAddress = body.ReadFixedString(offset + 17, 16, Encoding.UTF8).TrimEnd('\0'),

                        // Domain name (128 bytes, FDFS_DOMAIN_NAME_MAX_SIZE)
                        DomainName = body.ReadFixedString(offset + 33, 128, Encoding.UTF8).TrimEnd('\0'),

                        // Version (6 bytes)
                        Version = body.ReadFixedString(offset + 161, 6, Encoding.UTF8).TrimEnd('\0'),

                        // Join time (8 bytes, timestamp)
                        JoinTime = DateTimeOffset.FromUnixTimeSeconds(ByteConverter.ToInt64(body, offset + 167)).DateTime,

                        // Last heartbeat time (8 bytes, timestamp)
                        LastHeartbeatTime = DateTimeOffset.FromUnixTimeSeconds(ByteConverter.ToInt64(body, offset + 175)).DateTime,

                        // Total disk space (8 bytes, MB)
                        TotalMB = ByteConverter.ToInt64(body, offset + 183),

                        // Free disk space (8 bytes, MB)
                        FreeMB = ByteConverter.ToInt64(body, offset + 191),

                        // Upload priority (8 bytes)
                        UploadPriority = (int)ByteConverter.ToInt64(body, offset + 199),

                        // Store path count (8 bytes)
                        StorePathCount = (int)ByteConverter.ToInt64(body, offset + 207),

                        // Subdir count per path (8 bytes)
                        SubdirCountPerPath = (int)ByteConverter.ToInt64(body, offset + 215),

                        // Current write path (8 bytes)
                        CurrentWritePath = (int)ByteConverter.ToInt64(body, offset + 223),

                        // Storage port (8 bytes)
                        StoragePort = (int)ByteConverter.ToInt64(body, offset + 231),

                        // Storage HTTP port (8 bytes)
                        StorageHttpPort = (int)ByteConverter.ToInt64(body, offset + 239),

                        // Statistics counters (each 8 bytes)
                        TotalUploadCount = ByteConverter.ToInt64(body, offset + 247),
                        SuccessUploadCount = ByteConverter.ToInt64(body, offset + 255),
                        TotalAppendCount = ByteConverter.ToInt64(body, offset + 263),
                        SuccessAppendCount = ByteConverter.ToInt64(body, offset + 271),
                        TotalModifyCount = ByteConverter.ToInt64(body, offset + 279),
                        SuccessModifyCount = ByteConverter.ToInt64(body, offset + 287),
                        TotalTruncateCount = ByteConverter.ToInt64(body, offset + 295),
                        SuccessTruncateCount = ByteConverter.ToInt64(body, offset + 303),
                        TotalSetMetadataCount = ByteConverter.ToInt64(body, offset + 311),
                        SuccessSetMetadataCount = ByteConverter.ToInt64(body, offset + 319),
                        TotalDeleteCount = ByteConverter.ToInt64(body, offset + 327),
                        SuccessDeleteCount = ByteConverter.ToInt64(body, offset + 335),
                        TotalDownloadCount = ByteConverter.ToInt64(body, offset + 343),
                        SuccessDownloadCount = ByteConverter.ToInt64(body, offset + 351),
                        TotalGetMetadataCount = ByteConverter.ToInt64(body, offset + 359),
                        SuccessGetMetadataCount = ByteConverter.ToInt64(body, offset + 367),

                        // Last sync timestamps (each 8 bytes)
                        LastSourceUpdate = DateTimeOffset.FromUnixTimeSeconds(ByteConverter.ToInt64(body, offset + 375)).DateTime,
                        LastSyncUpdate = DateTimeOffset.FromUnixTimeSeconds(ByteConverter.ToInt64(body, offset + 383)).DateTime
                    };

                    // Set IpAddress same as Id for backward compatibility
                    server.IpAddress = server.Id;

                    Servers.Add(server);
                }
                catch (Exception)
                {
                    // If we can't parse a server info block, stop processing
                    // This might happen if the protocol format is different than expected
                    break;
                }
            }
        }
    }
}
