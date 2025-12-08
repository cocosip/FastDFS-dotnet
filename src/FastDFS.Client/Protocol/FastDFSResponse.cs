using System;

namespace FastDFS.Client.Protocol
{
    /// <summary>
    /// Abstract base class for FastDFS response packets.
    /// </summary>
    public abstract class FastDFSResponse : FastDFSPacket, IFastDFSResponse
    {
        /// <summary>
        /// Gets whether the response indicates success.
        /// </summary>
        public bool IsSuccess => Header?.IsSuccess ?? false;

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSResponse"/> class.
        /// </summary>
        protected FastDFSResponse()
        {
        }

        /// <summary>
        /// Decodes the response from raw bytes.
        /// </summary>
        /// <param name="header">The response header.</param>
        /// <param name="body">The response body (can be null or empty).</param>
        public void Decode(FastDFSHeader header, byte[]? body)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            Body = body;

            if (IsSuccess)
            {
                DecodeBody(body);
            }
        }

        /// <summary>
        /// Decodes the response body. Override this to provide custom body decoding.
        /// This method is only called when the response status indicates success.
        /// </summary>
        /// <param name="body">The response body bytes.</param>
        protected virtual void DecodeBody(byte[]? body)
        {
            // Default implementation does nothing
            // Derived classes should override this to parse the body
        }

        /// <summary>
        /// Gets the error message if the response indicates an error.
        /// </summary>
        /// <returns>Error message or null if success.</returns>
        public string? GetErrorMessage()
        {
            if (IsSuccess)
                return null;

            return $"FastDFS error: Status code {Header.Status}";
        }
    }
}
