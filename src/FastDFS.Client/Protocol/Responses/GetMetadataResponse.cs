namespace FastDFS.Client.Protocol.Responses
{
    /// <summary>
    /// Response for get metadata operation.
    /// Command: STORAGE_PROTO_CMD_GET_METADATA (15).
    /// Body format: key1\x02value1\x01key2\x02value2\x01...
    /// </summary>
    public class GetMetadataResponse : FastDFSResponse
    {
        /// <summary>
        /// Gets the file metadata.
        /// </summary>
        public FastDFSMetadata Metadata { get; private set; } = new FastDFSMetadata();

        /// <summary>
        /// Decodes the response body.
        /// </summary>
        protected override void DecodeBody(byte[]? body)
        {
            if (body == null || body.Length == 0)
            {
                Metadata = new FastDFSMetadata();
                return;
            }

            Metadata = FastDFSMetadata.Decode(body);
        }
    }
}
