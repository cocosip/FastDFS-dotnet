namespace FastDFS.Client.Protocol
{
    /// <summary>
    /// Interface for FastDFS request packets.
    /// </summary>
    public interface IFastDFSRequest
    {
        /// <summary>
        /// Gets the command code for this request.
        /// </summary>
        byte Command { get; }

        /// <summary>
        /// Encodes the request to a byte array for transmission.
        /// </summary>
        /// <returns>The complete packet bytes (header + body).</returns>
        byte[] Encode();

        /// <summary>
        /// Gets the expected response type for this request.
        /// </summary>
        /// <returns>The type of the response.</returns>
        System.Type GetResponseType();
    }
}
