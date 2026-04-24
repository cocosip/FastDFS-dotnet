using System;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Protocol.Decoding
{
    internal static class ProtocolFieldReader
    {
        public static string ReadFixedUtf8(byte[] buffer, int offset, int length)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || length <= 0 || offset + length > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            return buffer.ReadFixedString(offset, length, System.Text.Encoding.UTF8).TrimEnd('\0');
        }
    }
}
