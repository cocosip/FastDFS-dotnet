using System;
using System.Collections.Generic;
using System.Text;
using FastDFS.Client.Tracker;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Protocol.Responses
{
    /// <summary>
    /// Response for list all groups operation.
    /// Command: TRACKER_PROTO_CMD_SERVER_LIST_ALL_GROUPS (91).
    /// Returns information about all storage groups in the cluster.
    /// </summary>
    public class ListAllGroupsResponse : FastDFSResponse
    {
        // Each group info block is 105 bytes
        private const int GroupInfoBlockSize = 105;

        /// <summary>
        /// Gets the list of group information.
        /// </summary>
        public List<GroupInfo> Groups { get; private set; } = new List<GroupInfo>();

        /// <summary>
        /// Decodes the response body containing all group information.
        /// Body format: multiple fixed-size group info blocks (105 bytes each).
        /// </summary>
        protected override void DecodeBody(byte[]? body)
        {
            if (body == null || body.Length == 0)
            {
                Groups = new List<GroupInfo>();
                return;
            }

            // Calculate number of groups
            int groupCount = body.Length / GroupInfoBlockSize;
            Groups = new List<GroupInfo>(groupCount);

            for (int i = 0; i < groupCount; i++)
            {
                int offset = i * GroupInfoBlockSize;

                var groupInfo = new GroupInfo
                {
                    // Group name (16 bytes, fixed length)
                    GroupName = body.ReadFixedString(offset, 16, Encoding.UTF8).TrimEnd('\0'),

                    // Total disk space (8 bytes, MB)
                    TotalMB = ByteConverter.ToInt64(body, offset + 16),

                    // Free disk space (8 bytes, MB)
                    FreeMB = ByteConverter.ToInt64(body, offset + 24),

                    // Trunk free space (8 bytes, MB)
                    TrunkFreeMB = ByteConverter.ToInt64(body, offset + 32),

                    // Storage server count (8 bytes)
                    StorageServerCount = (int)ByteConverter.ToInt64(body, offset + 40),

                    // Storage port (8 bytes)
                    StoragePort = (int)ByteConverter.ToInt64(body, offset + 48),

                    // Storage HTTP port (8 bytes)
                    StorageHttpPort = (int)ByteConverter.ToInt64(body, offset + 56),

                    // Active server count (8 bytes)
                    ActiveServerCount = (int)ByteConverter.ToInt64(body, offset + 64),

                    // Current write server index (8 bytes)
                    CurrentWriteServer = (int)ByteConverter.ToInt64(body, offset + 72),

                    // Store path count (8 bytes)
                    StorePathCount = (int)ByteConverter.ToInt64(body, offset + 80),

                    // Subdir count per path (8 bytes)
                    SubdirCountPerPath = (int)ByteConverter.ToInt64(body, offset + 88),

                    // Current trunk file ID (8 bytes)
                    CurrentTrunkFileId = (int)ByteConverter.ToInt64(body, offset + 96)

                    // Last byte (offset + 104) is reserved/padding
                };

                Groups.Add(groupInfo);
            }
        }
    }
}
