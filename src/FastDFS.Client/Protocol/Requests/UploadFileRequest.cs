using System;
using System.Text;
using FastDFS.Client.Protocol.Responses;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Protocol.Requests
{
    /// <summary>
    /// Request to upload a file to storage server.
    /// Command: STORAGE_PROTO_CMD_UPLOAD_FILE (11)
    /// Request body structure:
    /// - Store path index (1 byte)
    /// - File size (8 bytes, big-endian long)
    /// - File extension name (6 bytes, including dot, e.g., ".jpg\0\0")
    /// - File content (variable length)
    /// </summary>
    public class UploadFileRequest : FastDFSRequest<UploadFileResponse>
    {
        /// <summary>
        /// Gets or sets the store path index (255 = auto).
        /// </summary>
        public byte StorePathIndex { get; set; } = FastDFSConstants.StorePathIndexAuto;

        /// <summary>
        /// Gets or sets the file content.
        /// </summary>
        public byte[] FileContent { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Gets or sets the file extension (e.g., "jpg", ".jpg").
        /// </summary>
        public string FileExtension { get; set; } = string.Empty;

        /// <summary>
        /// Gets the command code for this request.
        /// </summary>
        public override byte Command => StorageCommand.UploadFile;

        /// <summary>
        /// Encodes the request body.
        /// </summary>
        /// <returns>The encoded body bytes.</returns>
        protected override byte[]? EncodeBody()
        {
            if (FileContent == null || FileContent.Length == 0)
                throw new ArgumentException("File content cannot be null or empty.", nameof(FileContent));

            // Normalize extension (remove leading dot if present)
            var extension = FileExtension;
            if (!string.IsNullOrEmpty(extension) && extension.StartsWith("."))
            {
                extension = extension.Substring(1);
            }

            // Header: 1 + 8 + 6 = 15 bytes
            var headerSize = 1 + 8 + FastDFSConstants.FileExtNameMaxLength;
            var body = new byte[headerSize + FileContent.Length];

            int offset = 0;

            // Store path index (1 byte)
            body[offset] = StorePathIndex;
            offset += 1;

            // File size (8 bytes)
            ByteConverter.WriteInt64(FileContent.Length, body, offset);
            offset += 8;

            // File extension (6 bytes, fixed length)
            ByteExtensions.CopyFixedString(extension, body, offset, FastDFSConstants.FileExtNameMaxLength);
            offset += FastDFSConstants.FileExtNameMaxLength;

            // File content
            Array.Copy(FileContent, 0, body, offset, FileContent.Length);

            return body;
        }
    }
}
