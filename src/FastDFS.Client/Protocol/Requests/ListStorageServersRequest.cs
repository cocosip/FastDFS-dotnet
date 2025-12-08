using System;
using System.Text;
using FastDFS.Client.Protocol.Responses;

namespace FastDFS.Client.Protocol.Requests
{
    /// <summary>
    /// Request to list all storage servers in a specific group.
    /// Command: TRACKER_PROTO_CMD_SERVER_LIST_STORAGE (93).
    /// </summary>
    public class ListStorageServersRequest : FastDFSRequest<ListStorageServersResponse>
    {
        private const int GroupNameLength = 16;

        /// <summary>
        /// Gets or sets the group name to query.
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional storage server ID (IP address) to query specific server.
        /// If null or empty, returns all servers in the group.
        /// </summary>
        public string? StorageServerId { get; set; }

        /// <summary>
        /// Gets the command code for list storage servers operation.
        /// </summary>
        public override byte Command => TrackerCommand.ListStorageServers;

        /// <summary>
        /// Encodes the request body.
        /// Body format:
        /// - GroupName (16 bytes, fixed length, padded with \0)
        /// - StorageServerId (16 bytes, optional, padded with \0)
        /// </summary>
        protected override byte[]? EncodeBody()
        {
            if (string.IsNullOrWhiteSpace(GroupName))
                throw new ArgumentException("GroupName is required.", nameof(GroupName));

            // Body can be 16 bytes (group only) or 32 bytes (group + server ID)
            var bodyLength = string.IsNullOrWhiteSpace(StorageServerId) ? GroupNameLength : GroupNameLength * 2;
            var body = new byte[bodyLength];

            int offset = 0;

            // GroupName (16 bytes, fixed length, padded with \0)
            var groupNameBytes = Encoding.UTF8.GetBytes(GroupName);
            Array.Copy(groupNameBytes, 0, body, offset, Math.Min(groupNameBytes.Length, GroupNameLength));
            offset += GroupNameLength;

            // StorageServerId (16 bytes, optional)
            if (!string.IsNullOrWhiteSpace(StorageServerId))
            {
                var serverIdBytes = Encoding.UTF8.GetBytes(StorageServerId);
                Array.Copy(serverIdBytes, 0, body, offset, Math.Min(serverIdBytes.Length, GroupNameLength));
            }

            return body;
        }
    }
}
