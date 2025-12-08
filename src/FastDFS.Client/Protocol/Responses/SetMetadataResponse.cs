namespace FastDFS.Client.Protocol.Responses
{
    /// <summary>
    /// Response for set metadata operation.
    /// Command: STORAGE_PROTO_CMD_SET_METADATA (13).
    /// This is an empty response - success is indicated by status code 0.
    /// </summary>
    public class SetMetadataResponse : FastDFSResponse
    {
        /// <summary>
        /// Decodes the response body (empty for this operation).
        /// </summary>
        protected override void DecodeBody(byte[]? body)
        {
            // No body content for set metadata response
            // Success is indicated by status code 0 in the header
        }
    }
}
