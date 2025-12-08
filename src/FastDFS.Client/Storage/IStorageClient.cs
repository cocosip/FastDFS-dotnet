using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FastDFS.Client.Protocol;

namespace FastDFS.Client.Storage
{
    /// <summary>
    /// Interface for FastDFS Storage client operations.
    /// </summary>
    public interface IStorageClient
    {
        /// <summary>
        /// Uploads a file from byte array.
        /// </summary>
        /// <param name="groupName">The storage group name. If null, the tracker will select a group.</param>
        /// <param name="content">The file content as byte array.</param>
        /// <param name="fileExtension">The file extension (e.g., "jpg", "txt"). Do not include the dot.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The complete file ID in the format "group_name/path/filename".</returns>
        Task<string> UploadAsync(string? groupName, byte[] content, string fileExtension, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uploads a file from a stream.
        /// </summary>
        /// <param name="groupName">The storage group name. If null, the tracker will select a group.</param>
        /// <param name="stream">The file content as a stream.</param>
        /// <param name="fileExtension">The file extension (e.g., "jpg", "txt"). Do not include the dot.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The complete file ID in the format "group_name/path/filename".</returns>
        Task<string> UploadAsync(string? groupName, Stream stream, string fileExtension, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uploads a file from local file path.
        /// </summary>
        /// <param name="groupName">The storage group name. If null, the tracker will select a group.</param>
        /// <param name="localFilePath">The local file path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The complete file ID in the format "group_name/path/filename".</returns>
        Task<string> UploadFileAsync(string? groupName, string localFilePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a file as byte array.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file content as byte array.</returns>
        Task<byte[]> DownloadAsync(string? groupName, string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a file to a stream.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="outputStream">The output stream to write file content.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DownloadAsync(string? groupName, string fileName, Stream outputStream, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a file to local file system.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="localFilePath">The local file path to save the downloaded file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DownloadFileAsync(string? groupName, string fileName, string localFilePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a portion of a file.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="offset">The offset in bytes to start downloading from.</param>
        /// <param name="length">The number of bytes to download.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file content as byte array.</returns>
        Task<byte[]> DownloadAsync(string? groupName, string fileName, long offset, long length, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a file from the storage server.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DeleteAsync(string? groupName, string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries file information.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file information.</returns>
        Task<FastDFSFileInfo> QueryFileInfoAsync(string? groupName, string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uploads an appender file from byte array.
        /// Appender files can have data appended to them after upload.
        /// </summary>
        /// <param name="groupName">The storage group name. If null, the tracker will select a group.</param>
        /// <param name="content">The file content as byte array.</param>
        /// <param name="fileExtension">The file extension (e.g., "log", "txt"). Do not include the dot.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The complete file ID in the format "group_name/path/filename".</returns>
        Task<string> UploadAppenderFileAsync(string? groupName, byte[] content, string fileExtension, CancellationToken cancellationToken = default);

        /// <summary>
        /// Appends data to an existing appender file.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server) of the appender file, or complete file ID in format "group_name/path/filename".</param>
        /// <param name="content">The content to append.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task AppendFileAsync(string? groupName, string fileName, byte[] content, CancellationToken cancellationToken = default);

        /// <summary>
        /// Appends data to an existing appender file using complete file ID.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="content">The content to append.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task AppendFileAsync(string fileId, byte[] content, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a file using complete file ID.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file content as byte array.</returns>
        Task<byte[]> DownloadAsync(string fileId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a file using complete file ID.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DeleteAsync(string fileId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries file information using complete file ID.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file information.</returns>
        Task<FastDFSFileInfo> QueryFileInfoAsync(string fileId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets metadata for a file.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="metadata">The metadata to set.</param>
        /// <param name="flag">The metadata operation flag (Overwrite or Merge).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SetMetadataAsync(string? groupName, string fileName, FastDFSMetadata metadata, MetadataFlag flag = MetadataFlag.Overwrite, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets metadata for a file using complete file ID.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="metadata">The metadata to set.</param>
        /// <param name="flag">The metadata operation flag (Overwrite or Merge).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SetMetadataAsync(string fileId, FastDFSMetadata metadata, MetadataFlag flag = MetadataFlag.Overwrite, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets metadata for a file.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file metadata.</returns>
        Task<FastDFSMetadata> GetMetadataAsync(string? groupName, string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets metadata for a file using complete file ID.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file metadata.</returns>
        Task<FastDFSMetadata> GetMetadataAsync(string fileId, CancellationToken cancellationToken = default);
    }
}
