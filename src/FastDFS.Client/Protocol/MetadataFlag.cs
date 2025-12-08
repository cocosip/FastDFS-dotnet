namespace FastDFS.Client.Protocol
{
    /// <summary>
    /// Metadata operation flags for FastDFS.
    /// </summary>
    public enum MetadataFlag : byte
    {
        /// <summary>
        /// Overwrite existing metadata (O).
        /// </summary>
        Overwrite = (byte)'O',

        /// <summary>
        /// Merge with existing metadata (M).
        /// </summary>
        Merge = (byte)'M'
    }
}
