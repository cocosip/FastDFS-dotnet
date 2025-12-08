namespace FastDFS.Client.Protocol
{
    /// <summary>
    /// Interface for FastDFS response packets.
    /// </summary>
    public interface IFastDFSResponse
    {
        /// <summary>
        /// Gets the response header.
        /// </summary>
        FastDFSHeader Header { get; }

        /// <summary>
        /// Gets whether the response indicates success.
        /// </summary>
        bool IsSuccess { get; }

        /// <summary>
        /// Decodes the response from raw bytes.
        /// </summary>
        /// <param name="header">The response header.</param>
        /// <param name="body">The response body (can be null or empty).</param>
        void Decode(FastDFSHeader header, byte[]? body);
    }
}
