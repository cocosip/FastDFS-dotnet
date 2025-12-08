using System;

namespace FastDFS.Client.Protocol.Responses
{
    /// <summary>
    /// Response for download file request.
    /// Returns the file content as bytes.
    /// Response body structure:
    /// - File content (entire body is file data)
    /// </summary>
    public class DownloadFileResponse : FastDFSResponse
    {
        /// <summary>
        /// Gets the file content.
        /// </summary>
        public byte[] Content { get; private set; } = Array.Empty<byte>();

        /// <summary>
        /// Decodes the response body.
        /// </summary>
        /// <param name="body">The response body bytes (entire body is file content).</param>
        protected override void DecodeBody(byte[]? body)
        {
            if (body == null || body.Length == 0)
            {
                Content = Array.Empty<byte>();
                return;
            }

            Content = body;
        }

        /// <summary>
        /// Returns a string representation of the download response.
        /// </summary>
        public override string ToString()
        {
            return $"DownloadFileResponse [ContentLength={Content.Length}]";
        }
    }
}
