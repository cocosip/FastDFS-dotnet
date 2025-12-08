namespace FastDFS.Client.Protocol
{
    /// <summary>
    /// FastDFS protocol constants.
    /// </summary>
    public static class FastDFSConstants
    {
        /// <summary>
        /// Default tracker server port.
        /// </summary>
        public const int DefaultTrackerPort = 22122;

        /// <summary>
        /// Default storage server port.
        /// </summary>
        public const int DefaultStoragePort = 23000;

        /// <summary>
        /// Maximum group name length.
        /// </summary>
        public const int GroupNameMaxLength = 16;

        /// <summary>
        /// Maximum file extension name length (including dot).
        /// </summary>
        public const int FileExtNameMaxLength = 6;

        /// <summary>
        /// Maximum file path length.
        /// </summary>
        public const int FilePathMaxLength = 128;

        /// <summary>
        /// IP address string length (IPv4: "xxx.xxx.xxx.xxx").
        /// </summary>
        public const int IpAddressLength = 16;

        /// <summary>
        /// Storage server ID length.
        /// </summary>
        public const int StorageIdMaxLength = 16;

        /// <summary>
        /// Maximum metadata name length.
        /// </summary>
        public const int MetadataNameMaxLength = 64;

        /// <summary>
        /// Maximum metadata value length.
        /// </summary>
        public const int MetadataValueMaxLength = 256;

        /// <summary>
        /// Metadata field separator.
        /// </summary>
        public const char MetadataFieldSeparator = '\x02';

        /// <summary>
        /// Metadata key-value separator.
        /// </summary>
        public const char MetadataKeyValueSeparator = '\x01';

        /// <summary>
        /// Default network timeout in seconds.
        /// </summary>
        public const int DefaultNetworkTimeoutSeconds = 30;

        /// <summary>
        /// Default character encoding (UTF-8).
        /// </summary>
        public const string DefaultCharset = "UTF-8";

        /// <summary>
        /// Protocol version for FastDFS.
        /// </summary>
        public const byte ProtocolVersion = 0;

        /// <summary>
        /// File prefix max length.
        /// </summary>
        public const int FilePrefixMaxLength = 16;

        /// <summary>
        /// Store path index for auto selection.
        /// </summary>
        public const byte StorePathIndexAuto = 255;
    }
}
