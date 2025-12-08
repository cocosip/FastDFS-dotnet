using System;
using System.Text;

namespace FastDFS.Client.Utilities
{
    /// <summary>
    /// Extension methods for byte array operations.
    /// </summary>
    public static class ByteExtensions
    {
        /// <summary>
        /// Converts a string to byte array using specified encoding.
        /// </summary>
        /// <param name="str">The string to convert.</param>
        /// <param name="encoding">The encoding to use. Defaults to UTF-8.</param>
        /// <returns>Byte array representation of the string.</returns>
        public static byte[] ToBytes(this string str, Encoding? encoding = null)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));

            encoding ??= Encoding.UTF8;
            return encoding.GetBytes(str);
        }

        /// <summary>
        /// Converts a byte array to string using specified encoding.
        /// </summary>
        /// <param name="bytes">The byte array to convert.</param>
        /// <param name="encoding">The encoding to use. Defaults to UTF-8.</param>
        /// <returns>String representation of the byte array.</returns>
        public static string ToStringFromBytes(this byte[] bytes, Encoding? encoding = null)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            encoding ??= Encoding.UTF8;
            return encoding.GetString(bytes);
        }

        /// <summary>
        /// Converts a byte array segment to string using specified encoding.
        /// </summary>
        /// <param name="bytes">The byte array to convert.</param>
        /// <param name="offset">The offset in the byte array.</param>
        /// <param name="count">The number of bytes to convert.</param>
        /// <param name="encoding">The encoding to use. Defaults to UTF-8.</param>
        /// <returns>String representation of the byte array segment.</returns>
        public static string ToStringFromBytes(this byte[] bytes, int offset, int count, Encoding? encoding = null)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (offset < 0 || offset >= bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            encoding ??= Encoding.UTF8;
            return encoding.GetString(bytes, offset, count);
        }

        /// <summary>
        /// Fills a byte array with a specific value.
        /// </summary>
        /// <param name="bytes">The byte array to fill.</param>
        /// <param name="value">The value to fill with.</param>
        public static void Fill(this byte[] bytes, byte value)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = value;
            }
        }

        /// <summary>
        /// Copies a fixed-length string to a byte buffer, padding with null bytes if necessary.
        /// Used for FastDFS protocol fixed-length fields.
        /// </summary>
        /// <param name="str">The string to copy.</param>
        /// <param name="buffer">The target byte array.</param>
        /// <param name="offset">The offset in the target array.</param>
        /// <param name="length">The fixed length to occupy.</param>
        /// <param name="encoding">The encoding to use. Defaults to UTF-8.</param>
        public static void CopyFixedString(string str, byte[] buffer, int offset, int length, Encoding? encoding = null)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset + length > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            encoding ??= Encoding.UTF8;

            // Clear the target area first
            Array.Clear(buffer, offset, length);

            if (string.IsNullOrEmpty(str))
                return;

            var bytes = encoding.GetBytes(str);
            var copyLength = Math.Min(bytes.Length, length);
            Array.Copy(bytes, 0, buffer, offset, copyLength);
        }

        /// <summary>
        /// Reads a fixed-length string from a byte buffer, trimming null bytes.
        /// Used for FastDFS protocol fixed-length fields.
        /// </summary>
        /// <param name="buffer">The source byte array.</param>
        /// <param name="offset">The offset in the source array.</param>
        /// <param name="length">The fixed length to read.</param>
        /// <param name="encoding">The encoding to use. Defaults to UTF-8.</param>
        /// <returns>The string, with trailing null bytes removed.</returns>
        public static string ReadFixedString(byte[] buffer, int offset, int length, Encoding? encoding = null)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset + length > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            encoding ??= Encoding.UTF8;

            // Find the first null byte
            int actualLength = length;
            for (int i = 0; i < length; i++)
            {
                if (buffer[offset + i] == 0)
                {
                    actualLength = i;
                    break;
                }
            }

            if (actualLength == 0)
                return string.Empty;

            return encoding.GetString(buffer, offset, actualLength);
        }
    }
}
