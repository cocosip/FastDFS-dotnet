using System;
using System.Text;
using FastDFS.Client.Protocol.Responses;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Protocol.Requests
{
    /// <summary>
    /// Request to append data to an existing appender file.
    /// Command: STORAGE_PROTO_CMD_APPEND_FILE (24)
    /// Request body structure:
    /// - Appender file name length (8 bytes, big-endian long)
    /// - File size to append (8 bytes, big-endian long)
    /// - Appender file name (variable length)
    /// - File content to append (variable length)
    /// </summary>
    public class AppendFileRequest : FastDFSRequest<AppendFileResponse>
    {
        /// <summary>
        /// Gets or sets the appender file name (path on storage server).
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the content to append.
        /// </summary>
        public byte[] Content { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Gets the command code for this request.
        /// </summary>
        public override byte Command => StorageCommand.AppendFile;

        /// <summary>
        /// Encodes the request body.
        /// </summary>
        /// <returns>The encoded body bytes.</returns>
        protected override byte[]? EncodeBody()
        {
            if (string.IsNullOrEmpty(FileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(FileName));
            if (Content == null || Content.Length == 0)
                throw new ArgumentException("Content cannot be null or empty.", nameof(Content));

            var fileNameBytes = Encoding.UTF8.GetBytes(FileName);
            var bodyLength = 8 + 8 + fileNameBytes.Length + Content.Length;
            var body = new byte[bodyLength];

            int offset = 0;

            // Appender file name length (8 bytes)
            ByteConverter.WriteInt64(fileNameBytes.Length, body, offset);
            offset += 8;

            // File size to append (8 bytes)
            ByteConverter.WriteInt64(Content.Length, body, offset);
            offset += 8;

            // Appender file name
            Array.Copy(fileNameBytes, 0, body, offset, fileNameBytes.Length);
            offset += fileNameBytes.Length;

            // Content to append
            Array.Copy(Content, 0, body, offset, Content.Length);

            return body;
        }
    }
}
