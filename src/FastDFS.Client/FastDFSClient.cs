using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FastDFS.Client.Configuration;
using FastDFS.Client.Connection;
using FastDFS.Client.Exceptions;
using FastDFS.Client.Protocol;
using FastDFS.Client.Protocol.Requests;
using FastDFS.Client.Protocol.Responses;
using FastDFS.Client.Storage;
using FastDFS.Client.Tracker;
using FastDFS.Client.Utilities;

namespace FastDFS.Client
{
    /// <summary>
    /// Default implementation of IFastDFSClient.
    /// Represents a FastDFS cluster client that manages connections to multiple storage servers.
    /// Each FastDFSClient instance corresponds to one FastDFS cluster (with multiple trackers and storage servers).
    /// </summary>
    public class FastDFSClient : IFastDFSClient, IDisposable
    {
        private readonly ITrackerClient _trackerClient;
        private readonly ConnectionPoolConfiguration _poolOptions;
        private readonly ConcurrentDictionary<string, IConnectionPool> _storagePools;
        private readonly string _name;
        private readonly string? _defaultGroupName;
        private readonly StorageSelectionStrategy _selectionStrategy;
        private readonly IStorageSelector? _storageSelector;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSClient"/> class.
        /// </summary>
        /// <param name="trackerClient">The tracker client instance for querying storage servers.</param>
        /// <param name="poolOptions">The connection pool configuration for storage servers.</param>
        /// <param name="name">The name of this client instance (for multi-cluster scenarios). Default is "default".</param>
        /// <param name="defaultGroupName">Optional: The default group name to use when file IDs don't contain group names.</param>
        /// <param name="selectionStrategy">The storage server selection strategy. Default is TrackerSelection.</param>
        public FastDFSClient(
            ITrackerClient trackerClient,
            ConnectionPoolConfiguration poolOptions,
            string name = "default",
            string? defaultGroupName = null,
            StorageSelectionStrategy selectionStrategy = StorageSelectionStrategy.TrackerSelection)
        {
            _trackerClient = trackerClient ?? throw new ArgumentNullException(nameof(trackerClient));
            _poolOptions = poolOptions ?? throw new ArgumentNullException(nameof(poolOptions));
            _storagePools = new ConcurrentDictionary<string, IConnectionPool>();
            _name = name ?? "default";
            _defaultGroupName = defaultGroupName;
            _selectionStrategy = selectionStrategy;

            // Create storage selector based on strategy
            _storageSelector = selectionStrategy switch
            {
                StorageSelectionStrategy.FirstAvailable => new FirstAvailableStorageSelector(),
                StorageSelectionStrategy.Random => new RandomStorageSelector(),
                StorageSelectionStrategy.RoundRobin => new RoundRobinStorageSelector(),
                StorageSelectionStrategy.TrackerSelection => null, // Use tracker's selection
                _ => null
            };
        }

        /// <inheritdoc/>
        public string Name => _name;

        // ==================== Helper Methods for Storage Selection ====================

        /// <summary>
        /// Selects a storage server for upload based on the configured selection strategy.
        /// </summary>
        private async Task<StorageServerInfo> SelectStorageForUploadAsync(string? groupName, CancellationToken cancellationToken)
        {
            if (_selectionStrategy == StorageSelectionStrategy.TrackerSelection)
            {
                // Use tracker's server-side selection (most efficient)
                return await _trackerClient.QueryStorageForUploadAsync(groupName, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Query all available storages and apply client-side selection
                var storages = await _trackerClient.QueryAllStoragesForUploadAsync(groupName, cancellationToken).ConfigureAwait(false);
                if (storages == null || storages.Count == 0)
                    throw new FastDFSException("No available storage servers for upload.");

                if (_storageSelector == null)
                    throw new InvalidOperationException($"Storage selector not initialized for strategy: {_selectionStrategy}");

                return _storageSelector.Select(storages);
            }
        }

        /// <summary>
        /// Selects a storage server for download based on the configured selection strategy.
        /// </summary>
        private async Task<StorageServerInfo> SelectStorageForDownloadAsync(string groupName, string fileName, CancellationToken cancellationToken)
        {
            if (_selectionStrategy == StorageSelectionStrategy.TrackerSelection)
            {
                // Use tracker's server-side selection (most efficient)
                return await _trackerClient.QueryStorageForDownloadAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Query all available storages and apply client-side selection
                var storages = await _trackerClient.QueryAllStoragesForDownloadAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
                if (storages == null || storages.Count == 0)
                    throw new FastDFSException($"No available storage servers for downloading file: {groupName}/{fileName}");

                if (_storageSelector == null)
                    throw new InvalidOperationException($"Storage selector not initialized for strategy: {_selectionStrategy}");

                return _storageSelector.Select(storages);
            }
        }

        // ==================== Upload Operations ====================

        /// <inheritdoc/>
        public async Task<string> UploadAsync(string? groupName, byte[] content, string fileExtension, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (content == null || content.Length == 0)
                throw new ArgumentException("File content cannot be null or empty.", nameof(content));
            if (string.IsNullOrEmpty(fileExtension))
                throw new ArgumentException("File extension cannot be null or empty.", nameof(fileExtension));

            // Select storage server based on configured strategy
            var storageInfo = await SelectStorageForUploadAsync(groupName, cancellationToken).ConfigureAwait(false);

            // Get or create connection pool for this storage server
            var pool = GetOrCreateStoragePool(storageInfo.IpAddress, storageInfo.Port);
            var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var request = new UploadFileRequest
                {
                    StorePathIndex = storageInfo.StorePathIndex,
                    FileContent = content,
                    FileExtension = fileExtension
                };

                var response = await connection.SendRequestAsync<UploadFileRequest, UploadFileResponse>(request, cancellationToken).ConfigureAwait(false);

                // Return complete file ID: group_name/file_name
                return FileIdHelper.CombineFileId(response.GroupName, response.FileName);
            }
            finally
            {
                pool.ReturnConnection(connection);
            }
        }

        /// <inheritdoc/>
        public async Task<string> UploadAsync(string? groupName, Stream stream, string fileExtension, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
                throw new ArgumentException("Stream must be readable.", nameof(stream));

            // Read stream into byte array
            using (var memoryStream = new MemoryStream())
            {
                await stream.CopyToAsync(memoryStream, 81920, cancellationToken).ConfigureAwait(false);
                var content = memoryStream.ToArray();
                return await UploadAsync(groupName, content, fileExtension, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<string> UploadFileAsync(string? groupName, string localFilePath, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(localFilePath))
                throw new ArgumentException("Local file path cannot be null or empty.", nameof(localFilePath));
            if (!File.Exists(localFilePath))
                throw new FileNotFoundException("Local file not found.", localFilePath);

            // Read file content
            byte[] content;
            using (var fileStream = File.OpenRead(localFilePath))
            using (var memoryStream = new MemoryStream())
            {
                await fileStream.CopyToAsync(memoryStream, 81920, cancellationToken).ConfigureAwait(false);
                content = memoryStream.ToArray();
            }

            // Get file extension from file path
            var fileExtension = Path.GetExtension(localFilePath);
            if (!string.IsNullOrEmpty(fileExtension) && fileExtension.StartsWith("."))
                fileExtension = fileExtension.Substring(1); // Remove leading dot

            return await UploadAsync(groupName, content, fileExtension, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<string> UploadAppenderFileAsync(string? groupName, byte[] content, string fileExtension, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (content == null || content.Length == 0)
                throw new ArgumentException("File content cannot be null or empty.", nameof(content));
            if (string.IsNullOrEmpty(fileExtension))
                throw new ArgumentException("File extension cannot be null or empty.", nameof(fileExtension));

            // Select storage server based on configured strategy
            var storageInfo = await SelectStorageForUploadAsync(groupName, cancellationToken).ConfigureAwait(false);

            // Get or create connection pool for this storage server
            var pool = GetOrCreateStoragePool(storageInfo.IpAddress, storageInfo.Port);
            var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var request = new UploadAppenderFileRequest
                {
                    StorePathIndex = storageInfo.StorePathIndex,
                    FileContent = content,
                    FileExtension = fileExtension
                };

                var response = await connection.SendRequestAsync<UploadAppenderFileRequest, UploadFileResponse>(request, cancellationToken).ConfigureAwait(false);

                // Return complete file ID: group_name/file_name
                return FileIdHelper.CombineFileId(response.GroupName, response.FileName);
            }
            finally
            {
                pool.ReturnConnection(connection);
            }
        }

        // ==================== Download Operations ====================

        /// <inheritdoc/>
        public async Task<byte[]> DownloadAsync(string fileId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            return await DownloadAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<byte[]> DownloadAsync(string? groupName, string fileName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            NormalizeGroupAndFileName(ref groupName, ref fileName);

            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be null or empty.", nameof(groupName));
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            // Select storage server based on configured strategy
            var storageInfo = await SelectStorageForDownloadAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);

            // Get or create connection pool for this storage server
            var pool = GetOrCreateStoragePool(storageInfo.IpAddress, storageInfo.Port);
            var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var request = new DownloadFileRequest
                {
                    GroupName = groupName!,
                    FileName = fileName,
                    FileOffset = 0,
                    DownloadBytes = 0 // 0 means download entire file
                };

                var response = await connection.SendRequestAsync<DownloadFileRequest, DownloadFileResponse>(request, cancellationToken).ConfigureAwait(false);

                return response.Content;
            }
            finally
            {
                pool.ReturnConnection(connection);
            }
        }

        /// <inheritdoc/>
        public async Task DownloadAsync(string fileId, Stream outputStream, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));

            var content = await DownloadAsync(fileId, cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAsync(content, 0, content.Length, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task DownloadAsync(string? groupName, string fileName, Stream outputStream, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));

            var content = await DownloadAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAsync(content, 0, content.Length, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task DownloadFileAsync(string fileId, string localFilePath, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(localFilePath))
                throw new ArgumentException("Local file path cannot be null or empty.", nameof(localFilePath));

            var content = await DownloadAsync(fileId, cancellationToken).ConfigureAwait(false);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(localFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var fileStream = File.Create(localFilePath))
            {
                await fileStream.WriteAsync(content, 0, content.Length, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task DownloadFileAsync(string? groupName, string fileName, string localFilePath, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(localFilePath))
                throw new ArgumentException("Local file path cannot be null or empty.", nameof(localFilePath));

            var content = await DownloadAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(localFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var fileStream = File.Create(localFilePath))
            {
                await fileStream.WriteAsync(content, 0, content.Length, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<byte[]> DownloadAsync(string fileId, long offset, long length, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            return await DownloadAsync(groupName, fileName, offset, length, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<byte[]> DownloadAsync(string? groupName, string fileName, long offset, long length, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            NormalizeGroupAndFileName(ref groupName, ref fileName);

            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be null or empty.", nameof(groupName));
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative.");
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative.");

            // Select storage server based on configured strategy
            var storageInfo = await SelectStorageForDownloadAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);

            // Get or create connection pool for this storage server
            var pool = GetOrCreateStoragePool(storageInfo.IpAddress, storageInfo.Port);
            var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var request = new DownloadFileRequest
                {
                    GroupName = groupName!,
                    FileName = fileName,
                    FileOffset = offset,
                    DownloadBytes = length
                };

                var response = await connection.SendRequestAsync<DownloadFileRequest, DownloadFileResponse>(request, cancellationToken).ConfigureAwait(false);

                return response.Content;
            }
            finally
            {
                pool.ReturnConnection(connection);
            }
        }

        // ==================== Delete Operations ====================

        /// <inheritdoc/>
        public async Task DeleteAsync(string fileId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            await DeleteAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(string? groupName, string fileName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            NormalizeGroupAndFileName(ref groupName, ref fileName);

            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be null or empty.", nameof(groupName));
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            // Query tracker for update storage (delete requires update permission)
            var storageInfo = await _trackerClient.QueryStorageForUpdateAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);

            // Get or create connection pool for this storage server
            var pool = GetOrCreateStoragePool(storageInfo.IpAddress, storageInfo.Port);
            var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var request = new DeleteFileRequest
                {
                    GroupName = groupName!,
                    FileName = fileName
                };

                await connection.SendRequestAsync<DeleteFileRequest, DeleteFileResponse>(request, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                pool.ReturnConnection(connection);
            }
        }

        // ==================== Append Operations ====================

        /// <inheritdoc/>
        public async Task AppendFileAsync(string fileId, byte[] content, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            await AppendFileAsync(groupName, fileName, content, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task AppendFileAsync(string? groupName, string fileName, byte[] content, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            NormalizeGroupAndFileName(ref groupName, ref fileName);

            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be null or empty.", nameof(groupName));
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));
            if (content == null || content.Length == 0)
                throw new ArgumentException("Content cannot be null or empty.", nameof(content));

            // Query tracker for update storage (append requires update permission)
            var storageInfo = await _trackerClient.QueryStorageForUpdateAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);

            // Get or create connection pool for this storage server
            var pool = GetOrCreateStoragePool(storageInfo.IpAddress, storageInfo.Port);
            var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var request = new AppendFileRequest
                {
                    FileName = fileName,
                    Content = content
                };

                await connection.SendRequestAsync<AppendFileRequest, AppendFileResponse>(request, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                pool.ReturnConnection(connection);
            }
        }

        // ==================== Query Operations ====================

        /// <inheritdoc/>
        public async Task<FastDFSFileInfo> QueryFileInfoAsync(string fileId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            return await QueryFileInfoAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<FastDFSFileInfo> QueryFileInfoAsync(string? groupName, string fileName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            NormalizeGroupAndFileName(ref groupName, ref fileName);

            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be null or empty.", nameof(groupName));
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            // Select storage server based on configured strategy (query uses download permission)
            var storageInfo = await SelectStorageForDownloadAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);

            // Get or create connection pool for this storage server
            var pool = GetOrCreateStoragePool(storageInfo.IpAddress, storageInfo.Port);
            var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var request = new QueryFileInfoRequest
                {
                    GroupName = groupName!,
                    FileName = fileName
                };

                var response = await connection.SendRequestAsync<QueryFileInfoRequest, QueryFileInfoResponse>(request, cancellationToken).ConfigureAwait(false);

                if (response.FileInfo == null)
                    throw new FastDFSProtocolException("Storage server returned null file info.");

                return response.FileInfo;
            }
            finally
            {
                pool.ReturnConnection(connection);
            }
        }

        /// <inheritdoc/>
        public async Task<bool> FileExistsAsync(string fileId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            return await FileExistsAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<bool> FileExistsAsync(string? groupName, string fileName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            try
            {
                await QueryFileInfoAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (FastDFSException ex) when (ex.ErrorCode == 2) // ENOENT - file not found
            {
                return false;
            }
        }

        // ==================== Metadata Operations ====================

        /// <inheritdoc/>
        public async Task SetMetadataAsync(string fileId, FastDFSMetadata metadata, MetadataFlag flag = MetadataFlag.Overwrite, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            await SetMetadataAsync(groupName, fileName, metadata, flag, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SetMetadataAsync(string? groupName, string fileName, FastDFSMetadata metadata, MetadataFlag flag = MetadataFlag.Overwrite, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            NormalizeGroupAndFileName(ref groupName, ref fileName);

            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be determined. Please provide a valid group name or a complete file ID.", nameof(groupName));
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            // Query tracker for update storage
            var storageInfo = await _trackerClient.QueryStorageForUpdateAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);

            // Get or create connection pool for this storage server
            var pool = GetOrCreateStoragePool(storageInfo.IpAddress, storageInfo.Port);
            var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var request = new SetMetadataRequest
                {
                    GroupName = groupName!,
                    FileName = fileName,
                    Metadata = metadata,
                    Flag = flag
                };

                await connection.SendRequestAsync<SetMetadataRequest, SetMetadataResponse>(request, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                pool.ReturnConnection(connection);
            }
        }

        /// <inheritdoc/>
        public async Task<FastDFSMetadata> GetMetadataAsync(string fileId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            return await GetMetadataAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<FastDFSMetadata> GetMetadataAsync(string? groupName, string fileName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            NormalizeGroupAndFileName(ref groupName, ref fileName);

            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be determined. Please provide a valid group name or a complete file ID.", nameof(groupName));
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            // Query tracker for download storage (metadata is read-only operation, use download storage)
            var storageInfo = await SelectStorageForDownloadAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);

            // Get or create connection pool for this storage server
            var pool = GetOrCreateStoragePool(storageInfo.IpAddress, storageInfo.Port);
            var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var request = new GetMetadataRequest
                {
                    GroupName = groupName!,
                    FileName = fileName
                };

                var response = await connection.SendRequestAsync<GetMetadataRequest, GetMetadataResponse>(request, cancellationToken).ConfigureAwait(false);
                return response.Metadata;
            }
            finally
            {
                pool.ReturnConnection(connection);
            }
        }

        // ==================== Management Operations ====================

        /// <inheritdoc/>
        public async Task<List<GroupInfo>> ListAllGroupsAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return await _trackerClient.ListAllGroupsAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<List<StorageServerDetail>> ListStorageServersAsync(string groupName, string? storageServerId = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return await _trackerClient.ListStorageServersAsync(groupName, storageServerId, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Tracker Query Operations (Advanced) ====================

        /// <inheritdoc/>
        public async Task<StorageServerInfo> QueryStorageForUploadAsync(string? groupName = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return await _trackerClient.QueryStorageForUploadAsync(groupName, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<StorageServerInfo> QueryStorageForDownloadAsync(string fileId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            return await _trackerClient.QueryStorageForDownloadAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<StorageServerInfo> QueryStorageForDownloadAsync(string? groupName, string fileName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            NormalizeGroupAndFileName(ref groupName, ref fileName);

            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be null or empty.", nameof(groupName));
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            return await _trackerClient.QueryStorageForDownloadAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<StorageServerInfo> QueryStorageForUpdateAsync(string fileId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ParseFileIdWithDefault(fileId, out string groupName, out string fileName);
            return await _trackerClient.QueryStorageForUpdateAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<StorageServerInfo> QueryStorageForUpdateAsync(string? groupName, string fileName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            NormalizeGroupAndFileName(ref groupName, ref fileName);

            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be null or empty.", nameof(groupName));
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            return await _trackerClient.QueryStorageForUpdateAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);
        }

        // ==================== Helper Methods ====================

        /// <summary>
        /// Gets or creates a connection pool for a storage server.
        /// </summary>
        /// <param name="host">The storage server host.</param>
        /// <param name="port">The storage server port.</param>
        /// <returns>The connection pool.</returns>
        private IConnectionPool GetOrCreateStoragePool(string host, int port)
        {
            var key = $"{host}:{port}";
            return _storagePools.GetOrAdd(key, _ => new ConnectionPool(host, port, _poolOptions));
        }

        /// <summary>
        /// Parses a file ID using the default group name if the file ID doesn't contain a group name.
        /// </summary>
        /// <param name="fileId">The file ID to parse.</param>
        /// <param name="groupName">The extracted or default group name.</param>
        /// <param name="fileName">The extracted file name.</param>
        private void ParseFileIdWithDefault(string fileId, out string groupName, out string fileName)
        {
            FileIdHelper.ParseFileId(fileId, out groupName, out fileName, _defaultGroupName);
        }

        /// <summary>
        /// Normalizes group name and file name parameters.
        /// Handles cases where fileName might already contain the group name prefix.
        /// </summary>
        /// <param name="groupName">The group name (may be updated).</param>
        /// <param name="fileName">The file name (may be updated to remove group prefix).</param>
        private void NormalizeGroupAndFileName(ref string? groupName, ref string fileName)
        {
            // Check if fileName contains a group name prefix
            if (FileIdHelper.HasGroupName(fileName))
            {
                // fileName contains a group name, parse it
                FileIdHelper.ParseFileId(fileName, out string extractedGroupName, out string extractedFileName, _defaultGroupName);

                // Update parameters with extracted values
                groupName = extractedGroupName;
                fileName = extractedFileName;
            }
            else if (string.IsNullOrEmpty(groupName))
            {
                // fileName doesn't contain group name and groupName is not provided
                // Use default group name if available
                groupName = _defaultGroupName;
            }
        }

        /// <summary>
        /// Throws ObjectDisposedException if the client has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FastDFSClient));
        }

        /// <summary>
        /// Disposes the client and all storage connection pools.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Dispose tracker client
            try
            {
                if (_trackerClient is IDisposable disposableTracker)
                    disposableTracker.Dispose();
            }
            catch
            {
                // Suppress exceptions during disposal
            }

            // Dispose all storage connection pools
            foreach (var pool in _storagePools.Values)
            {
                try
                {
                    pool.Dispose();
                }
                catch
                {
                    // Suppress exceptions during disposal
                }
            }

            _storagePools.Clear();

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Returns a string representation of the client.
        /// </summary>
        public override string ToString()
        {
            return $"FastDFSClient [Name={_name}, StorageServers={_storagePools.Count}, DefaultGroup={_defaultGroupName ?? "none"}]";
        }
    }
}
