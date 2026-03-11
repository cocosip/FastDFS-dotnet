using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FastDFS.Client.Configuration;
using FastDFS.Client.Exceptions;
using FastDFS.Client.Protocol;
using FastDFS.Client.Storage;
using FastDFS.Client.Tracker;
using FastDFS.Client.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FastDFS.Client
{
    /// <summary>
    /// Default implementation of IFastDFSClient.
    /// Coordinates tracker queries and storage operations for a single FastDFS cluster.
    /// </summary>
    public class FastDFSClient : IFastDFSClient, IDisposable
    {
        private readonly ITrackerClient _trackerClient;
        private readonly IStorageClient _storageClient;
        private readonly ILogger _logger;
        private readonly string _name;
        private readonly string? _defaultGroupName;
        private readonly StorageSelectionStrategy _selectionStrategy;
        private readonly IStorageSelector? _storageSelector;
        private readonly HttpConfiguration? _httpConfig;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSClient"/> class.
        /// </summary>
        /// <param name="trackerClient">The tracker client for querying storage servers.</param>
        /// <param name="storageClient">The storage client for executing storage operations.</param>
        /// <param name="name">The name of this client instance (for multi-cluster scenarios).</param>
        /// <param name="defaultGroupName">The default storage group name to use when not specified.</param>
        /// <param name="selectionStrategy">The storage selection strategy for client-side load balancing.</param>
        /// <param name="httpConfig">Optional HTTP configuration for generating file access URLs.</param>
        /// <param name="loggerFactory">Optional logger factory for creating loggers.</param>
        /// <exception cref="ArgumentNullException">Thrown when trackerClient or storageClient is null.</exception>
        public FastDFSClient(
            ITrackerClient trackerClient,
            IStorageClient storageClient,
            string name = "default",
            string? defaultGroupName = null,
            StorageSelectionStrategy selectionStrategy = StorageSelectionStrategy.TrackerSelection,
            HttpConfiguration? httpConfig = null,
            ILoggerFactory? loggerFactory = null)
        {
            _trackerClient = trackerClient ?? throw new ArgumentNullException(nameof(trackerClient));
            _storageClient = storageClient ?? throw new ArgumentNullException(nameof(storageClient));
            _logger = loggerFactory?.CreateLogger<FastDFSClient>() ?? NullLogger<FastDFSClient>.Instance;
            _name = name ?? "default";
            _defaultGroupName = defaultGroupName;
            _selectionStrategy = selectionStrategy;
            _httpConfig = httpConfig;

            _storageSelector = selectionStrategy switch
            {
                StorageSelectionStrategy.FirstAvailable => new FirstAvailableStorageSelector(),
                StorageSelectionStrategy.Random => new RandomStorageSelector(),
                StorageSelectionStrategy.RoundRobin => new RoundRobinStorageSelector(),
                _ => null // TrackerSelection: tracker picks for us
            };

            _logger.LogInformation("FastDFSClient '{Name}' initialized with strategy: {Strategy}, default group: {DefaultGroup}",
                _name, _selectionStrategy, _defaultGroupName ?? "(none)");
        }

        /// <summary>
        /// Gets the name of this client instance (for multi-cluster scenarios).
        /// </summary>
        public string Name => _name;

        /// <summary>
        /// Gets the underlying tracker client for advanced low-level tracker operations.
        /// </summary>
        public ITrackerClient TrackerClient => _trackerClient;

        /// <summary>
        /// Gets the underlying storage client for advanced low-level storage operations.
        /// </summary>
        public IStorageClient StorageClient => _storageClient;

        // ==================== Storage Selection ====================

        private async Task<StorageServerInfo> SelectStorageForUploadAsync(
            string? groupName, 
            CancellationToken cancellationToken)
        {
            if (_selectionStrategy == StorageSelectionStrategy.TrackerSelection)
                return await _trackerClient.QueryStorageForUploadAsync(groupName, cancellationToken).ConfigureAwait(false);

            var storages = await _trackerClient.QueryAllStoragesForUploadAsync(groupName, cancellationToken).ConfigureAwait(false);
            if (storages == null || storages.Count == 0)
                throw new FastDFSException("No available storage servers for upload.");

            return _storageSelector!.Select(storages);
        }

        private async Task<StorageServerInfo> SelectStorageForDownloadAsync(
            string groupName, 
            string fileName, 
            CancellationToken cancellationToken)
        {
            if (_selectionStrategy == StorageSelectionStrategy.TrackerSelection)
                return await _trackerClient.QueryStorageForDownloadAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);

            var storages = await _trackerClient.QueryAllStoragesForDownloadAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
            if (storages == null || storages.Count == 0)
                throw new FastDFSException($"No available storage servers for file: {groupName}/{fileName}");

            return _storageSelector!.Select(storages);
        }

        // ==================== Upload Operations ====================

        /// <summary>
        /// Uploads a file from a byte array to the FastDFS cluster.
        /// Automatically queries the tracker for an available storage server based on the configured selection strategy.
        /// </summary>
        /// <param name="groupName">Optional: The storage group name. If null, the tracker will select a group automatically.</param>
        /// <param name="content">The file content as a byte array.</param>
        /// <param name="fileExtension">The file extension without the leading dot (e.g., "jpg", "txt").</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>The file ID in the format "group_name/filename".</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <exception cref="ArgumentException">Thrown when content is null/empty or fileExtension is null/empty.</exception>
        /// <exception cref="FastDFSException">Thrown when the upload operation fails.</exception>
        public async Task<string> UploadAsync(
            string? groupName, 
            byte[] content, 
            string fileExtension, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (content == null || content.Length == 0)
                throw new ArgumentException("File content cannot be null or empty.", nameof(content));
            if (string.IsNullOrEmpty(fileExtension))
                throw new ArgumentException("File extension cannot be null or empty.", nameof(fileExtension));

            _logger.LogInformation("Uploading file to group '{GroupName}', size={Size} bytes, extension={Extension}",
                groupName ?? "(auto-select)", content.Length, fileExtension);

            var server = await SelectStorageForUploadAsync(groupName, cancellationToken).ConfigureAwait(false);
            var fileId = await _storageClient.UploadAsync(server, content, fileExtension, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully uploaded file: {FileId}", fileId);
            return fileId;
        }

        /// <summary>
        /// Uploads a file from a stream to the FastDFS cluster.
        /// Automatically queries the tracker for an available storage server based on the configured selection strategy.
        /// </summary>
        /// <param name="groupName">Optional: The storage group name. If null, the tracker will select a group automatically.</param>
        /// <param name="stream">The file content as a stream. Must be readable.</param>
        /// <param name="fileExtension">The file extension without the leading dot (e.g., "jpg", "txt").</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>The file ID in the format "group_name/filename".</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
        /// <exception cref="ArgumentException">Thrown when stream is not readable or fileExtension is null/empty.</exception>
        /// <exception cref="FastDFSException">Thrown when the upload operation fails.</exception>
        public async Task<string> UploadAsync(
            string? groupName, 
            Stream stream, 
            string fileExtension, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
                throw new ArgumentException("Stream must be readable.", nameof(stream));

            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, 81920, cancellationToken).ConfigureAwait(false);
            return await UploadAsync(groupName, memoryStream.ToArray(), fileExtension, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Uploads a file from a local file path to the FastDFS cluster.
        /// Automatically queries the tracker for an available storage server based on the configured selection strategy.
        /// </summary>
        /// <param name="groupName">Optional: The storage group name. If null, the tracker will select a group automatically.</param>
        /// <param name="localFilePath">The local file path to upload.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>The file ID in the format "group_name/filename".</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <exception cref="ArgumentException">Thrown when localFilePath is null or empty.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the local file does not exist.</exception>
        /// <exception cref="FastDFSException">Thrown when the upload operation fails.</exception>
        public async Task<string> UploadFileAsync(
            string? groupName, 
            string localFilePath, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(localFilePath))
                throw new ArgumentException("Local file path cannot be null or empty.", nameof(localFilePath));
            if (!File.Exists(localFilePath))
                throw new FileNotFoundException("Local file not found.", localFilePath);

            byte[] content;
            using (var fileStream = File.OpenRead(localFilePath))
            using (var memoryStream = new MemoryStream())
            {
                await fileStream.CopyToAsync(memoryStream, 81920, cancellationToken).ConfigureAwait(false);
                content = memoryStream.ToArray();
            }

            var fileExtension = Path.GetExtension(localFilePath).TrimStart('.');
            return await UploadAsync(groupName, content, fileExtension, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Uploads an appender file from a byte array to the FastDFS cluster.
        /// Appender files support appending data after the initial upload.
        /// Automatically queries the tracker for an available storage server based on the configured selection strategy.
        /// </summary>
        /// <param name="groupName">Optional: The storage group name. If null, the tracker will select a group automatically.</param>
        /// <param name="content">The file content as a byte array.</param>
        /// <param name="fileExtension">The file extension without the leading dot (e.g., "log", "txt").</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>The file ID in the format "group_name/filename".</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <exception cref="ArgumentException">Thrown when content is null/empty or fileExtension is null/empty.</exception>
        /// <exception cref="FastDFSException">Thrown when the upload operation fails.</exception>
        public async Task<string> UploadAppenderFileAsync(
            string? groupName, 
            byte[] content, 
            string fileExtension, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (content == null || content.Length == 0)
                throw new ArgumentException("File content cannot be null or empty.", nameof(content));
            if (string.IsNullOrEmpty(fileExtension))
                throw new ArgumentException("File extension cannot be null or empty.", nameof(fileExtension));

            var server = await SelectStorageForUploadAsync(groupName, cancellationToken).ConfigureAwait(false);
            return await _storageClient.UploadAppenderFileAsync(server, content, fileExtension, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Download Operations ====================

        /// <summary>
        /// Downloads a file as a byte array from the FastDFS cluster.
        /// Automatically queries the tracker for the storage server location.
        /// </summary>
        /// <param name="fileId">The file ID in the format "group_name/filename".</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>The downloaded file content as a byte array.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <exception cref="FastDFSException">Thrown when the download operation fails.</exception>
        public async Task<byte[]> DownloadAsync(
            string fileId, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            return await DownloadCoreAsync(groupName, fileName, 0, 0, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Downloads a file as a byte array from the FastDFS cluster.
        /// Automatically queries the tracker for the storage server location.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID.</param>
        /// <param name="fileName">The file name or complete file ID in the format "group_name/filename".</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>The downloaded file content as a byte array.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <exception cref="ArgumentException">Thrown when groupName or fileName is null/empty.</exception>
        /// <exception cref="FastDFSException">Thrown when the download operation fails.</exception>
        public async Task<byte[]> DownloadAsync(
            string? groupName, 
            string fileName, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ResolveGroupAndFileName(ref groupName, ref fileName);
            ValidateGroupAndFileName(groupName, fileName);
            return await DownloadCoreAsync(groupName!, fileName, 0, 0, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Downloads a file to a stream from the FastDFS cluster.
        /// Automatically queries the tracker for the storage server location.
        /// </summary>
        /// <param name="fileId">The file ID in the format "group_name/filename".</param>
        /// <param name="outputStream">The output stream to write the downloaded content.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown when outputStream is null.</exception>
        /// <exception cref="FastDFSException">Thrown when the download operation fails.</exception>
        public async Task DownloadAsync(
            string fileId, 
            Stream outputStream, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (outputStream == null) throw new ArgumentNullException(nameof(outputStream));
            var content = await DownloadAsync(fileId, cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAsync(content, 0, content.Length, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Downloads a file to a stream from the FastDFS cluster.
        /// Automatically queries the tracker for the storage server location.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID.</param>
        /// <param name="fileName">The file name or complete file ID in the format "group_name/filename".</param>
        /// <param name="outputStream">The output stream to write the downloaded content.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown when outputStream is null.</exception>
        /// <exception cref="ArgumentException">Thrown when groupName or fileName is null/empty.</exception>
        /// <exception cref="FastDFSException">Thrown when the download operation fails.</exception>
        public async Task DownloadAsync(
            string? groupName, 
            string fileName, 
            Stream outputStream, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (outputStream == null) throw new ArgumentNullException(nameof(outputStream));
            var content = await DownloadAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAsync(content, 0, content.Length, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Downloads a file to a local file path from the FastDFS cluster.
        /// Automatically queries the tracker for the storage server location.
        /// </summary>
        /// <param name="fileId">The file ID in the format "group_name/filename".</param>
        /// <param name="localFilePath">The local file path to save the downloaded file.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <exception cref="ArgumentException">Thrown when localFilePath is null or empty.</exception>
        /// <exception cref="FastDFSException">Thrown when the download operation fails.</exception>
        public async Task DownloadFileAsync(
            string fileId, 
            string localFilePath, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(localFilePath))
                throw new ArgumentException("Local file path cannot be null or empty.", nameof(localFilePath));

            var content = await DownloadAsync(fileId, cancellationToken).ConfigureAwait(false);
            await WriteToFileAsync(content, localFilePath, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Downloads a file to a local file path from the FastDFS cluster.
        /// Automatically queries the tracker for the storage server location.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID.</param>
        /// <param name="fileName">The file name or complete file ID in the format "group_name/filename".</param>
        /// <param name="localFilePath">The local file path to save the downloaded file.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <exception cref="ArgumentException">Thrown when localFilePath is null or empty.</exception>
        /// <exception cref="FastDFSException">Thrown when the download operation fails.</exception>
        public async Task DownloadFileAsync(
            string? groupName, 
            string fileName, 
            string localFilePath, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(localFilePath))
                throw new ArgumentException("Local file path cannot be null or empty.", nameof(localFilePath));

            var content = await DownloadAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
            await WriteToFileAsync(content, localFilePath, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Downloads a portion of a file as a byte array from the FastDFS cluster.
        /// Automatically queries the tracker for the storage server location.
        /// </summary>
        /// <param name="fileId">The file ID in the format "group_name/filename".</param>
        /// <param name="offset">The byte offset from which to start downloading.</param>
        /// <param name="length">The number of bytes to download.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>The downloaded file content as a byte array.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when offset or length is negative.</exception>
        /// <exception cref="FastDFSException">Thrown when the download operation fails.</exception>
        public async Task<byte[]> DownloadAsync(
            string fileId, 
            long offset, 
            long length, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            return await DownloadCoreAsync(groupName, fileName, offset, length, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Downloads a portion of a file as a byte array from the FastDFS cluster.
        /// Automatically queries the tracker for the storage server location.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID.</param>
        /// <param name="fileName">The file name or complete file ID in the format "group_name/filename".</param>
        /// <param name="offset">The byte offset from which to start downloading.</param>
        /// <param name="length">The number of bytes to download.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>The downloaded file content as a byte array.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <exception cref="ArgumentException">Thrown when groupName or fileName is null/empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when offset or length is negative.</exception>
        /// <exception cref="FastDFSException">Thrown when the download operation fails.</exception>
        public async Task<byte[]> DownloadAsync(
            string? groupName, 
            string fileName, 
            long offset, 
            long length, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ResolveGroupAndFileName(ref groupName, ref fileName);
            ValidateGroupAndFileName(groupName, fileName);
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative.");
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative.");
            return await DownloadCoreAsync(groupName!, fileName, offset, length, cancellationToken).ConfigureAwait(false);
        }

        private async Task<byte[]> DownloadCoreAsync(
            string groupName, 
            string fileName, 
            long offset, 
            long length, 
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Downloading file: group={GroupName}, file={FileName}", groupName, fileName);
            var server = await SelectStorageForDownloadAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
            var content = await _storageClient.DownloadAsync(server, groupName, fileName, offset, length, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Successfully downloaded file: group={GroupName}, file={FileName}, size={Size} bytes", groupName, fileName, content.Length);
            return content;
        }

        // ==================== Append Operations ====================

        /// <summary>
        /// Appends data to an existing appender file.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="fileId">The complete file ID of the appender file in the format "group_name/path/filename".</param>
        /// <param name="content">The content to append.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task AppendFileAsync(
            string fileId,
            byte[] content,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            await AppendFileCoreAsync(groupName, fileName, content, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Appends data to an existing appender file.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="content">The content to append.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task AppendFileAsync(
            string? groupName,
            string fileName,
            byte[] content,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ResolveGroupAndFileName(ref groupName, ref fileName);
            ValidateGroupAndFileName(groupName, fileName);
            if (content == null || content.Length == 0)
                throw new ArgumentException("Content cannot be null or empty.", nameof(content));
            await AppendFileCoreAsync(groupName!, fileName, content, cancellationToken).ConfigureAwait(false);
        }

        private async Task AppendFileCoreAsync(string groupName, string fileName, byte[] content, CancellationToken cancellationToken)
        {
            var server = await _trackerClient.QueryStorageForUpdateAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
            await _storageClient.AppendFileAsync(server, fileName, content, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Delete Operations ====================

        /// <summary>
        /// Deletes a file from the storage server.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task DeleteAsync(
            string fileId,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            await DeleteCoreAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a file from the storage server.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task DeleteAsync(
            string? groupName,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ResolveGroupAndFileName(ref groupName, ref fileName);
            ValidateGroupAndFileName(groupName, fileName);
            await DeleteCoreAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);
        }

        private async Task DeleteCoreAsync(
            string groupName,
            string fileName,
            CancellationToken cancellationToken)
        {
            var server = await _trackerClient.QueryStorageForUpdateAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
            await _storageClient.DeleteAsync(server, groupName, fileName, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Query Operations ====================

        /// <summary>
        /// Queries file information.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file information.</returns>
        public async Task<FastDFSFileInfo> QueryFileInfoAsync(
            string fileId,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            return await QueryFileInfoCoreAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Queries file information.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file information.</returns>
        public async Task<FastDFSFileInfo> QueryFileInfoAsync(
            string? groupName,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ResolveGroupAndFileName(ref groupName, ref fileName);
            ValidateGroupAndFileName(groupName, fileName);
            return await QueryFileInfoCoreAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);
        }

        private async Task<FastDFSFileInfo> QueryFileInfoCoreAsync(
            string groupName,
            string fileName,
            CancellationToken cancellationToken)
        {
            var server = await SelectStorageForDownloadAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
            return await _storageClient.QueryFileInfoAsync(server, groupName, fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if a file exists.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the file exists; otherwise, false.</returns>
        public async Task<bool> FileExistsAsync(
            string fileId,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            return await FileExistsAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if a file exists.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the file exists; otherwise, false.</returns>
        public async Task<bool> FileExistsAsync(
            string? groupName,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            try
            {
                await QueryFileInfoAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (FastDFSException ex) when (ex.ErrorCode == 2) // ENOENT
            {
                return false;
            }
        }

        // ==================== Metadata Operations ====================

        /// <summary>
        /// Sets metadata for a file using complete file ID.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="metadata">The metadata to set.</param>
        /// <param name="flag">The metadata operation flag (Overwrite or Merge).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SetMetadataAsync(
            string fileId,
            FastDFSMetadata metadata,
            MetadataFlag flag = MetadataFlag.Overwrite,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            await SetMetadataCoreAsync(groupName, fileName, metadata, flag, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets metadata for a file.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="metadata">The metadata to set.</param>
        /// <param name="flag">The metadata operation flag (Overwrite or Merge).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SetMetadataAsync(
            string? groupName,
            string fileName,
            FastDFSMetadata metadata,
            MetadataFlag flag = MetadataFlag.Overwrite,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ResolveGroupAndFileName(ref groupName, ref fileName);
            ValidateGroupAndFileName(groupName, fileName);
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            await SetMetadataCoreAsync(groupName!, fileName, metadata, flag, cancellationToken).ConfigureAwait(false);
        }

        private async Task SetMetadataCoreAsync(
            string groupName,
            string fileName,
            FastDFSMetadata metadata,
            MetadataFlag flag,
            CancellationToken cancellationToken)
        {
            var server = await _trackerClient.QueryStorageForUpdateAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
            await _storageClient.SetMetadataAsync(server, groupName, fileName, metadata, flag, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets metadata for a file using complete file ID.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file metadata.</returns>
        public async Task<FastDFSMetadata> GetMetadataAsync(
            string fileId,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            return await GetMetadataCoreAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets metadata for a file.
        /// Automatically queries tracker for the storage server location.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file metadata.</returns>
        public async Task<FastDFSMetadata> GetMetadataAsync(
            string? groupName,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ResolveGroupAndFileName(ref groupName, ref fileName);
            ValidateGroupAndFileName(groupName, fileName);
            return await GetMetadataCoreAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);
        }

        private async Task<FastDFSMetadata> GetMetadataCoreAsync(
            string groupName,
            string fileName,
            CancellationToken cancellationToken)
        {
            var server = await SelectStorageForDownloadAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
            return await _storageClient.GetMetadataAsync(server, groupName, fileName, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Management Operations ====================

        /// <summary>
        /// Lists all storage groups in the cluster.
        /// This is a management operation used for monitoring, scheduling, and cluster administration.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of all storage groups with their information (disk usage, server count, etc.).</returns>
        public async Task<List<GroupInfo>> ListAllGroupsAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return await _trackerClient.ListAllGroupsAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists all storage servers in a specific group.
        /// This is a management operation used for monitoring, scheduling, and cluster administration.
        /// </summary>
        /// <param name="groupName">The storage group name.</param>
        /// <param name="storageServerId">Optional: specific storage server ID (IP address) to query. If null, returns all servers in the group.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of storage servers in the specified group with detailed status information.</returns>
        public async Task<List<StorageServerDetail>> ListStorageServersAsync(
            string groupName,
            string? storageServerId = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return await _trackerClient.ListStorageServersAsync(groupName, storageServerId, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Tracker Query Operations (Advanced) ====================

        /// <summary>
        /// Queries tracker for an available storage server for uploading.
        /// This is an advanced method that allows you to control the upload process manually.
        /// </summary>
        /// <param name="groupName">Optional: The storage group name. If null, tracker will select a group automatically.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Information about the storage server to use for upload.</returns>
        public async Task<StorageServerInfo> QueryStorageForUploadAsync(
            string? groupName = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return await _trackerClient.QueryStorageForUploadAsync(groupName, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Queries tracker for the storage server that contains the specified file.
        /// This is an advanced method that allows you to get storage server information.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Information about the storage server that contains the file.</returns>
        public async Task<StorageServerInfo> QueryStorageForDownloadAsync(
            string fileId,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            return await _trackerClient.QueryStorageForDownloadAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Queries tracker for the storage server that contains the specified file.
        /// This is an advanced method that allows you to get storage server information.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Information about the storage server that contains the file.</returns>
        public async Task<StorageServerInfo> QueryStorageForDownloadAsync(
            string? groupName,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ResolveGroupAndFileName(ref groupName, ref fileName);
            ValidateGroupAndFileName(groupName, fileName);
            return await _trackerClient.QueryStorageForDownloadAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Queries tracker for the storage server that can update/delete the specified file.
        /// This is an advanced method that allows you to get storage server information.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Information about the storage server that can modify the file.</returns>
        public async Task<StorageServerInfo> QueryStorageForUpdateAsync(
            string fileId,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            return await _trackerClient.QueryStorageForUpdateAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Queries tracker for the storage server that can update/delete the specified file.
        /// This is an advanced method that allows you to get storage server information.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Information about the storage server that can modify the file.</returns>
        public async Task<StorageServerInfo> QueryStorageForUpdateAsync(
            string? groupName,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ResolveGroupAndFileName(ref groupName, ref fileName);
            ValidateGroupAndFileName(groupName, fileName);
            return await _trackerClient.QueryStorageForUpdateAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);
        }

        // ==================== HTTP URL Operations ====================

        /// <summary>
        /// Generates an HTTP access URL for a file.
        /// This requires FastDFS Nginx module to be installed and configured on storage servers.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="attachmentFilename">Optional: Custom filename for download (Content-Disposition header). If null, uses original filename.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>HTTP URL for accessing the file (e.g., "http://192.168.1.100/group1/M00/00/00/xxxxx.jpg").</returns>
        /// <exception cref="InvalidOperationException">Thrown when HTTP configuration is not enabled.</exception>
        public Task<string> GetFileUrlAsync(
            string fileId,
            string? attachmentFilename = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureHttpConfig();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            return GetFileUrlAsync(groupName, fileName, attachmentFilename, cancellationToken);
        }

        /// <summary>
        /// Generates an HTTP access URL for a file.
        /// This requires FastDFS Nginx module to be installed and configured on storage servers.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="attachmentFilename">Optional: Custom filename for download (Content-Disposition header). If null, uses original filename.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>HTTP URL for accessing the file.</returns>
        /// <exception cref="InvalidOperationException">Thrown when HTTP configuration is not enabled.</exception>
        public async Task<string> GetFileUrlAsync(
            string? groupName,
            string fileName,
            string? attachmentFilename = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureHttpConfig();
            ResolveGroupAndFileName(ref groupName, ref fileName);
            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be determined. Provide groupName or configure DefaultGroupName.", nameof(groupName));

            var storageInfo = await _trackerClient.QueryStorageForDownloadAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);
            string baseUrl = _httpConfig!.GetServerUrl(groupName!, storageInfo.IpAddress);
            string filePath = $"/{groupName}/{fileName.TrimStart('/')}";
            string url = baseUrl + filePath;

            if (!string.IsNullOrWhiteSpace(attachmentFilename))
                url += $"?attname={Uri.EscapeDataString(attachmentFilename)}";

            return url;
        }


        /// <summary>
        /// Generates an HTTP access URL with anti-steal token.
        /// This requires FastDFS Nginx module with anti-steal feature enabled.
        /// The URL will be valid until the specified expiration time.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="expireSeconds">Token expiration time in seconds from now. If null, uses configuration default.</param>
        /// <param name="attachmentFilename">Optional: Custom filename for download. If null, uses original filename.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>HTTP URL with token and timestamp parameters (e.g., "http://192.168.1.100/group1/M00/00/00/xxxxx.jpg?token=xxx&amp;ts=1234567890").</returns>
        /// <exception cref="InvalidOperationException">Thrown when HTTP configuration or anti-steal token is not enabled.</exception>
        public Task<string> GetFileUrlWithTokenAsync(
            string fileId,
            int? expireSeconds = null,
            string? attachmentFilename = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureHttpConfig();
            EnsureAntiStealToken();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            return GetFileUrlWithTokenAsync(groupName, fileName, expireSeconds, attachmentFilename, cancellationToken);
        }


        /// <summary>
        /// Generates an HTTP access URL with anti-steal token.
        /// This requires FastDFS Nginx module with anti-steal feature enabled.
        /// </summary>
        /// <param name="groupName">The storage group name. Can be null if fileName contains the complete file ID with group name.</param>
        /// <param name="fileName">The file name (path on storage server), or complete file ID in format "group_name/path/filename".</param>
        /// <param name="expireSeconds">Token expiration time in seconds from now. If null, uses configuration default.</param>
        /// <param name="attachmentFilename">Optional: Custom filename for download. If null, uses original filename.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>HTTP URL with token and timestamp parameters.</returns>
        /// <exception cref="InvalidOperationException">Thrown when HTTP configuration or anti-steal token is not enabled.</exception>
        public async Task<string> GetFileUrlWithTokenAsync(
            string? groupName,
            string fileName,
            int? expireSeconds = null,
            string? attachmentFilename = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureHttpConfig();
            EnsureAntiStealToken();
            ResolveGroupAndFileName(ref groupName, ref fileName);
            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be determined. Provide groupName or configure DefaultGroupName.", nameof(groupName));

            var storageInfo = await _trackerClient.QueryStorageForDownloadAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);
            string baseUrl = _httpConfig!.GetServerUrl(groupName!, storageInfo.IpAddress);
            string fileIdForToken = $"{groupName}/{fileName.TrimStart('/')}";

            int actualExpireSeconds = expireSeconds ?? _httpConfig.DefaultTokenExpireSeconds;
            var (token, timestamp) = Utilities.TokenGenerator.GenerateTokenWithExpire(
                fileIdForToken,
                _httpConfig.SecretKey!,
                actualExpireSeconds);

            string url = $"{baseUrl}/{fileIdForToken}?token={token}&ts={timestamp}";

            if (!string.IsNullOrWhiteSpace(attachmentFilename))
                url += $"&attname={Uri.EscapeDataString(attachmentFilename)}";

            return url;
        }

        // ==================== Helpers ====================

        private void ParseFileIdWithDefault(string fileId, out string groupName, out string fileName) =>
            FileIdHelper.ParseFileId(fileId, out groupName, out fileName, _defaultGroupName);

        private void ResolveGroupAndFileName(ref string? groupName, ref string fileName)
        {
            if (FileIdHelper.HasGroupName(fileName))
            {
                FileIdHelper.ParseFileId(fileName, out string extractedGroup, out string extractedFile, _defaultGroupName);
                groupName = extractedGroup;
                fileName = extractedFile;
            }
            else if (string.IsNullOrEmpty(groupName))
            {
                groupName = _defaultGroupName;
            }
        }

        private static void ValidateGroupAndFileName(string? groupName, string fileName)
        {
            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be null or empty.", nameof(groupName));
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));
        }

        private static async Task WriteToFileAsync(byte[] content, string localFilePath, CancellationToken cancellationToken)
        {
            var directory = Path.GetDirectoryName(localFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using var fileStream = File.Create(localFilePath);
            await fileStream.WriteAsync(content, 0, content.Length, cancellationToken).ConfigureAwait(false);
        }

        private void EnsureHttpConfig()
        {
            if (_httpConfig == null)
                throw new InvalidOperationException("HTTP configuration is not enabled. Please configure HttpConfig in FastDFSConfiguration.");
        }

        private void EnsureAntiStealToken()
        {
            if (!_httpConfig!.AntiStealTokenEnabled)
                throw new InvalidOperationException("Anti-steal token is not enabled. Set AntiStealTokenEnabled to true in HttpConfiguration.");
            if (string.IsNullOrWhiteSpace(_httpConfig.SecretKey))
                throw new InvalidOperationException("Secret key is not configured. Set SecretKey in HttpConfiguration.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FastDFSClient));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer — ensures TCP connections are released even if Dispose() is never called.
        /// </summary>
        ~FastDFSClient() => Dispose(disposing: false);

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            _disposed = true;

            try { (_trackerClient as IDisposable)?.Dispose(); } catch { }
            try { (_storageClient as IDisposable)?.Dispose(); } catch { }
        }
    }
}
