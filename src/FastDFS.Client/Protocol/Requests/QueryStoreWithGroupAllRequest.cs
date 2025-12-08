using System;
using System.Text;
using FastDFS.Client.Protocol.Responses;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Protocol.Requests
{
    /// <summary>
    /// Request to query all available storage servers for upload with specified group.
    /// The tracker will return all available storage servers in the group.
    /// Command: TRACKER_PROTO_CMD_SERVICE_QUERY_STORE_WITH_GROUP_ALL (106)
    /// Request body structure:
    /// - Group name (16 bytes)
    /// </summary>
    public class QueryStoreWithGroupAllRequest : FastDFSRequest<QueryStoreAllResponse>
    {
        /// <summary>
        /// Gets or sets the group name.
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Gets the command code for this request.
        /// </summary>
        public override byte Command => TrackerCommand.QueryStoreWithGroupAll;

        /// <summary>
        /// Encodes the request body.
        /// </summary>
        /// <returns>The encoded body bytes.</returns>
        protected override byte[]? EncodeBody()
        {
            if (string.IsNullOrEmpty(GroupName))
                throw new ArgumentException("Group name cannot be null or empty.", nameof(GroupName));

            var body = new byte[FastDFSConstants.GroupNameMaxLength];
            ByteExtensions.CopyFixedString(GroupName, body, 0, FastDFSConstants.GroupNameMaxLength);
            return body;
        }
    }
}
