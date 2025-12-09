using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FastDFS.Client.Configuration;
using FastDFS.Client.Connection;
using FastDFS.Client.Protocol;
using FastDFS.Client.Protocol.Requests;
using FastDFS.Client.Protocol.Responses;
using FastDFS.Client.Tracker;
using FastDFS.Client.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FastDFS.Client.Storage
{
    /// <summary>
    /// FastDFS Storage client for file operations.
    /// NOTE: This class is not typically used directly. Use IFastDFSClient instead.
    /// </summary>
    public class StorageClient : IStorageClient, IDisposable
    {
        private readonly ITrackerClient _trackerClient;
        private readonly ConnectionPoolConfiguration _poolOptions;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, IConnectionPool> _storagePools;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageClient"/> class.
        /// </summary>
        /// <param name="trackerClient">The tracker client.</param>
        /// <param name="poolOptions">Connection pool options.</param>
        /// <param name="loggerFactory">Optional logger factory for creating loggers.</param>
        public StorageClient(
            ITrackerClient trackerClient,
            ConnectionPoolConfiguration poolOptions,
            ILoggerFactory? loggerFactory = null)
        {
            _trackerClient = trackerClient ?? throw new ArgumentNullException(nameof(trackerClient));
            _poolOptions = poolOptions ?? throw new ArgumentNullException(nameof(poolOptions));
            _loggerFactory = loggerFactory;
            _logger = loggerFactory?.CreateLogger<StorageClient>() ?? NullLogger<StorageClient>.Instance;
            _storagePools = new ConcurrentDictionary<string, IConnectionPool>();

            _logger.LogInformation("StorageClient initialized");
        }

        /// <summary>
        /// Uploads a file from byte array.
        /// </summary>
        public async Task<string> UploadAsync(string? groupName, byte[] content, string fileExtension, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StorageClient));
            if (content == null || content.Length == 0)
                throw new ArgumentException("File content cannot be null or empty.", nameof(content));
            if (string.IsNullOrEmpty(fileExtension))
                throw new ArgumentException("File extension cannot be null or empty.", nameof(fileExtension));

            // Query tracker for upload storage
            var storageInfo = await _trackerClient.QueryStorageForUploadAsync(groupName, cancellationToken).ConfigureAwait(false);

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
                // CombineFileId handles cases where FileName might already contain GroupName
                return FileIdHelper.CombineFileId(response.GroupName, response.FileName);
            }
            finally
            {
                pool.ReturnConnection(connection);
            }
        }

        /// <summary>
        /// Uploads a file from a stream.
        /// </summary>
        public async Task<string> UploadAsync(string? groupName, Stream stream, string fileExtension, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StorageClient));
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

        /// <summary>
        /// Uploads a file from local file path.
        /// </summary>
        public async Task<string> UploadFileAsync(string? groupName, string localFilePath, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StorageClient));
            if (string.IsNullOrEmpty(localFilePath))
                throw new ArgumentException("Local file path cannot be null or empty.", nameof(localFilePath));
            if (!File.Exists(localFilePath))
                throw new FileNotFoundException("File not found.", localFilePath);

            var extension = Path.GetExtension(localFilePath).TrimStart('.');
            byte[] content;
            using (var fileStream = File.OpenRead(localFilePath))
            {
                content = new byte[fileStream.Length];
                await fileStream.ReadAsync(content, 0, content.Length, cancellationToken).ConfigureAwait(false);
            }

            return await UploadAsync(groupName, content, extension, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Downloads a file as byte array.
        /// </summary>
        public async Task<byte[]> DownloadAsync(string? groupName, string fileName, CancellationToken cancellationToken = default)
        {
            return await DownloadAsync(groupName, fileName, 0, 0, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Downloads a file to a stream.
        /// </summary>
        public async Task DownloadAsync(string? groupName, string fileName, Stream outputStream, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StorageClient));
            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));
            if (!outputStream.CanWrite)
                throw new ArgumentException("Stream must be writable.", nameof(outputStream));

            var content = await DownloadAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAsync(content, 0, content.Length, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Downloads a file to local file system.
        /// </summary>
        public async Task DownloadFileAsync(string? groupName, string fileName, string localFilePath, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StorageClient));
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

        /// <summary>
        /// Downloads a portion of a file.
        /// </summary>
        public async Task<byte[]> DownloadAsync(string? groupName, string fileName, long offset, long length, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StorageClient));

            // Validate fileName first
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            // Normalize parameters: handle case where fileName might contain groupName
            NormalizeGroupAndFileName(ref groupName, ref fileName);

            // Validate groupName after normalization
            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be determined. Please provide a valid group name or a complete file ID.", nameof(groupName));

            // Query tracker for download storage
            var storageInfo = await _trackerClient.QueryStorageForDownloadAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);

            // Get or create connection pool for this storage server
            var pool = GetOrCreateStoragePool(storageInfo.IpAddress, storageInfo.Port);
            var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var request = new DownloadFileRequest
                {
                    GroupName = groupName!, // Validated as non-null after normalization
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

        /// <summary>
        /// Deletes a file from the storage server.
        /// </summary>
        public async Task DeleteAsync(string? groupName, string fileName, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StorageClient));

            // Validate fileName first
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            // Normalize parameters: handle case where fileName might contain groupName
            NormalizeGroupAndFileName(ref groupName, ref fileName);

            // Validate groupName after normalization
            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be determined. Please provide a valid group name or a complete file ID.", nameof(groupName));

            // Query tracker for update storage
            var storageInfo = await _trackerClient.QueryStorageForUpdateAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);

            // Get or create connection pool for this storage server
            var pool = GetOrCreateStoragePool(storageInfo.IpAddress, storageInfo.Port);
            var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var request = new DeleteFileRequest
                {
                    GroupName = groupName!, // Validated as non-null after normalization
                    FileName = fileName
                };

                await connection.SendRequestAsync<DeleteFileRequest, DeleteFileResponse>(request, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                pool.ReturnConnection(connection);
            }
        }

        /// <summary>
        /// Queries file information.
        /// </summary>
        public async Task<FastDFSFileInfo> QueryFileInfoAsync(string? groupName, string fileName, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StorageClient));

            // Validate fileName first
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            // Normalize parameters: handle case where fileName might contain groupName
            NormalizeGroupAndFileName(ref groupName, ref fileName);

            // Validate groupName after normalization
            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be determined. Please provide a valid group name or a complete file ID.", nameof(groupName));

            // Query tracker for download storage (any storage that has the file)
            var storageInfo = await _trackerClient.QueryStorageForDownloadAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);

            // Get or create connection pool for this storage server
            var pool = GetOrCreateStoragePool(storageInfo.IpAddress, storageInfo.Port);
            var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var request = new QueryFileInfoRequest
                {
                    GroupName = groupName!, // Validated as non-null after normalization
                    FileName = fileName
                };

                var response = await connection.SendRequestAsync<QueryFileInfoRequest, QueryFileInfoResponse>(request, cancellationToken).ConfigureAwait(false);

                return response.FileInfo ?? new FastDFSFileInfo();
            }
            finally
            {
                pool.ReturnConnection(connection);
            }
        }

        /// <summary>
        /// Gets or creates a connection pool for a storage server.
        /// </summary>
        /// <param name="host">The storage server host.</param>
        /// <param name="port">The storage server port.</param>
        /// <returns>The connection pool.</returns>
        private IConnectionPool GetOrCreateStoragePool(string host, int port)
        {
            var key = $"{host}:{port}";
            return _storagePools.GetOrAdd(key, _ =>
            {
                var poolLogger = _loggerFactory?.CreateLogger<ConnectionPool>();
                return new ConnectionPool(host, port, _poolOptions, poolLogger);
            });
        }

        /// <summary>
        /// Normalizes group name and file name parameters.
        /// Handles cases where fileName might already contain the group name prefix.
        /// If groupName is null/empty but fileName contains a complete file ID, extracts the group name.
        /// </summary>
        /// <param name="groupName">The group name (may be updated).</param>
        /// <param name="fileName">The file name (may be updated to remove group prefix).</param>
        private static void NormalizeGroupAndFileName(ref string? groupName, ref string fileName)
        {
            // Check if fileName contains a group name prefix
            if (FileIdHelper.HasGroupName(fileName))
            {
                // fileName contains a group name, parse it
                FileIdHelper.ParseFileId(fileName, out string extractedGroupName, out string extractedFileName);

                // Update parameters with extracted values
                groupName = extractedGroupName;
                fileName = extractedFileName;
            }
            // else: fileName doesn't contain group name, keep groupName as provided
        }

        /// <summary>
        /// Disposes the storage client and all connection pools.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

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
        /// Uploads an appender file from byte array.
        /// </summary>
        public async Task<string> UploadAppenderFileAsync(string? groupName, byte[] content, string fileExtension, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StorageClient));
            if (content == null || content.Length == 0)
                throw new ArgumentException("File content cannot be null or empty.", nameof(content));
            if (string.IsNullOrEmpty(fileExtension))
                throw new ArgumentException("File extension cannot be null or empty.", nameof(fileExtension));

            // Query tracker for upload storage
            var storageInfo = await _trackerClient.QueryStorageForUploadAsync(groupName, cancellationToken).ConfigureAwait(false);

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
                // CombineFileId handles cases where FileName might already contain GroupName
                return FileIdHelper.CombineFileId(response.GroupName, response.FileName);
            }
            finally
            {
                pool.ReturnConnection(connection);
            }
        }

        /// <summary>
        /// Appends data to an existing appender file.
        /// </summary>
        public async Task AppendFileAsync(string? groupName, string fileName, byte[] content, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StorageClient));

            // Validate fileName first
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));
            if (content == null || content.Length == 0)
                throw new ArgumentException("Content cannot be null or empty.", nameof(content));

            // Normalize parameters: handle case where fileName might contain groupName
            NormalizeGroupAndFileName(ref groupName, ref fileName);

            // Validate groupName after normalization
            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be determined. Please provide a valid group name or a complete file ID.", nameof(groupName));

            // Query tracker for update storage (appender file must be updated on original storage)
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

        /// <summary>
        /// Appends data to an existing appender file using complete file ID.
        /// NOTE: This method requires a complete file ID with group name (e.g., "group1/M00/00/00/xxx.jpg").
        /// If your file ID doesn't contain the group name, use the AppendFileAsync(groupName, fileName, content) overload instead.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="content">The content to append.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="ArgumentException">Thrown when fileId doesn't contain a group name.</exception>
        public async Task AppendFileAsync(string fileId, byte[] content, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(fileId))
                throw new ArgumentException("File ID cannot be null or empty.", nameof(fileId));

            // This will throw ArgumentException if fileId doesn't contain groupName
            FileIdHelper.ParseFileId(fileId, out string groupName, out string fileName);
            await AppendFileAsync(groupName, fileName, content, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Downloads a file using complete file ID.
        /// NOTE: This method requires a complete file ID with group name (e.g., "group1/M00/00/00/xxx.jpg").
        /// If your file ID doesn't contain the group name, use the DownloadAsync(groupName, fileName) overload instead.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file content as byte array.</returns>
        /// <exception cref="ArgumentException">Thrown when fileId doesn't contain a group name.</exception>
        public async Task<byte[]> DownloadAsync(string fileId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(fileId))
                throw new ArgumentException("File ID cannot be null or empty.", nameof(fileId));

            // This will throw ArgumentException if fileId doesn't contain groupName
            FileIdHelper.ParseFileId(fileId, out string groupName, out string fileName);
            return await DownloadAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a file using complete file ID.
        /// NOTE: This method requires a complete file ID with group name (e.g., "group1/M00/00/00/xxx.jpg").
        /// If your file ID doesn't contain the group name, use the DeleteAsync(groupName, fileName) overload instead.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="ArgumentException">Thrown when fileId doesn't contain a group name.</exception>
        public async Task DeleteAsync(string fileId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(fileId))
                throw new ArgumentException("File ID cannot be null or empty.", nameof(fileId));

            // This will throw ArgumentException if fileId doesn't contain groupName
            FileIdHelper.ParseFileId(fileId, out string groupName, out string fileName);
            await DeleteAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Queries file information using complete file ID.
        /// NOTE: This method requires a complete file ID with group name (e.g., "group1/M00/00/00/xxx.jpg").
        /// If your file ID doesn't contain the group name, use the QueryFileInfoAsync(groupName, fileName) overload instead.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file information.</returns>
        /// <exception cref="ArgumentException">Thrown when fileId doesn't contain a group name.</exception>
        public async Task<FastDFSFileInfo> QueryFileInfoAsync(string fileId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(fileId))
                throw new ArgumentException("File ID cannot be null or empty.", nameof(fileId));

            // This will throw ArgumentException if fileId doesn't contain groupName
            FileIdHelper.ParseFileId(fileId, out string groupName, out string fileName);
            return await QueryFileInfoAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets metadata for a file.
        /// </summary>
        public async Task SetMetadataAsync(string? groupName, string fileName, FastDFSMetadata metadata, MetadataFlag flag = MetadataFlag.Overwrite, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StorageClient));

            // Validate fileName first
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            // Normalize parameters: handle case where fileName might contain groupName
            NormalizeGroupAndFileName(ref groupName, ref fileName);

            // Validate groupName after normalization
            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be determined. Please provide a valid group name or a complete file ID.", nameof(groupName));

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

        /// <summary>
        /// Sets metadata for a file using complete file ID.
        /// NOTE: This method requires a complete file ID with group name (e.g., "group1/M00/00/00/xxx.jpg").
        /// If your file ID doesn't contain the group name, use the SetMetadataAsync(groupName, fileName, metadata, flag) overload instead.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="metadata">The metadata to set.</param>
        /// <param name="flag">The metadata operation flag (Overwrite or Merge).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="ArgumentException">Thrown when fileId doesn't contain a group name.</exception>
        public async Task SetMetadataAsync(string fileId, FastDFSMetadata metadata, MetadataFlag flag = MetadataFlag.Overwrite, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(fileId))
                throw new ArgumentException("File ID cannot be null or empty.", nameof(fileId));

            // This will throw ArgumentException if fileId doesn't contain groupName
            FileIdHelper.ParseFileId(fileId, out string groupName, out string fileName);
            await SetMetadataAsync(groupName, fileName, metadata, flag, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets metadata for a file.
        /// </summary>
        public async Task<FastDFSMetadata> GetMetadataAsync(string? groupName, string fileName, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StorageClient));

            // Validate fileName first
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            // Normalize parameters: handle case where fileName might contain groupName
            NormalizeGroupAndFileName(ref groupName, ref fileName);

            // Validate groupName after normalization
            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be determined. Please provide a valid group name or a complete file ID.", nameof(groupName));

            // Query tracker for download storage
            var storageInfo = await _trackerClient.QueryStorageForDownloadAsync(groupName!, fileName, cancellationToken).ConfigureAwait(false);

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

        /// <summary>
        /// Gets metadata for a file using complete file ID.
        /// NOTE: This method requires a complete file ID with group name (e.g., "group1/M00/00/00/xxx.jpg").
        /// If your file ID doesn't contain the group name, use the GetMetadataAsync(groupName, fileName) overload instead.
        /// </summary>
        /// <param name="fileId">The complete file ID in the format "group_name/path/filename".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file metadata.</returns>
        /// <exception cref="ArgumentException">Thrown when fileId doesn't contain a group name.</exception>
        public async Task<FastDFSMetadata> GetMetadataAsync(string fileId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(fileId))
                throw new ArgumentException("File ID cannot be null or empty.", nameof(fileId));

            // This will throw ArgumentException if fileId doesn't contain groupName
            FileIdHelper.ParseFileId(fileId, out string groupName, out string fileName);
            return await GetMetadataAsync(groupName, fileName, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns a string representation of the storage client.
        /// </summary>
        public override string ToString()
        {
            return $"StorageClient [ActiveStoragePools={_storagePools.Count}]";
        }
    }
}
