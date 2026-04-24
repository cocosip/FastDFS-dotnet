using System;
using System.Text;

namespace FastDFS.Client.Protocol.Encoding
{
    /// <summary>
    /// Guards UTF-8 encoded values before writing them into fixed-width FastDFS fields.
    /// </summary>
    internal static class ProtocolFieldLengthGuard
    {
        public static void EnsureUtf8FitsFixedField(string fieldName, string value, int maxBytes)
        {
            if (fieldName == null)
                throw new ArgumentNullException(nameof(fieldName));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (maxBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBytes), "Max bytes must be greater than 0.");

            int byteCount = System.Text.Encoding.UTF8.GetByteCount(value);
            if (byteCount > maxBytes)
            {
                throw new ArgumentException(
                    $"Value for '{fieldName}' exceeds the FastDFS fixed field limit of {maxBytes} bytes when UTF-8 encoded.",
                    nameof(value));
            }
        }
    }
}
