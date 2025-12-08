namespace FastDFS.Client.Protocol.Responses
{
    /// <summary>
    /// Response for delete file request.
    /// This response has no body on success.
    /// Only the status code in the header indicates success or failure.
    /// </summary>
    public class DeleteFileResponse : FastDFSResponse
    {
        // No additional fields needed - success is indicated by header status code

        /// <summary>
        /// Returns a string representation of the delete response.
        /// </summary>
        public override string ToString()
        {
            return $"DeleteFileResponse [Success={IsSuccess}]";
        }
    }
}
