using System;
using System.Collections.Generic;
using FastDFS.Client.Tracker;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Protocol.Responses
{
    /// <summary>
    /// Response for query all fetch (download) storage servers request.
    /// Returns multiple storage server information for file download.
    /// Response body structure: multiple storage server info blocks
    /// Each block (39 bytes):
    /// - Group name (16 bytes)
    /// - IP address (15 bytes)
    /// - Port (8 bytes, big-endian long)
    /// </summary>
    public class QueryFetchAllResponse : FastDFSResponse
    {
        private const int StorageInfoBlockSize = 39;

        /// <summary>
        /// Gets the list of storage server information.
        /// </summary>
        public List<StorageServerInfo> ServerInfos { get; private set; } = new List<StorageServerInfo>();

        /// <summary>
        /// Decodes the response body.
        /// </summary>
        /// <param name="body">The response body bytes.</param>
        protected override void DecodeBody(byte[]? body)
        {
            if (body == null || body.Length == 0)
            {
                // No storage servers available
                ServerInfos = new List<StorageServerInfo>();
                return;
            }

            if (body.Length % StorageInfoBlockSize != 0)
            {
                throw new ArgumentException($"Invalid response body length. Expected multiple of {StorageInfoBlockSize} bytes, got {body.Length}.");
            }

            int storageCount = body.Length / StorageInfoBlockSize;
            ServerInfos = new List<StorageServerInfo>(storageCount);

            for (int i = 0; i < storageCount; i++)
            {
                int offset = i * StorageInfoBlockSize;
                var serverInfo = new StorageServerInfo();

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

                ServerInfos.Add(serverInfo);
            }
        }

        /// <summary>
        /// Returns a string representation of the response.
        /// </summary>
        public override string ToString()
        {
            return $"QueryFetchAllResponse [StorageCount={ServerInfos.Count}]";
        }
    }
}
