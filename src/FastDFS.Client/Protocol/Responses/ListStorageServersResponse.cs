using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FastDFS.Client.Exceptions;
using FastDFS.Client.Tracker;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Protocol.Responses
{
    /// <summary>
    /// Response for list storage servers operation.
    /// Command: TRACKER_PROTO_CMD_SERVER_LIST_STORAGE (93).
    /// Returns detailed information about storage servers in a group.
    /// </summary>
    public class ListStorageServersResponse : FastDFSResponse
    {
        private static readonly int[] SupportedServerInfoBlockSizes = { 600, 592 };

        /// <summary>
        /// Gets the list of storage server details.
        /// </summary>
        public List<StorageServerDetail> Servers { get; private set; } = new List<StorageServerDetail>();

        /// <summary>
        /// Decodes the response body containing storage server information.
        /// Body format: multiple fixed-size storage server info blocks.
        /// </summary>
        protected override void DecodeBody(byte[]? body)
        {
            if (body == null || body.Length == 0)
            {
                Servers = new List<StorageServerDetail>();
                return;
            }

            int serverInfoBlockSize = GetServerInfoBlockSize(body.Length);
            if (serverInfoBlockSize == 0)
            {
                throw new FastDFSProtocolException(
                    $"Invalid storage server list response length {body.Length.ToString(CultureInfo.InvariantCulture)}. Expected a multiple of {string.Join(" or ", SupportedServerInfoBlockSizes)} bytes.");
            }

            int serverCount = body.Length / serverInfoBlockSize;
            Servers = new List<StorageServerDetail>(serverCount);

            for (int i = 0; i < serverCount; i++)
            {
                int offset = i * serverInfoBlockSize;
                Servers.Add(ParseServer(body, offset, i));
            }
        }

        private static int GetServerInfoBlockSize(int bodyLength)
        {
            foreach (int blockSize in SupportedServerInfoBlockSizes)
            {
                if (bodyLength % blockSize == 0)
                    return blockSize;
            }

            return 0;
        }

        private static StorageServerDetail ParseServer(byte[] body, int offset, int index)
        {
            try
            {
                var joinTime = DateTimeOffset.FromUnixTimeSeconds(ByteConverter.ToInt64(body, offset + 167));
                var lastHeartbeatTime = DateTimeOffset.FromUnixTimeSeconds(ByteConverter.ToInt64(body, offset + 175));
                var lastSourceUpdate = DateTimeOffset.FromUnixTimeSeconds(ByteConverter.ToInt64(body, offset + 375));
                var lastSyncUpdate = DateTimeOffset.FromUnixTimeSeconds(ByteConverter.ToInt64(body, offset + 383));

                var server = new StorageServerDetail
                {
                    Status = (StorageServerStatus)body[offset],
                    Id = body.ReadFixedString(offset + 1, 16, System.Text.Encoding.UTF8).TrimEnd('\0'),
                    SourceIpAddress = body.ReadFixedString(offset + 17, 16, System.Text.Encoding.UTF8).TrimEnd('\0'),
                    DomainName = body.ReadFixedString(offset + 33, 128, System.Text.Encoding.UTF8).TrimEnd('\0'),
                    Version = body.ReadFixedString(offset + 161, 6, System.Text.Encoding.UTF8).TrimEnd('\0'),
                    JoinTime = joinTime.UtcDateTime,
                    LastHeartbeatTime = lastHeartbeatTime.UtcDateTime,
                    TotalMB = ByteConverter.ToInt64(body, offset + 183),
                    FreeMB = ByteConverter.ToInt64(body, offset + 191),
                    UploadPriority = checked((int)ByteConverter.ToInt64(body, offset + 199)),
                    StorePathCount = checked((int)ByteConverter.ToInt64(body, offset + 207)),
                    SubdirCountPerPath = checked((int)ByteConverter.ToInt64(body, offset + 215)),
                    CurrentWritePath = checked((int)ByteConverter.ToInt64(body, offset + 223)),
                    StoragePort = checked((int)ByteConverter.ToInt64(body, offset + 231)),
                    StorageHttpPort = checked((int)ByteConverter.ToInt64(body, offset + 239)),
                    TotalUploadCount = ByteConverter.ToInt64(body, offset + 247),
                    SuccessUploadCount = ByteConverter.ToInt64(body, offset + 255),
                    TotalAppendCount = ByteConverter.ToInt64(body, offset + 263),
                    SuccessAppendCount = ByteConverter.ToInt64(body, offset + 271),
                    TotalModifyCount = ByteConverter.ToInt64(body, offset + 279),
                    SuccessModifyCount = ByteConverter.ToInt64(body, offset + 287),
                    TotalTruncateCount = ByteConverter.ToInt64(body, offset + 295),
                    SuccessTruncateCount = ByteConverter.ToInt64(body, offset + 303),
                    TotalSetMetadataCount = ByteConverter.ToInt64(body, offset + 311),
                    SuccessSetMetadataCount = ByteConverter.ToInt64(body, offset + 319),
                    TotalDeleteCount = ByteConverter.ToInt64(body, offset + 327),
                    SuccessDeleteCount = ByteConverter.ToInt64(body, offset + 335),
                    TotalDownloadCount = ByteConverter.ToInt64(body, offset + 343),
                    SuccessDownloadCount = ByteConverter.ToInt64(body, offset + 351),
                    TotalGetMetadataCount = ByteConverter.ToInt64(body, offset + 359),
                    SuccessGetMetadataCount = ByteConverter.ToInt64(body, offset + 367),
                    LastSourceUpdate = lastSourceUpdate.UtcDateTime,
                    LastSyncUpdate = lastSyncUpdate.UtcDateTime
                };

                server.IpAddress = server.Id;
                return server;
            }
            catch (Exception ex)
            {
                throw new FastDFSProtocolException(
                    $"Failed to parse storage server detail block at index {index.ToString(CultureInfo.InvariantCulture)} (offset {offset.ToString(CultureInfo.InvariantCulture)}).",
                    ex);
            }
        }
    }
}
