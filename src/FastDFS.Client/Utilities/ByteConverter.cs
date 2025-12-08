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
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return bytes;
        }

        /// <summary>
        /// Converts a int value to big-endian byte array (4 bytes).
        /// </summary>
        /// <param name="value">The int value to convert.</param>
        /// <returns>Big-endian byte array.</returns>
        public static byte[] ToBytes(int value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
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

            if (BitConverter.IsLittleEndian)
            {
                // Need to reverse for big-endian to little-endian conversion
                var temp = new byte[8];
                Array.Copy(bytes, offset, temp, 0, 8);
                Array.Reverse(temp);
                return BitConverter.ToInt64(temp, 0);
            }
            else
            {
                return BitConverter.ToInt64(bytes, offset);
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

            if (BitConverter.IsLittleEndian)
            {
                // Need to reverse for big-endian to little-endian conversion
                var temp = new byte[4];
                Array.Copy(bytes, offset, temp, 0, 4);
                Array.Reverse(temp);
                return BitConverter.ToInt32(temp, 0);
            }
            else
            {
                return BitConverter.ToInt32(bytes, offset);
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

            var bytes = ToBytes(value);
            Array.Copy(bytes, 0, buffer, offset, 8);
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

            var bytes = ToBytes(value);
            Array.Copy(bytes, 0, buffer, offset, 4);
        }
    }
}
