using System;
using System.Collections.Generic;
using FastDFS.Client.Protocol.Decoding;
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
                ServerInfos = new List<StorageServerInfo>();
                return;
            }

            ProtocolBlockGuard.EnsureMinimumBodyLength(nameof(QueryFetchAllResponse), body.Length, StorageInfoBlockSize);
            ProtocolBlockGuard.EnsureExactBlockMultiple(nameof(QueryFetchAllResponse), body.Length, StorageInfoBlockSize);
            int storageCount = body.Length / StorageInfoBlockSize;
            ServerInfos = new List<StorageServerInfo>(storageCount);

            for (int i = 0; i < storageCount; i++)
            {
                int offset = i * StorageInfoBlockSize;
                var serverInfo = new StorageServerInfo
                {
                    GroupName = ProtocolFieldReader.ReadFixedUtf8(body, offset, FastDFSConstants.GroupNameMaxLength),
                    IpAddress = ProtocolFieldReader.ReadFixedUtf8(body, offset + FastDFSConstants.GroupNameMaxLength, FastDFSConstants.IpAddressLength - 1).Trim(),
                    Port = (int)ByteConverter.ToInt64(body, offset + 31),
                    StorePathIndex = 0
                };

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
