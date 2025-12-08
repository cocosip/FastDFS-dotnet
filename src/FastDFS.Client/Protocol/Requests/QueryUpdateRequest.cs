using System;
using System.Text;
using FastDFS.Client.Protocol.Responses;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Protocol.Requests
{
    /// <summary>
    /// Request to query a storage server for update operations (delete, set metadata, etc.).
    /// Command: TRACKER_PROTO_CMD_SERVICE_QUERY_UPDATE (103)
    /// Request body structure:
    /// - Group name (16 bytes)
    /// - File name (variable length)
    /// </summary>
    public class QueryUpdateRequest : FastDFSRequest<QueryFetchResponse>
    {
        /// <summary>
        /// Gets or sets the group name.
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file name (path on storage server).
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets the command code for this request.
        /// </summary>
        public override byte Command => TrackerCommand.QueryUpdate;

        /// <summary>
        /// Encodes the request body.
        /// </summary>
        /// <returns>The encoded body bytes.</returns>
        protected override byte[]? EncodeBody()
        {
            if (string.IsNullOrEmpty(GroupName))
                throw new ArgumentException("Group name cannot be null or empty.", nameof(GroupName));
            if (string.IsNullOrEmpty(FileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(FileName));

            var fileNameBytes = Encoding.UTF8.GetBytes(FileName);
            var bodyLength = FastDFSConstants.GroupNameMaxLength + fileNameBytes.Length;
            var body = new byte[bodyLength];

            int offset = 0;

            // Group name (16 bytes, fixed length)
            ByteExtensions.CopyFixedString(GroupName, body, offset, FastDFSConstants.GroupNameMaxLength);
            offset += FastDFSConstants.GroupNameMaxLength;

            // File name (variable length)
            Array.Copy(fileNameBytes, 0, body, offset, fileNameBytes.Length);

            return body;
        }
    }
}
