using System;
using FastDFS.Client.Tracker;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Protocol.Responses
{
    /// <summary>
    /// Response for query fetch (download) storage server request.
    /// Returns storage server information for file download.
    /// Response body structure (total: 16 + 15 + 8 = 39 bytes minimum):
    /// - Group name (16 bytes)
    /// - IP address (15 bytes)
    /// - Port (8 bytes, big-endian long)
    /// </summary>
    public class QueryFetchResponse : FastDFSResponse
    {
        private const int ResponseBodyLength = 39;

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

            // IP address (15 bytes)
            serverInfo.IpAddress = ByteExtensions.ReadFixedString(body, offset, FastDFSConstants.IpAddressLength - 1).Trim();
            offset += FastDFSConstants.IpAddressLength - 1;

            // Port (8 bytes, big-endian long)
            long portLong = ByteConverter.ToInt64(body, offset);
            serverInfo.Port = (int)portLong;

            // Store path index is not returned in fetch query, set to 0
            serverInfo.StorePathIndex = 0;

            ServerInfo = serverInfo;
        }
    }
}
