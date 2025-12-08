using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FastDFS.Client.Tracker
{
    /// <summary>
    /// Interface for FastDFS Tracker client operations.
    /// </summary>
    public interface ITrackerClient
    {
        /// <summary>
        /// Queries a Storage server for uploading files.
        /// Tracker selects one available storage server based on server-side load balancing.
        /// </summary>
        /// <param name="groupName">The storage group name. If null or empty, the tracker will select a group automatically.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Storage server information for upload.</returns>
        Task<StorageServerInfo> QueryStorageForUploadAsync(string? groupName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries all available Storage servers for uploading files.
        /// Allows client-side load balancing and storage selection strategies.
        /// </summary>
        /// <param name="groupName">The storage group name. If null or empty, the tracker will select a group automatically.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of all available storage servers for upload.</returns>
        Task<List<StorageServerInfo>> QueryAllStoragesForUploadAsync(string? groupName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries a Storage server for downloading a file.
        /// Tracker selects one available storage server that has the file.
        /// </summary>
        /// <param name="groupName">The storage group name.</param>
        /// <param name="fileName">The file name (path on storage server).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Storage server information for download.</returns>
        Task<StorageServerInfo> QueryStorageForDownloadAsync(string groupName, string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries all Storage servers for downloading a file.
        /// Returns all storage servers that have the file.
        /// Allows client-side selection based on network latency, geographic location, etc.
        /// </summary>
        /// <param name="groupName">The storage group name.</param>
        /// <param name="fileName">The file name (path on storage server).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of all storage servers that have the file.</returns>
        Task<List<StorageServerInfo>> QueryAllStoragesForDownloadAsync(string groupName, string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries a Storage server for updating a file (delete, set metadata, etc.).
        /// </summary>
        /// <param name="groupName">The storage group name.</param>
        /// <param name="fileName">The file name (path on storage server).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Storage server information for update.</returns>
        Task<StorageServerInfo> QueryStorageForUpdateAsync(string groupName, string fileName, CancellationToken cancellationToken = default);

        // ==================== Management Operations ====================

        /// <summary>
        /// Lists all storage groups in the cluster.
        /// This is a management operation used for monitoring and scheduling.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of all storage groups with their information.</returns>
        Task<List<GroupInfo>> ListAllGroupsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists all storage servers in a specific group.
        /// This is a management operation used for monitoring and scheduling.
        /// </summary>
        /// <param name="groupName">The storage group name.</param>
        /// <param name="storageServerId">Optional: specific storage server ID (IP address) to query. If null, returns all servers in the group.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of storage servers in the specified group with detailed information.</returns>
        Task<List<StorageServerDetail>> ListStorageServersAsync(string groupName, string? storageServerId = null, CancellationToken cancellationToken = default);
    }
}
