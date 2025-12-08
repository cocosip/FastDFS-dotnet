using FastDFS.Client.Protocol.Responses;

namespace FastDFS.Client.Protocol.Requests
{
    /// <summary>
    /// Request to query a storage server for upload without specifying group.
    /// The tracker will select an available storage server automatically.
    /// Command: TRACKER_PROTO_CMD_SERVICE_QUERY_STORE_WITHOUT_GROUP_ONE (101)
    /// </summary>
    public class QueryStoreWithoutGroupRequest : FastDFSRequest<QueryStoreResponse>
    {
        /// <summary>
        /// Gets the command code for this request.
        /// </summary>
        public override byte Command => TrackerCommand.QueryStoreWithoutGroupOne;

        /// <summary>
        /// Encodes the request body.
        /// This request has no body.
        /// </summary>
        /// <returns>Null (no body).</returns>
        protected override byte[]? EncodeBody()
        {
            return null;
        }
    }
}
