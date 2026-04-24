using System;
using FastDFS.Client.Protocol.Encoding;
using FastDFS.Client.Protocol.Responses;

namespace FastDFS.Client.Protocol.Requests
{
    /// <summary>
    /// Request to list all storage servers in a specific group.
    /// Command: TRACKER_PROTO_CMD_SERVER_LIST_STORAGE (93).
    /// </summary>
    public class ListStorageServersRequest : FastDFSRequest<ListStorageServersResponse>
    {
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
            var groupName = Domain.Identifiers.GroupName.Create(this.GroupName).Value;
            var hasStorageServerId = !string.IsNullOrWhiteSpace(StorageServerId);
            var bodyLength = hasStorageServerId
                ? FastDFSConstants.GroupNameMaxLength + FastDFSConstants.StorageIdMaxLength
                : FastDFSConstants.GroupNameMaxLength;
            var body = new byte[bodyLength];

            ProtocolFieldWriter.WriteFixedUtf8(body, 0, FastDFSConstants.GroupNameMaxLength, "groupName", groupName);

            if (hasStorageServerId)
            {
                var storageServerId = Domain.Identifiers.StorageServerId.Create(StorageServerId!).Value;
                ProtocolFieldWriter.WriteFixedUtf8(
                    body,
                    FastDFSConstants.GroupNameMaxLength,
                    FastDFSConstants.StorageIdMaxLength,
                    "storageServerId",
                    storageServerId);
            }

            return body;
        }
    }
}
