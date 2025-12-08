using System;
using FastDFS.Client.Storage;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Protocol.Responses
{
    /// <summary>
    /// Response for query file info request.
    /// Returns file information including size, creation time, CRC32, and source IP.
    /// Response body structure (total: 8 + 8 + 8 + 16 = 40 bytes):
    /// - File size (8 bytes, big-endian long)
    /// - Create timestamp (8 bytes, big-endian long, Unix timestamp)
    /// - CRC32 (8 bytes, big-endian long)
    /// - Source IP address (16 bytes, null-terminated string)
    /// </summary>
    public class QueryFileInfoResponse : FastDFSResponse
    {
        private const int ResponseBodyLength = 40;

        /// <summary>
        /// Gets the file information.
        /// </summary>
        public FastDFSFileInfo? FileInfo { get; private set; }

        /// <summary>
        /// Decodes the response body.
        /// </summary>
        /// <param name="body">The response body bytes.</param>
        protected override void DecodeBody(byte[]? body)
        {
            if (body == null || body.Length < ResponseBodyLength)
            {
                throw new ArgumentException($"Invalid response body length. Expected {ResponseBodyLength} bytes, got {body?.Length ?? 0}.");
            }

            var fileInfo = new FastDFSFileInfo();

            int offset = 0;

            // File size (8 bytes)
            fileInfo.FileSize = ByteConverter.ToInt64(body, offset);
            offset += 8;

            // Create timestamp (8 bytes)
            fileInfo.CreateTimestamp = ByteConverter.ToInt64(body, offset);
            offset += 8;

            // CRC32 (8 bytes)
            fileInfo.Crc32 = ByteConverter.ToInt64(body, offset);
            offset += 8;

            // Source IP address (16 bytes)
            fileInfo.SourceIpAddress = ByteExtensions.ReadFixedString(body, offset, FastDFSConstants.IpAddressLength).Trim();

            FileInfo = fileInfo;
        }

        /// <summary>
        /// Returns a string representation of the query file info response.
        /// </summary>
        public override string ToString()
        {
            return $"QueryFileInfoResponse [{FileInfo}]";
        }
    }
}
