using System;
using FastDFS.Client.Protocol;
using FastDFS.Client.Protocol.Encoding;

namespace FastDFS.Client.Domain.Identifiers
{
    internal readonly struct GroupName
    {
        private GroupName(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public static GroupName Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Group name cannot be null or empty.", nameof(value));

            ProtocolFieldLengthGuard.EnsureUtf8FitsFixedField("groupName", value, FastDFSConstants.GroupNameMaxLength);
            return new GroupName(value);
        }
    }
}
