using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FastDFS.Client.Protocol;

namespace FastDFS.Client
{
    /// <summary>
    /// Represents metadata (custom attributes) for a FastDFS file.
    /// Metadata is stored as key-value pairs.
    /// </summary>
    public class FastDFSMetadata
    {
        private readonly Dictionary<string, string> _metadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSMetadata"/> class.
        /// </summary>
        public FastDFSMetadata()
        {
            _metadata = new Dictionary<string, string>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSMetadata"/> class with initial data.
        /// </summary>
        /// <param name="metadata">Initial metadata key-value pairs.</param>
        public FastDFSMetadata(Dictionary<string, string> metadata)
        {
            _metadata = new Dictionary<string, string>();

            if (metadata == null)
                return;

            foreach (var item in metadata)
            {
                SetValue(item.Key, item.Value);
            }
        }

        /// <summary>
        /// Gets or sets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The metadata key.</param>
        /// <returns>The metadata value.</returns>
        public string this[string key]
        {
            get => _metadata[key];
            set => SetValue(key, value);
        }

        /// <summary>
        /// Gets the number of metadata entries.
        /// </summary>
        public int Count => _metadata.Count;

        /// <summary>
        /// Gets all metadata keys.
        /// </summary>
        public IEnumerable<string> Keys => _metadata.Keys;

        /// <summary>
        /// Gets all metadata values.
        /// </summary>
        public IEnumerable<string> Values => _metadata.Values;

        /// <summary>
        /// Adds a metadata entry.
        /// </summary>
        /// <param name="key">The metadata key.</param>
        /// <param name="value">The metadata value.</param>
        public void Add(string key, string value)
        {
            SetValue(key, value);
        }

        private void SetValue(string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Metadata key cannot be null or empty.", nameof(key));

            ValidateMetadataComponent(key, nameof(key));
            ValidateMetadataComponent(value, nameof(value));

            _metadata[key] = value ?? string.Empty;
        }

        /// <summary>
        /// Removes a metadata entry.
        /// </summary>
        /// <param name="key">The metadata key to remove.</param>
        /// <returns>True if the entry was removed; otherwise, false.</returns>
        public bool Remove(string key)
        {
            return _metadata.Remove(key);
        }

        /// <summary>
        /// Checks if a metadata key exists.
        /// </summary>
        /// <param name="key">The metadata key.</param>
        /// <returns>True if the key exists; otherwise, false.</returns>
        public bool ContainsKey(string key)
        {
            return _metadata.ContainsKey(key);
        }

        /// <summary>
        /// Tries to get the value associated with the specified key.
        /// </summary>
        /// <param name="key">The metadata key.</param>
        /// <param name="value">The metadata value if found.</param>
        /// <returns>True if the key was found; otherwise, false.</returns>
        public bool TryGetValue(string key, out string value)
        {
            return _metadata.TryGetValue(key, out value);
        }

        /// <summary>
        /// Clears all metadata entries.
        /// </summary>
        public void Clear()
        {
            _metadata.Clear();
        }

        /// <summary>
        /// Gets all metadata as a dictionary.
        /// </summary>
        /// <returns>A copy of the metadata dictionary.</returns>
        public Dictionary<string, string> ToDictionary()
        {
            return new Dictionary<string, string>(_metadata);
        }

        /// <summary>
        /// Encodes metadata to FastDFS protocol format.
        /// Format: key1\x02value1\x01key2\x02value2\x01...
        /// </summary>
        internal byte[] Encode()
        {
            if (_metadata.Count == 0)
                return Array.Empty<byte>();

            var sb = new StringBuilder();
            bool first = true;

            foreach (var kvp in _metadata)
            {
                if (!first)
                    sb.Append('\x01'); // FastDFS_RECORD_SEPERATOR

                sb.Append(kvp.Key);
                sb.Append('\x02'); // FastDFS_FIELD_SEPERATOR
                sb.Append(kvp.Value);

                first = false;
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        /// <summary>
        /// Decodes metadata from FastDFS protocol format.
        /// Format: key1\x02value1\x01key2\x02value2\x01...
        /// </summary>
        internal static FastDFSMetadata Decode(byte[] data)
        {
            var metadata = new FastDFSMetadata();

            if (data == null || data.Length == 0)
                return metadata;

            var content = Encoding.UTF8.GetString(data);
            var records = content.Split('\x01'); // FastDFS_RECORD_SEPERATOR

            foreach (var record in records)
            {
                if (string.IsNullOrEmpty(record))
                    continue;

                int separatorIndex = record.IndexOf('\x02'); // FastDFS_FIELD_SEPERATOR
                if (separatorIndex <= 0)
                    continue;

                string key = record.Substring(0, separatorIndex);
                string value = record.Substring(separatorIndex + 1);
                metadata.Add(key, value);
            }

            return metadata;
        }

        private static void ValidateMetadataComponent(string? value, string paramName)
        {
            if (value == null)
                return;

            if (value.IndexOf('\x01') >= 0)
                throw new ArgumentException("Metadata keys and values cannot contain the FastDFS record separator (\\x01).", paramName);

            if (value.IndexOf('\x02') >= 0)
                throw new ArgumentException("Metadata keys and values cannot contain the FastDFS field separator (\\x02).", paramName);
        }
 
        /// <summary>
        /// Returns a string representation of the metadata.
        /// </summary>
        public override string ToString()
        {
            return $"FastDFSMetadata[Count={Count}]";
        }
    }
}
