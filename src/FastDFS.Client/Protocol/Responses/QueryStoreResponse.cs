using System;
using FastDFS.Client.Tracker;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Protocol.Responses
{
    /// <summary>
    /// Response for query storage server request.
    /// Returns storage server information for file upload.
    /// Response body structure (total: 16 + 1 + 15 + 1 = 33 bytes if IPv4):
    /// - Group name (16 bytes, null-terminated)
    /// - IP address (15 bytes, null-terminated) or (16 bytes for compatibility)
    /// - Port (8 bytes, big-endian long)
    /// - Store path index (1 byte)
    ///
    /// Actual response varies: some versions return TRACKER_QUERY_STORAGE_STORE_BODY_LEN =
    /// FDFS_GROUP_NAME_MAX_LEN + IP_ADDRESS_SIZE - 1 + FDFS_PROTO_PKG_LEN_SIZE + 1
    /// = 16 + 15 + 8 + 1 = 40 bytes (with padding)
    /// </summary>
    public class QueryStoreResponse : FastDFSResponse
    {
        private const int ResponseBodyLength = 40; // Standard response size

        /// <summary>
        /// Gets the storage server information.
        /// </summary>
        public StorageServerInfo? ServerInfo { get; private set; }

        /// <summary>
        /// Decodes the response body.
        /// </summary>
        /// <param name="body">The response body bytes.</param>
        protected override void DecodeBody(byte[]? body)
        {
            if (body == null || body.Length < ResponseBodyLength)
            {
                throw new ArgumentException($"Invalid response body length. Expected at least {ResponseBodyLength} bytes, got {body?.Length ?? 0}.");
            }

            var serverInfo = new StorageServerInfo();

            int offset = 0;

            // Group name (16 bytes)
            serverInfo.GroupName = ByteExtensions.ReadFixedString(body, offset, FastDFSConstants.GroupNameMaxLength);
            offset += FastDFSConstants.GroupNameMaxLength;

            // IP address (15 bytes, but buffer has 16 for alignment)
            serverInfo.IpAddress = ByteExtensions.ReadFixedString(body, offset, FastDFSConstants.IpAddressLength - 1).Trim();
            offset += FastDFSConstants.IpAddressLength - 1;

            // Port (8 bytes, big-endian long)
            long portLong = ByteConverter.ToInt64(body, offset);
            serverInfo.Port = (int)portLong;
            offset += 8;

            // Store path index (1 byte)
            serverInfo.StorePathIndex = body[offset];

            ServerInfo = serverInfo;
        }
    }
}
