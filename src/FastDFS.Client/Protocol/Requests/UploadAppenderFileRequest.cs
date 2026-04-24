using System;
using FastDFS.Client.Domain.Identifiers;
using FastDFS.Client.Protocol.Encoding;
using FastDFS.Client.Protocol.Responses;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Protocol.Requests
{
    /// <summary>
    /// Request to upload an appender file to storage server.
    /// Appender files can have data appended to them after upload.
    /// Command: STORAGE_PROTO_CMD_UPLOAD_APPENDER_FILE (23)
    /// Request body structure is same as UploadFileRequest:
    /// - Store path index (1 byte)
    /// - File size (8 bytes, big-endian long)
    /// - File extension name (6 bytes, including dot, e.g., ".log\0\0")
    /// - File content (variable length)
    /// </summary>
    public class UploadAppenderFileRequest : FastDFSRequest<UploadFileResponse>
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
        /// Gets or sets the file extension (e.g., "log", ".log").
        /// </summary>
        public string FileExtension { get; set; } = string.Empty;

        /// <summary>
        /// Gets the command code for this request.
        /// </summary>
        public override byte Command => StorageCommand.UploadAppenderFile;

        /// <summary>
        /// Encodes the request body.
        /// </summary>
        /// <returns>The encoded body bytes.</returns>
        protected override byte[]? EncodeBody()
        {
            if (FileContent == null || FileContent.Length == 0)
                throw new ArgumentException("File content cannot be null or empty.", nameof(FileContent));

            var extension = Domain.Identifiers.FileExtension.Create(this.FileExtension).Value;

            var headerSize = 1 + 8 + FastDFSConstants.FileExtNameMaxLength;
            var body = new byte[headerSize + FileContent.Length];

            int offset = 0;

            body[offset] = StorePathIndex;
            offset += 1;

            ByteConverter.WriteInt64(FileContent.Length, body, offset);
            offset += 8;

            ProtocolFieldWriter.WriteFixedUtf8(body, offset, FastDFSConstants.FileExtNameMaxLength, "fileExtension", extension);
            offset += FastDFSConstants.FileExtNameMaxLength;

            Array.Copy(FileContent, 0, body, offset, FileContent.Length);

            return body;
        }
    }
}
