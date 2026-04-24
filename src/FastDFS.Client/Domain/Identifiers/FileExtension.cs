using System;

namespace FastDFS.Client.Domain.Identifiers
{
    internal readonly struct FileExtension
    {
        private FileExtension(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public static FileExtension Create(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.StartsWith(".", StringComparison.Ordinal))
                value = value.Substring(1);

            return new FileExtension(value);
        }
    }
}
