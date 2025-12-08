using System;
using FastDFS.Client.Protocol.Responses;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Protocol.Requests
{
    /// <summary>
    /// Request to query a storage server for upload with specified group.
    /// Command: TRACKER_PROTO_CMD_SERVICE_QUERY_STORE_WITH_GROUP_ONE (104)
    /// Request body structure:
    /// - Group name (16 bytes)
    /// </summary>
    public class QueryStoreWithGroupRequest : FastDFSRequest<QueryStoreResponse>
    {
        /// <summary>
        /// Gets or sets the group name.
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Gets the command code for this request.
        /// </summary>
        public override byte Command => TrackerCommand.QueryStoreWithGroupOne;

        /// <summary>
        /// Encodes the request body.
        /// </summary>
        /// <returns>The encoded body bytes.</returns>
        protected override byte[]? EncodeBody()
        {
            if (string.IsNullOrEmpty(GroupName))
                throw new ArgumentException("Group name cannot be null or empty.", nameof(GroupName));

            var body = new byte[FastDFSConstants.GroupNameMaxLength];

            // Group name (16 bytes, fixed length)
            ByteExtensions.CopyFixedString(GroupName, body, 0, FastDFSConstants.GroupNameMaxLength);

            return body;
        }
    }
}
