using System;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Protocol
{
    /// <summary>
    /// FastDFS protocol header (10 bytes).
    /// Structure:
    /// - Bytes 0-7: Body length (8 bytes, big-endian long)
    /// - Byte 8: Command code (1 byte)
    /// - Byte 9: Status code (1 byte, 0 = success)
    /// </summary>
    public class FastDFSHeader
    {
        /// <summary>
        /// The fixed size of FastDFS header in bytes.
        /// </summary>
        public const int HeaderSize = 10;

        /// <summary>
        /// Gets or sets the length of the packet body.
        /// </summary>
        public long BodyLength { get; set; }

        /// <summary>
        /// Gets or sets the command code.
        /// </summary>
        public byte Command { get; set; }

        /// <summary>
        /// Gets or sets the status code (0 = success).
        /// </summary>
        public byte Status { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSHeader"/> class.
        /// </summary>
        public FastDFSHeader()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSHeader"/> class.
        /// </summary>
        /// <param name="bodyLength">The body length.</param>
        /// <param name="command">The command code.</param>
        /// <param name="status">The status code.</param>
        public FastDFSHeader(long bodyLength, byte command, byte status = 0)
        {
            BodyLength = bodyLength;
            Command = command;
            Status = status;
        }

        /// <summary>
        /// Converts the header to a byte array (10 bytes).
        /// </summary>
        /// <returns>The byte array representation of the header.</returns>
        public byte[] ToBytes()
        {
            var buffer = new byte[HeaderSize];

            // Bytes 0-7: Body length (big-endian)
            ByteConverter.WriteInt64(BodyLength, buffer, 0);

            // Byte 8: Command
            buffer[8] = Command;

            // Byte 9: Status
            buffer[9] = Status;

            return buffer;
        }

        /// <summary>
        /// Parses a header from a byte array.
        /// </summary>
        /// <param name="buffer">The byte array containing the header (must be at least 10 bytes).</param>
        /// <param name="offset">The offset in the buffer to start parsing.</param>
        /// <returns>The parsed header.</returns>
        public static FastDFSHeader Parse(byte[] buffer, int offset = 0)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (buffer.Length < offset + HeaderSize)
                throw new ArgumentException($"Buffer is too short. Expected at least {HeaderSize} bytes.", nameof(buffer));

            var header = new FastDFSHeader
            {
                // Bytes 0-7: Body length (big-endian)
                BodyLength = ByteConverter.ToInt64(buffer, offset),

                // Byte 8: Command
                Command = buffer[offset + 8],

                // Byte 9: Status
                Status = buffer[offset + 9]
            };

            return header;
        }

        /// <summary>
        /// Checks if the status indicates success.
        /// </summary>
        public bool IsSuccess => Status == 0;

        /// <summary>
        /// Returns a string representation of the header.
        /// </summary>
        public override string ToString()
        {
            return $"FastDFSHeader [BodyLength={BodyLength}, Command={Command}, Status={Status}]";
        }
    }
}
