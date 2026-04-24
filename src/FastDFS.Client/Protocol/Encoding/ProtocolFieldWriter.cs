using System;

namespace FastDFS.Client.Protocol.Encoding
{
    /// <summary>
    /// Writes UTF-8 encoded values into fixed-width FastDFS fields with zero padding.
    /// </summary>
    internal static class ProtocolFieldWriter
    {
        public static void WriteFixedUtf8(byte[] buffer, int offset, int length, string fieldName, string value)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || length <= 0 || offset + length > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            ProtocolFieldLengthGuard.EnsureUtf8FitsFixedField(fieldName, value, length);

            Array.Clear(buffer, offset, length);

            if (value.Length == 0)
                return;

            System.Text.Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, offset);
        }
    }
}
