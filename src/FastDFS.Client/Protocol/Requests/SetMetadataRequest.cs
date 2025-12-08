using System;
using System.Text;
using FastDFS.Client.Protocol.Responses;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Protocol.Requests
{
    /// <summary>
    /// Request to set file metadata.
    /// Command: STORAGE_PROTO_CMD_SET_METADATA (13).
    /// </summary>
    public class SetMetadataRequest : FastDFSRequest<SetMetadataResponse>
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
        /// Gets or sets the metadata to set.
        /// </summary>
        public FastDFSMetadata Metadata { get; set; } = new FastDFSMetadata();

        /// <summary>
        /// Gets or sets the metadata operation flag.
        /// </summary>
        public MetadataFlag Flag { get; set; } = MetadataFlag.Overwrite;

        /// <summary>
        /// Gets the command code for set metadata operation.
        /// </summary>
        public override byte Command => StorageCommand.SetMetadata;

        /// <summary>
        /// Encodes the request body.
        /// Body format:
        /// - FileName length (8 bytes)
        /// - Metadata length (8 bytes)
        /// - Flag (1 byte): 'O' = overwrite, 'M' = merge
        /// - GroupName (16 bytes, fixed length, padded with \0)
        /// - FileName (variable length)
        /// - Metadata (variable length, format: key1\x02value1\x01key2\x02value2\x01...)
        /// </summary>
        protected override byte[]? EncodeBody()
        {
            if (string.IsNullOrWhiteSpace(GroupName))
                throw new ArgumentException("GroupName is required.", nameof(GroupName));

            if (string.IsNullOrWhiteSpace(FileName))
                throw new ArgumentException("FileName is required.", nameof(FileName));

            var fileNameBytes = Encoding.UTF8.GetBytes(FileName);
            var metadataBytes = Metadata.Encode();

            var bodyLength = 8 + 8 + 1 + GroupNameLength + fileNameBytes.Length + metadataBytes.Length;
            var body = new byte[bodyLength];

            int offset = 0;

            // FileName length (8 bytes)
            ByteConverter.WriteInt64(fileNameBytes.Length, body, offset);
            offset += 8;

            // Metadata length (8 bytes)
            ByteConverter.WriteInt64(metadataBytes.Length, body, offset);
            offset += 8;

            // Flag (1 byte)
            body[offset] = (byte)Flag;
            offset += 1;

            // GroupName (16 bytes, fixed length, padded with \0)
            var groupNameBytes = Encoding.UTF8.GetBytes(GroupName);
            Array.Copy(groupNameBytes, 0, body, offset, Math.Min(groupNameBytes.Length, GroupNameLength));
            offset += GroupNameLength;

            // FileName (variable length)
            Array.Copy(fileNameBytes, 0, body, offset, fileNameBytes.Length);
            offset += fileNameBytes.Length;

            // Metadata (variable length)
            if (metadataBytes.Length > 0)
            {
                Array.Copy(metadataBytes, 0, body, offset, metadataBytes.Length);
            }

            return body;
        }
    }
}
