using FastDFS.Client.Protocol.Responses;

namespace FastDFS.Client.Protocol.Requests
{
    /// <summary>
    /// Request to query all available storage servers for upload without specifying group.
    /// The tracker will return all available storage servers.
    /// Command: TRACKER_PROTO_CMD_SERVICE_QUERY_STORE_WITHOUT_GROUP_ALL (105)
    /// </summary>
    public class QueryStoreWithoutGroupAllRequest : FastDFSRequest<QueryStoreAllResponse>
    {
        /// <summary>
        /// Gets the command code for this request.
        /// </summary>
        public override byte Command => TrackerCommand.QueryStoreWithoutGroupAll;

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
