using FastDFS.Client.Protocol.Responses;

namespace FastDFS.Client.Protocol.Requests
{
    /// <summary>
    /// Request to list all storage groups.
    /// Command: TRACKER_PROTO_CMD_SERVER_LIST_ALL_GROUPS (91).
    /// </summary>
    public class ListAllGroupsRequest : FastDFSRequest<ListAllGroupsResponse>
    {
        /// <summary>
        /// Gets the command code for list all groups operation.
        /// </summary>
        public override byte Command => TrackerCommand.ListAllGroups;

        /// <summary>
        /// Encodes the request body (empty for this command).
        /// </summary>
        protected override byte[]? EncodeBody()
        {
            // This command has no body
            return null;
        }
    }
}
