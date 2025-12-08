using System;

namespace FastDFS.Client.Protocol
{
    /// <summary>
    /// Base class for FastDFS protocol packets.
    /// A packet consists of a header (10 bytes) and a body (variable length).
    /// </summary>
    public abstract class FastDFSPacket
    {
        /// <summary>
        /// Gets the packet header.
        /// </summary>
        public FastDFSHeader Header { get; protected set; }

        /// <summary>
        /// Gets the packet body.
        /// </summary>
        public byte[]? Body { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSPacket"/> class.
        /// </summary>
        protected FastDFSPacket()
        {
            Header = new FastDFSHeader();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSPacket"/> class with a header.
        /// </summary>
        /// <param name="header">The packet header.</param>
        protected FastDFSPacket(FastDFSHeader header)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
        }

        /// <summary>
        /// Gets the total packet size (header + body).
        /// </summary>
        public long TotalSize => FastDFSHeader.HeaderSize + (Body?.Length ?? 0);

        /// <summary>
        /// Converts the packet to a complete byte array (header + body).
        /// </summary>
        /// <returns>The byte array representation of the packet.</returns>
        public virtual byte[] ToBytes()
        {
            var headerBytes = Header.ToBytes();

            if (Body == null || Body.Length == 0)
            {
                return headerBytes;
            }

            var packet = new byte[FastDFSHeader.HeaderSize + Body.Length];
            Array.Copy(headerBytes, 0, packet, 0, FastDFSHeader.HeaderSize);
            Array.Copy(Body, 0, packet, FastDFSHeader.HeaderSize, Body.Length);

            return packet;
        }

        /// <summary>
        /// Returns a string representation of the packet.
        /// </summary>
        public override string ToString()
        {
            return $"{GetType().Name} [Header={Header}, BodyLength={Body?.Length ?? 0}]";
        }
    }
}
