namespace FastDFS.Client.Protocol
{
    /// <summary>
    /// FastDFS Storage server command codes.
    /// </summary>
    public static class StorageCommand
    {
        /// <summary>
        /// Upload file (11).
        /// </summary>
        public const byte UploadFile = 11;

        /// <summary>
        /// Delete file (12).
        /// </summary>
        public const byte DeleteFile = 12;

        /// <summary>
        /// Set metadata (13).
        /// </summary>
        public const byte SetMetadata = 13;

        /// <summary>
        /// Download file (14).
        /// </summary>
        public const byte DownloadFile = 14;

        /// <summary>
        /// Get metadata (15).
        /// </summary>
        public const byte GetMetadata = 15;

        /// <summary>
        /// Upload slave file (21).
        /// </summary>
        public const byte UploadSlaveFile = 21;

        /// <summary>
        /// Query file information (22).
        /// </summary>
        public const byte QueryFileInfo = 22;

        /// <summary>
        /// Upload appender file (23).
        /// Appender file can be appended with new data.
        /// </summary>
        public const byte UploadAppenderFile = 23;

        /// <summary>
        /// Append data to file (24).
        /// </summary>
        public const byte AppendFile = 24;

        /// <summary>
        /// Modify file (34).
        /// </summary>
        public const byte ModifyFile = 34;

        /// <summary>
        /// Truncate file (36).
        /// </summary>
        public const byte TruncateFile = 36;

        /// <summary>
        /// Regenerate appender file name (38).
        /// </summary>
        public const byte RegenerateAppenderFileName = 38;

        /// <summary>
        /// Query file information from filename (42).
        /// </summary>
        public const byte QueryFileInfoByFilename = 42;
    }
}
