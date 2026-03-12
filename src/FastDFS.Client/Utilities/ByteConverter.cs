using System;

namespace FastDFS.Client.Utilities
{
    /// <summary>
    /// Byte converter for big-endian and little-endian conversion.
    /// FastDFS protocol uses big-endian byte order.
    /// </summary>
    public static class ByteConverter
    {
        /// <summary>
        /// Converts a long value to big-endian byte array (8 bytes).
        /// </summary>
        /// <param name="value">The long value to convert.</param>
        /// <returns>Big-endian byte array.</returns>
        public static byte[] ToBytes(long value)
        {
            var bytes = new byte[8];
            WriteInt64(value, bytes, 0);
            return bytes;
        }

        /// <summary>
        /// Converts a int value to big-endian byte array (4 bytes).
        /// </summary>
        /// <param name="value">The int value to convert.</param>
        /// <returns>Big-endian byte array.</returns>
        public static byte[] ToBytes(int value)
        {
            var bytes = new byte[4];
            WriteInt32(value, bytes, 0);
            return bytes;
        }

        /// <summary>
        /// Converts a big-endian byte array to long value.
        /// </summary>
        /// <param name="bytes">The byte array (must be 8 bytes).</param>
        /// <param name="offset">The offset in the byte array.</param>
        /// <returns>The long value.</returns>
        public static long ToInt64(byte[] bytes, int offset = 0)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length < offset + 8)
                throw new ArgumentException("Byte array is too short for Int64 conversion.", nameof(bytes));

            unchecked
            {
                return ((long)bytes[offset] << 56)
                    | ((long)bytes[offset + 1] << 48)
                    | ((long)bytes[offset + 2] << 40)
                    | ((long)bytes[offset + 3] << 32)
                    | ((long)bytes[offset + 4] << 24)
                    | ((long)bytes[offset + 5] << 16)
                    | ((long)bytes[offset + 6] << 8)
                    | bytes[offset + 7];
            }
        }

        /// <summary>
        /// Converts a big-endian byte array to int value.
        /// </summary>
        /// <param name="bytes">The byte array (must be 4 bytes).</param>
        /// <param name="offset">The offset in the byte array.</param>
        /// <returns>The int value.</returns>
        public static int ToInt32(byte[] bytes, int offset = 0)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length < offset + 4)
                throw new ArgumentException("Byte array is too short for Int32 conversion.", nameof(bytes));

            unchecked
            {
                return (bytes[offset] << 24)
                    | (bytes[offset + 1] << 16)
                    | (bytes[offset + 2] << 8)
                    | bytes[offset + 3];
            }
        }

        /// <summary>
        /// Writes a long value to a byte array in big-endian format.
        /// </summary>
        /// <param name="value">The long value to write.</param>
        /// <param name="buffer">The target byte array.</param>
        /// <param name="offset">The offset in the target array.</param>
        public static void WriteInt64(long value, byte[] buffer, int offset)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (buffer.Length < offset + 8)
                throw new ArgumentException("Buffer is too short.", nameof(buffer));

            unchecked
            {
                buffer[offset] = (byte)(value >> 56);
                buffer[offset + 1] = (byte)(value >> 48);
                buffer[offset + 2] = (byte)(value >> 40);
                buffer[offset + 3] = (byte)(value >> 32);
                buffer[offset + 4] = (byte)(value >> 24);
                buffer[offset + 5] = (byte)(value >> 16);
                buffer[offset + 6] = (byte)(value >> 8);
                buffer[offset + 7] = (byte)value;
            }
        }

        /// <summary>
        /// Writes an int value to a byte array in big-endian format.
        /// </summary>
        /// <param name="value">The int value to write.</param>
        /// <param name="buffer">The target byte array.</param>
        /// <param name="offset">The offset in the target array.</param>
        public static void WriteInt32(int value, byte[] buffer, int offset)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (buffer.Length < offset + 4)
                throw new ArgumentException("Buffer is too short.", nameof(buffer));

            unchecked
            {
                buffer[offset] = (byte)(value >> 24);
                buffer[offset + 1] = (byte)(value >> 16);
                buffer[offset + 2] = (byte)(value >> 8);
                buffer[offset + 3] = (byte)value;
            }
        }
    }
}
