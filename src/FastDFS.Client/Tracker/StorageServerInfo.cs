using System;
using System.Net;

namespace FastDFS.Client.Tracker
{
    /// <summary>
    /// Storage server information returned by Tracker.
    /// </summary>
    public class StorageServerInfo
    {
        /// <summary>
        /// Gets or sets the group name.
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the storage server IP address.
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the storage server port.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the store path index on the storage server.
        /// </summary>
        public byte StorePathIndex { get; set; }

        /// <summary>
        /// Gets the endpoint of the storage server.
        /// </summary>
        public IPEndPoint GetEndPoint()
        {
            if (!IPAddress.TryParse(IpAddress, out var ipAddr))
            {
                throw new InvalidOperationException($"Invalid IP address: {IpAddress}");
            }

            return new IPEndPoint(ipAddr, Port);
        }

        /// <summary>
        /// Returns a string representation of the storage server info.
        /// </summary>
        public override string ToString()
        {
            return $"StorageServer [Group={GroupName}, IP={IpAddress}, Port={Port}, StorePathIndex={StorePathIndex}]";
        }
    }
}
