using System;
using FastDFS.Client.Protocol;
using FastDFS.Client.Protocol.Encoding;

namespace FastDFS.Client.Domain.Identifiers
{
    internal readonly struct StorageServerId
    {
        private StorageServerId(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public static StorageServerId Create(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Storage server ID cannot be null or empty.", nameof(value));

            ProtocolFieldLengthGuard.EnsureUtf8FitsFixedField("storageServerId", value, FastDFSConstants.StorageIdMaxLength);
            return new StorageServerId(value);
        }
    }
}
