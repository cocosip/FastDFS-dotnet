using System;
using System.Text;
using FastDFS.Client.Protocol.Responses;

namespace FastDFS.Client.Protocol.Requests
{
    /// <summary>
    /// Request to get file metadata.
    /// Command: STORAGE_PROTO_CMD_GET_METADATA (15).
    /// </summary>
    public class GetMetadataRequest : FastDFSRequest<GetMetadataResponse>
    {
        private const int GroupNameLength = 16;

        /// <summary>
        /// Gets or sets the storage group name.
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file name (path on storage server).
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets the command code for get metadata operation.
        /// </summary>
        public override byte Command => StorageCommand.GetMetadata;

        /// <summary>
        /// Encodes the request body.
        /// Body format:
        /// - GroupName (16 bytes, fixed length, padded with \0)
        /// - FileName (variable length)
        /// </summary>
        protected override byte[]? EncodeBody()
        {
            if (string.IsNullOrWhiteSpace(GroupName))
                throw new ArgumentException("GroupName is required.", nameof(GroupName));

            if (string.IsNullOrWhiteSpace(FileName))
                throw new ArgumentException("FileName is required.", nameof(FileName));

            var fileNameBytes = Encoding.UTF8.GetBytes(FileName);
            var bodyLength = GroupNameLength + fileNameBytes.Length;
            var body = new byte[bodyLength];

            int offset = 0;

            // GroupName (16 bytes, fixed length, padded with \0)
            var groupNameBytes = Encoding.UTF8.GetBytes(GroupName);
            Array.Copy(groupNameBytes, 0, body, offset, Math.Min(groupNameBytes.Length, GroupNameLength));
            offset += GroupNameLength;

            // FileName (variable length)
            Array.Copy(fileNameBytes, 0, body, offset, fileNameBytes.Length);

            return body;
        }
    }
}
