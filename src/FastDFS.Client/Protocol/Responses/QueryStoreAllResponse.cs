using System;
using System.Collections.Generic;
using FastDFS.Client.Tracker;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Protocol.Responses
{
    /// <summary>
    /// Response for query all storage servers request.
    /// Returns multiple storage server information for file upload.
    /// Response body structure: multiple storage server info blocks
    /// Each block (40 bytes):
    /// - Group name (16 bytes, null-terminated)
    /// - IP address (15 bytes, null-terminated)
    /// - Port (8 bytes, big-endian long)
    /// - Store path index (1 byte)
    /// </summary>
    public class QueryStoreAllResponse : FastDFSResponse
    {
        private const int StorageInfoBlockSize = 40;

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

                // IP address (15 bytes, but buffer has 16 for alignment)
                serverInfo.IpAddress = ByteExtensions.ReadFixedString(body, offset, FastDFSConstants.IpAddressLength - 1).Trim();
                offset += FastDFSConstants.IpAddressLength - 1;

                // Port (8 bytes, big-endian long)
                long portLong = ByteConverter.ToInt64(body, offset);
                serverInfo.Port = (int)portLong;
                offset += 8;

                // Store path index (1 byte)
                serverInfo.StorePathIndex = body[offset];

                ServerInfos.Add(serverInfo);
            }
        }

        /// <summary>
        /// Returns a string representation of the response.
        /// </summary>
        public override string ToString()
        {
            return $"QueryStoreAllResponse [StorageCount={ServerInfos.Count}]";
        }
    }
}
