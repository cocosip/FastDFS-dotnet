using System;
using FastDFS.Client.Domain.Identifiers;
using FastDFS.Client.Protocol.Encoding;
using FastDFS.Client.Protocol.Responses;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Protocol.Requests
{
    /// <summary>
    /// Request to query a storage server for download.
    /// Command: TRACKER_PROTO_CMD_SERVICE_QUERY_FETCH_ONE (102)
    /// Request body structure:
    /// - Group name (16 bytes)
    /// - File name (variable length)
    /// </summary>
    public class QueryFetchRequest : FastDFSRequest<QueryFetchResponse>
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
        public override byte Command => TrackerCommand.QueryFetchOne;

        /// <summary>
        /// Encodes the request body.
        /// </summary>
        /// <returns>The encoded body bytes.</returns>
        protected override byte[]? EncodeBody()
        {
            if (string.IsNullOrEmpty(FileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(FileName));

            var groupName = Domain.Identifiers.GroupName.Create(this.GroupName).Value;
            var fileNameBytes = System.Text.Encoding.UTF8.GetBytes(FileName);
            var bodyLength = FastDFSConstants.GroupNameMaxLength + fileNameBytes.Length;
            var body = new byte[bodyLength];

            ProtocolFieldWriter.WriteFixedUtf8(body, 0, FastDFSConstants.GroupNameMaxLength, "groupName", groupName);

            // File name (variable length)
            Array.Copy(fileNameBytes, 0, body, FastDFSConstants.GroupNameMaxLength, fileNameBytes.Length);

            return body;
        }
    }
}
