using System;
using System.Text;
using FastDFS.Client.Protocol.Responses;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Protocol.Requests
{
    /// <summary>
    /// Request to download a file from storage server.
    /// Command: STORAGE_PROTO_CMD_DOWNLOAD_FILE (14)
    /// Request body structure:
    /// - File offset (8 bytes, big-endian long)
    /// - Download bytes (8 bytes, big-endian long, 0 = download entire file)
    /// - Group name (16 bytes)
    /// - File name (variable length)
    /// </summary>
    public class DownloadFileRequest : FastDFSRequest<DownloadFileResponse>
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
        /// Gets or sets the offset in the file to start downloading from.
        /// </summary>
        public long FileOffset { get; set; } = 0;

        /// <summary>
        /// Gets or sets the number of bytes to download (0 = entire file from offset).
        /// </summary>
        public long DownloadBytes { get; set; } = 0;

        /// <summary>
        /// Gets the command code for this request.
        /// </summary>
        public override byte Command => StorageCommand.DownloadFile;

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
            var bodyLength = 8 + 8 + FastDFSConstants.GroupNameMaxLength + fileNameBytes.Length;
            var body = new byte[bodyLength];

            int offset = 0;

            // File offset (8 bytes)
            ByteConverter.WriteInt64(FileOffset, body, offset);
            offset += 8;

            // Download bytes (8 bytes)
            ByteConverter.WriteInt64(DownloadBytes, body, offset);
            offset += 8;

            // Group name (16 bytes, fixed length)
            ByteExtensions.CopyFixedString(GroupName, body, offset, FastDFSConstants.GroupNameMaxLength);
            offset += FastDFSConstants.GroupNameMaxLength;

            // File name (variable length)
            Array.Copy(fileNameBytes, 0, body, offset, fileNameBytes.Length);

            return body;
        }
    }
}
