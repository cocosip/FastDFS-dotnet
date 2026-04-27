using System;
using System.Collections.Generic;
using FastDFS.Client.Protocol.Decoding;
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

            ProtocolBlockGuard.EnsureMinimumBodyLength(nameof(ListAllGroupsResponse), body.Length, GroupInfoBlockSize);
            ProtocolBlockGuard.EnsureExactBlockMultiple(nameof(ListAllGroupsResponse), body.Length, GroupInfoBlockSize);
            int groupCount = body.Length / GroupInfoBlockSize;
            Groups = new List<GroupInfo>(groupCount);

            for (int i = 0; i < groupCount; i++)
            {
                int offset = i * GroupInfoBlockSize;

                var groupInfo = new GroupInfo
                {
                    GroupName = ProtocolFieldReader.ReadFixedUtf8(body, offset, 16),
                    TotalMB = ByteConverter.ToInt64(body, offset + 16),
                    FreeMB = ByteConverter.ToInt64(body, offset + 24),
                    TrunkFreeMB = ByteConverter.ToInt64(body, offset + 32),
                    StorageServerCount = (int)ByteConverter.ToInt64(body, offset + 40),
                    StoragePort = (int)ByteConverter.ToInt64(body, offset + 48),
                    StorageHttpPort = (int)ByteConverter.ToInt64(body, offset + 56),
                    ActiveServerCount = (int)ByteConverter.ToInt64(body, offset + 64),
                    CurrentWriteServer = (int)ByteConverter.ToInt64(body, offset + 72),
                    StorePathCount = (int)ByteConverter.ToInt64(body, offset + 80),
                    SubdirCountPerPath = (int)ByteConverter.ToInt64(body, offset + 88),
                    CurrentTrunkFileId = (int)ByteConverter.ToInt64(body, offset + 96)
                };

                Groups.Add(groupInfo);
            }
        }
    }
}
