using System;

namespace FastDFS.Client.Protocol
{
    /// <summary>
    /// Abstract base class for FastDFS request packets.
    /// </summary>
    /// <typeparam name="TResponse">The response type for this request.</typeparam>
    public abstract class FastDFSRequest<TResponse> : FastDFSPacket, IFastDFSRequest
        where TResponse : IFastDFSResponse, new()
    {
        /// <summary>
        /// Gets the command code for this request.
        /// </summary>
        public abstract byte Command { get; }

        /// <summary>
        /// Encodes the request body. Override this to provide custom body encoding.
        /// </summary>
        /// <returns>The encoded body bytes, or null if no body.</returns>
        protected abstract byte[]? EncodeBody();

        /// <summary>
        /// Encodes the request to a byte array for transmission.
        /// </summary>
        /// <returns>The complete packet bytes (header + body).</returns>
        public byte[] Encode()
        {
            Body = EncodeBody();
            Header = new FastDFSHeader(Body?.Length ?? 0, Command, 0);
            return ToBytes();
        }

        /// <summary>
        /// Gets the expected response type for this request.
        /// </summary>
        /// <returns>The type of the response.</returns>
        public Type GetResponseType()
        {
            return typeof(TResponse);
        }

        /// <summary>
        /// Creates a new instance of the response type.
        /// </summary>
        /// <returns>A new response instance.</returns>
        public TResponse CreateResponse()
        {
            return new TResponse();
        }
    }
}
