using System;
using System.Text;

namespace FastDFS.Client.Protocol.Responses
{
    /// <summary>
    /// Response for upload file request.
    /// Returns the file name (path) on the storage server.
    /// Response body structure:
    /// - Group name (16 bytes)
    /// - File name (variable length)
    /// </summary>
    public class UploadFileResponse : FastDFSResponse
    {
        /// <summary>
        /// Gets the group name.
        /// </summary>
        public string GroupName { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the file name (path on storage server).
        /// </summary>
        public string FileName { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the complete file ID (group_name/file_name).
        /// </summary>
        public string FileId => $"{GroupName}/{FileName}";

        /// <summary>
        /// Decodes the response body.
        /// </summary>
        /// <param name="body">The response body bytes.</param>
        protected override void DecodeBody(byte[]? body)
        {
            if (body == null || body.Length < FastDFSConstants.GroupNameMaxLength)
            {
                throw new ArgumentException($"Invalid response body length. Expected at least {FastDFSConstants.GroupNameMaxLength} bytes.", nameof(body));
            }

            int offset = 0;

            // Group name (16 bytes, fixed)
            GroupName = Encoding.UTF8.GetString(body, offset, FastDFSConstants.GroupNameMaxLength).TrimEnd('\0');
            offset += FastDFSConstants.GroupNameMaxLength;

            // File name (remaining bytes)
            if (body.Length > offset)
            {
                FileName = Encoding.UTF8.GetString(body, offset, body.Length - offset);
            }
        }

        /// <summary>
        /// Returns a string representation of the upload response.
        /// </summary>
        public override string ToString()
        {
            return $"UploadFileResponse [FileId={FileId}]";
        }
    }
}
