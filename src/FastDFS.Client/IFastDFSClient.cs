using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FastDFS.Client.Protocol;
using FastDFS.Client.Storage;

namespace FastDFS.Client
{
    /// <summary>
    /// Unified interface for FastDFS client operations.
    /// This is the main entry point for interacting with FastDFS.
    /// Automatically handles tracker queries and storage operations.
    /// </summary>
    public interface IFastDFSClient
    {
        /// <summary>
        /// Gets the name of this client instance (for multi-cluster scenarios).
        /// </summary>
        string Name { get; }

        // ==================== Upload Operations ====================

        /// <summary>
        /// Uploads a file from byte array.
        /// Automatically queries tracker for an available storage server.
        /// </summary>
        /// <param name="groupName">Optional: The storage group name. If null, tracker will select a group automatically.</param>
        /// <param name="content">The file content as byte array.</param>
        /// <param name="fileExtension">The file extension (e.g., "jpg", "txt"). Do not include the dot.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The complete file ID in the format "group_name/path/filename".</returns>
        Task<string> UploadAsync(string? groupName, byte[] content, string fileExtension, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uploads a file from a stream.
        /// Automatically queries tracker for an available storage server.
        /// </summary>
        /// <param name="groupName">Optional: The storage group name. If null, tracker will select a group automatically.</param>
        /// <param name="stream">The file content as a stream.</param>
        /// <param name="fileExtension">The file extension (e.g., "jpg", "txt"). Do not include the dot.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The complete file ID in the format "group_name/path/filename".</returns>
        Task<string> UploadAsync(string? groupName, Stream stream, string fileExtension, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uploads a file from local file path.
        /// Automatically queries tracker for an available storage server.
        /// </summary>
        /// <param name="groupName">Optional: The storage group name. If null, tracker will select a group automatically.</param>
        /// <param name="localFilePath">The local file path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The complete file ID in the format "group_name/path/filename".</returns>
        Task<string> UploadFileAsync(string? groupName, string localFilePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uploads an appender file from byte array.
        /// Appender files can have data appended to them after upload.
        /// Automatically queries tracker for an available storage server.
        /// </summary>
        /// <param name="groupName">Optional: The storage group name. If null, tracker will select a group automatically.</param>
        /// <param name="content">The file content as byte array.</param>
        /// <param name="fileExtension">The file extension (e.g., "log", "txt"). Do not include the dot.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The complete file ID in the format "group_name/path/filename".</returns>
        Task<string> UploadAppenderFileAsync(string? groupName, byte[] content, string fileExtension, CancellationToken cancellationToken = default);

        // ==================== Download Operations ====================

        /// <summary>
        /// Downloads a file as byte array.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file content as byte array.</returns>
        Task<byte[]> DownloadAsync(string fileId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a file as byte array.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file content as byte array.</returns>
        Task<byte[]> DownloadAsync(string? groupName, string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a file to a stream.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="outputStream">The output stream to write file content.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DownloadAsync(string fileId, Stream outputStream, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a file to a stream.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="outputStream">The output stream to write file content.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DownloadAsync(string? groupName, string fileName, Stream outputStream, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a file to local file system.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="localFilePath">The local file path to save the downloaded file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DownloadFileAsync(string fileId, string localFilePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a file to local file system.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="localFilePath">The local file path to save the downloaded file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DownloadFileAsync(string? groupName, string fileName, string localFilePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a portion of a file.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="offset">The offset in bytes to start downloading from.</param>
        /// <param name="length">The number of bytes to download.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file content as byte array.</returns>
        Task<byte[]> DownloadAsync(string fileId, long offset, long length, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a portion of a file.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="offset">The offset in bytes to start downloading from.</param>
        /// <param name="length">The number of bytes to download.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file content as byte array.</returns>
        Task<byte[]> DownloadAsync(string? groupName, string fileName, long offset, long length, CancellationToken cancellationToken = default);

        // ==================== Append Operations ====================

        /// <summary>
        /// Appends data to an existing appender file.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="fileId">The complete file ID of the appender file in the format "group_name/path/filename".</param>
        /// <param name="content">The content to append.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task AppendFileAsync(string fileId, byte[] content, CancellationToken cancellationToken = default);

        /// <summary>
        /// Appends data to an existing appender file.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="content">The content to append.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task AppendFileAsync(string? groupName, string fileName, byte[] content, CancellationToken cancellationToken = default);

        // ==================== Delete Operations ====================

        /// <summary>
        /// Deletes a file from the storage server.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DeleteAsync(string fileId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a file from the storage server.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DeleteAsync(string? groupName, string fileName, CancellationToken cancellationToken = default);

        // ==================== Query Operations ====================

        /// <summary>
        /// Queries file information.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file information.</returns>
        Task<FastDFSFileInfo> QueryFileInfoAsync(string fileId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries file information.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file information.</returns>
        Task<FastDFSFileInfo> QueryFileInfoAsync(string? groupName, string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a file exists.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the file exists; otherwise, false.</returns>
        Task<bool> FileExistsAsync(string fileId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a file exists.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the file exists; otherwise, false.</returns>
        Task<bool> FileExistsAsync(string? groupName, string fileName, CancellationToken cancellationToken = default);

        // ==================== Tracker Query Operations (Advanced) ====================

        /// <summary>
        /// Queries tracker for an available storage server for uploading.
        /// This is an advanced method that allows you to control the upload process manually.
        /// </summary>
        /// <param name="groupName">Optional: The storage group name. If null, tracker will select a group automatically.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Information about the storage server to use for upload.</returns>
        Task<Tracker.StorageServerInfo> QueryStorageForUploadAsync(string? groupName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries tracker for the storage server that contains the specified file.
        /// This is an advanced method that allows you to get storage server information.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Information about the storage server that contains the file.</returns>
        Task<Tracker.StorageServerInfo> QueryStorageForDownloadAsync(string fileId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries tracker for the storage server that contains the specified file.
        /// This is an advanced method that allows you to get storage server information.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Information about the storage server that contains the file.</returns>
        Task<Tracker.StorageServerInfo> QueryStorageForDownloadAsync(string? groupName, string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries tracker for the storage server that can update/delete the specified file.
        /// This is an advanced method that allows you to get storage server information.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Information about the storage server that can modify the file.</returns>
        Task<Tracker.StorageServerInfo> QueryStorageForUpdateAsync(string fileId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries tracker for the storage server that can update/delete the specified file.
        /// This is an advanced method that allows you to get storage server information.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Information about the storage server that can modify the file.</returns>
        Task<Tracker.StorageServerInfo> QueryStorageForUpdateAsync(string? groupName, string fileName, CancellationToken cancellationToken = default);

        // ==================== Metadata Operations ====================

        /// <summary>
        /// Sets metadata for a file.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="metadata">The metadata to set.</param>
        /// <param name="flag">The metadata operation flag (Overwrite or Merge).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SetMetadataAsync(string? groupName, string fileName, FastDFSMetadata metadata, MetadataFlag flag = MetadataFlag.Overwrite, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets metadata for a file using complete file ID.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="metadata">The metadata to set.</param>
        /// <param name="flag">The metadata operation flag (Overwrite or Merge).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SetMetadataAsync(string fileId, FastDFSMetadata metadata, MetadataFlag flag = MetadataFlag.Overwrite, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets metadata for a file.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file metadata.</returns>
        Task<FastDFSMetadata> GetMetadataAsync(string? groupName, string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets metadata for a file using complete file ID.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file metadata.</returns>
        Task<FastDFSMetadata> GetMetadataAsync(string fileId, CancellationToken cancellationToken = default);

        // ==================== Management Operations ====================

        /// <summary>
        /// Lists all storage groups in the cluster.
        /// This is a management operation used for monitoring, scheduling, and cluster administration.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of all storage groups with their information (disk usage, server count, etc.).</returns>
        Task<List<Tracker.GroupInfo>> ListAllGroupsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists all storage servers in a specific group.
        /// This is a management operation used for monitoring, scheduling, and cluster administration.
        /// </summary>
        /// <param name="groupName">The storage group name.</param>
        /// <param name="storageServerId">Optional: specific storage server ID (IP address) to query. If null, returns all servers in the group.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of storage servers in the specified group with detailed status information.</returns>
        Task<List<Tracker.StorageServerDetail>> ListStorageServersAsync(string groupName, string? storageServerId = null, CancellationToken cancellationToken = default);
    }
}
