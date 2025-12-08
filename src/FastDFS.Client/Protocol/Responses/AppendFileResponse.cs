namespace FastDFS.Client.Protocol.Responses
{
    /// <summary>
    /// Response for append file request.
    /// This is a simple success/failure response with no body.
    /// </summary>
    public class AppendFileResponse : FastDFSResponse
    {
        // No additional properties needed
        // Status is checked via IsSuccess property from base class
    }
}
