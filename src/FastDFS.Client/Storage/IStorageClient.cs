using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FastDFS.Client.Exceptions;
using FastDFS.Client.Protocol;
using FastDFS.Client.Tracker;

namespace FastDFS.Client.Storage
{
    /// <summary>
    /// Interface for FastDFS Storage client operations.
    /// Responsible for executing protocol commands against a specific storage server.
    /// The caller is responsible for resolving the correct storage server via the tracker
    /// before invoking any method on this interface.
    /// </summary>
    public interface IStorageClient
    {
        /// <summary>
        /// Uploads a file from a byte array to the specified storage server.
        /// </summary>
        /// <param name="server">The storage server information obtained from the tracker.</param>
        /// <param name="content">The file content as a byte array.</param>
        /// <param name="fileExtension">The file extension without the leading dot (e.g., "jpg", "txt").</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>The file ID in the format "group_name/filename".</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the storage client has been disposed.</exception>
        /// <exception cref="FastDFSException">Thrown when the upload operation fails.</exception>
        Task<string> UploadAsync(StorageServerInfo server, byte[] content, string fileExtension, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uploads an appender file from a byte array to the specified storage server.
        /// Appender files support appending data after the initial upload.
        /// </summary>
        /// <param name="server">The storage server information obtained from the tracker.</param>
        /// <param name="content">The file content as a byte array.</param>
        /// <param name="fileExtension">The file extension without the leading dot (e.g., "log", "txt").</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>The file ID in the format "group_name/filename".</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the storage client has been disposed.</exception>
        /// <exception cref="FastDFSException">Thrown when the upload operation fails.</exception>
        Task<string> UploadAppenderFileAsync(StorageServerInfo server, byte[] content, string fileExtension, CancellationToken cancellationToken = default);

        /// <summary>
        /// Appends data to an existing appender file on the specified storage server.
        /// </summary>
        /// <param name="server">The storage server information obtained from the tracker.</param>
        /// <param name="fileName">The name of the appender file to append data to.</param>
        /// <param name="content">The data to append to the file.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="ObjectDisposedException">Thrown when the storage client has been disposed.</exception>
        /// <exception cref="FastDFSException">Thrown when the append operation fails.</exception>
        Task AppendFileAsync(StorageServerInfo server, string fileName, byte[] content, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a portion of a file from the specified storage server.
        /// </summary>
        /// <param name="server">The storage server information obtained from the tracker.</param>
        /// <param name="groupName">The name of the storage group where the file is located.</param>
        /// <param name="fileName">The name of the file to download.</param>
        /// <param name="offset">The byte offset from which to start downloading. Use 0 to start from the beginning.</param>
        /// <param name="length">The number of bytes to download. Use 0 to download the entire file.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>The downloaded file content as a byte array.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the storage client has been disposed.</exception>
        /// <exception cref="FastDFSException">Thrown when the download operation fails.</exception>
        Task<byte[]> DownloadAsync(StorageServerInfo server, string groupName, string fileName, long offset, long length, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a file from the specified storage server.
        /// </summary>
        /// <param name="server">The storage server information obtained from the tracker.</param>
        /// <param name="groupName">The name of the storage group where the file is located.</param>
        /// <param name="fileName">The name of the file to delete.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="ObjectDisposedException">Thrown when the storage client has been disposed.</exception>
        /// <exception cref="FastDFSException">Thrown when the delete operation fails.</exception>
        Task DeleteAsync(StorageServerInfo server, string groupName, string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries file information from the specified storage server.
        /// </summary>
        /// <param name="server">The storage server information obtained from the tracker.</param>
        /// <param name="groupName">The name of the storage group where the file is located.</param>
        /// <param name="fileName">The name of the file to query.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A <see cref="FastDFSFileInfo"/> object containing file metadata such as file size and creation time.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the storage client has been disposed.</exception>
        /// <exception cref="FastDFSException">Thrown when the query operation fails.</exception>
        Task<FastDFSFileInfo> QueryFileInfoAsync(StorageServerInfo server, string groupName, string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets metadata for a file on the specified storage server.
        /// </summary>
        /// <param name="server">The storage server information obtained from the tracker.</param>
        /// <param name="groupName">The name of the storage group where the file is located.</param>
        /// <param name="fileName">The name of the file to set metadata for.</param>
        /// <param name="metadata">The metadata to set.</param>
        /// <param name="flag">The metadata operation flag indicating whether to overwrite or merge with existing metadata.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="ObjectDisposedException">Thrown when the storage client has been disposed.</exception>
        /// <exception cref="FastDFSException">Thrown when the set metadata operation fails.</exception>
        Task SetMetadataAsync(StorageServerInfo server, string groupName, string fileName, FastDFSMetadata metadata, MetadataFlag flag, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets metadata for a file from the specified storage server.
        /// </summary>
        /// <param name="server">The storage server information obtained from the tracker.</param>
        /// <param name="groupName">The name of the storage group where the file is located.</param>
        /// <param name="fileName">The name of the file to get metadata for.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A <see cref="FastDFSMetadata"/> object containing the file metadata.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the storage client has been disposed.</exception>
        /// <exception cref="FastDFSException">Thrown when the get metadata operation fails.</exception>
        Task<FastDFSMetadata> GetMetadataAsync(StorageServerInfo server, string groupName, string fileName, CancellationToken cancellationToken = default);
    }
}
