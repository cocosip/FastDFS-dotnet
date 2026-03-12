using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FastDFS.Client.Configuration;
using FastDFS.Client.Connection;
using FastDFS.Client.Exceptions;
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
    /// FastDFS Storage client for executing protocol commands against storage servers.
    /// Manages a connection pool per storage server endpoint.
    /// Does not interact with the tracker — the caller is responsible for resolving
    /// the correct <see cref="StorageServerInfo"/> before invoking any method.
    /// </summary>
    public class StorageClient : IStorageClient, IDisposable
    {
        private readonly IConnectionPoolProvider _poolProvider;
        private readonly ILogger _logger;
        private readonly bool _ownsPoolProvider;
        private readonly int _streamCopyBufferSize;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageClient"/> class.
        /// </summary>
        public StorageClient(ConnectionPoolConfiguration poolOptions, ILoggerFactory? loggerFactory = null)
            : this(new ConnectionPoolProvider(poolOptions, loggerFactory), poolOptions?.StreamCopyBufferSize ?? 81920, loggerFactory, true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageClient"/> class.
        /// </summary>
        /// <param name="poolProvider">The shared connection pool provider.</param>
        /// <param name="loggerFactory">Optional logger factory.</param>
        public StorageClient(IConnectionPoolProvider poolProvider, ILoggerFactory? loggerFactory = null)
            : this(poolProvider, 81920, loggerFactory, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageClient"/> class.
        /// </summary>
        /// <param name="poolProvider">The shared connection pool provider.</param>
        /// <param name="streamCopyBufferSize">The buffer size used for streaming upload/download operations.</param>
        /// <param name="loggerFactory">Optional logger factory.</param>
        public StorageClient(IConnectionPoolProvider poolProvider, int streamCopyBufferSize, ILoggerFactory? loggerFactory = null)
            : this(poolProvider, streamCopyBufferSize, loggerFactory, false)
        {
        }

        private StorageClient(IConnectionPoolProvider poolProvider, int streamCopyBufferSize, ILoggerFactory? loggerFactory, bool ownsPoolProvider)
        {
            _poolProvider = poolProvider ?? throw new ArgumentNullException(nameof(poolProvider));
            if (streamCopyBufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(streamCopyBufferSize), "Stream copy buffer size must be greater than 0.");

            _logger = loggerFactory?.CreateLogger<StorageClient>() ?? NullLogger<StorageClient>.Instance;
            _ownsPoolProvider = ownsPoolProvider;
            _streamCopyBufferSize = streamCopyBufferSize;

            _logger.LogInformation("StorageClient initialized with StreamCopyBufferSize={BufferSize}", _streamCopyBufferSize);
        }

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
        public async Task<string> UploadAsync(StorageServerInfo server, byte[] content, string fileExtension, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var pool = GetOrCreateStoragePool(server);
            return await pool.ExecuteAsync(async connection =>
            {
                var request = new UploadFileRequest
                {
                    StorePathIndex = server.StorePathIndex,
                    FileContent = content,
                    FileExtension = fileExtension
                };

                var response = await connection.SendRequestAsync<UploadFileRequest, UploadFileResponse>(request, cancellationToken).ConfigureAwait(false);
                return FileIdHelper.CombineFileId(response.GroupName, response.FileName);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Uploads a file from a readable stream to the specified storage server.
        /// </summary>
        public async Task<string> UploadAsync(StorageServerInfo server, Stream contentStream, long contentLength, string fileExtension, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (contentStream == null)
                throw new ArgumentNullException(nameof(contentStream));
            if (!contentStream.CanRead)
                throw new ArgumentException("Content stream must be readable.", nameof(contentStream));
            if (contentLength < 0)
                throw new ArgumentOutOfRangeException(nameof(contentLength), "Content length cannot be negative.");

            var pool = GetOrCreateStoragePool(server);
            return await pool.ExecuteAsync(async connection =>
            {
                var response = await connection.SendUploadRequestAsync(
                    StorageCommand.UploadFile,
                    server.StorePathIndex,
                    contentStream,
                    contentLength,
                    fileExtension,
                    _streamCopyBufferSize,
                    cancellationToken).ConfigureAwait(false);

                return FileIdHelper.CombineFileId(response.GroupName, response.FileName);
            }, cancellationToken).ConfigureAwait(false);
        }

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
        public async Task<string> UploadAppenderFileAsync(StorageServerInfo server, byte[] content, string fileExtension, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var pool = GetOrCreateStoragePool(server);
            return await pool.ExecuteAsync(async connection =>
            {
                var request = new UploadAppenderFileRequest
                {
                    StorePathIndex = server.StorePathIndex,
                    FileContent = content,
                    FileExtension = fileExtension
                };

                var response = await connection.SendRequestAsync<UploadAppenderFileRequest, UploadFileResponse>(request, cancellationToken).ConfigureAwait(false);
                return FileIdHelper.CombineFileId(response.GroupName, response.FileName);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Appends data to an existing appender file on the specified storage server.
        /// </summary>
        /// <param name="server">The storage server information obtained from the tracker.</param>
        /// <param name="fileName">The name of the appender file to append data to.</param>
        /// <param name="content">The data to append to the file.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="ObjectDisposedException">Thrown when the storage client has been disposed.</exception>
        /// <exception cref="FastDFSException">Thrown when the append operation fails.</exception>
        public async Task AppendFileAsync(StorageServerInfo server, string fileName, byte[] content, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var pool = GetOrCreateStoragePool(server);
            await pool.ExecuteAsync(async connection =>
            {
                var request = new AppendFileRequest
                {
                    FileName = fileName,
                    Content = content
                };

                await connection.SendRequestAsync<AppendFileRequest, AppendFileResponse>(request, cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }

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
        public async Task<byte[]> DownloadAsync(StorageServerInfo server, string groupName, string fileName, long offset, long length, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var pool = GetOrCreateStoragePool(server);
            return await pool.ExecuteAsync(async connection =>
            {
                var request = new DownloadFileRequest
                {
                    GroupName = groupName,
                    FileName = fileName,
                    FileOffset = offset,
                    DownloadBytes = length
                };

                var response = await connection.SendRequestAsync<DownloadFileRequest, DownloadFileResponse>(request, cancellationToken).ConfigureAwait(false);
                return response.Content;
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Downloads a portion of a file from the specified storage server directly into the destination stream.
        /// </summary>
        public async Task DownloadAsync(StorageServerInfo server, string groupName, string fileName, Stream destination, long offset, long length, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (!destination.CanWrite)
                throw new ArgumentException("Destination stream must be writable.", nameof(destination));

            var pool = GetOrCreateStoragePool(server);
            await pool.ExecuteAsync(async connection =>
            {
                var request = new DownloadFileRequest
                {
                    GroupName = groupName,
                    FileName = fileName,
                    FileOffset = offset,
                    DownloadBytes = length
                };

                await connection.DownloadToStreamAsync(request, destination, _streamCopyBufferSize, cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a file from the specified storage server.
        /// </summary>
        /// <param name="server">The storage server information obtained from the tracker.</param>
        /// <param name="groupName">The name of the storage group where the file is located.</param>
        /// <param name="fileName">The name of the file to delete.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="ObjectDisposedException">Thrown when the storage client has been disposed.</exception>
        /// <exception cref="FastDFSException">Thrown when the delete operation fails.</exception>
        public async Task DeleteAsync(StorageServerInfo server, string groupName, string fileName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var pool = GetOrCreateStoragePool(server);
            await pool.ExecuteAsync(async connection =>
            {
                var request = new DeleteFileRequest
                {
                    GroupName = groupName,
                    FileName = fileName
                };

                await connection.SendRequestAsync<DeleteFileRequest, DeleteFileResponse>(request, cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }

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
        public async Task<FastDFSFileInfo> QueryFileInfoAsync(StorageServerInfo server, string groupName, string fileName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var pool = GetOrCreateStoragePool(server);
            return await pool.ExecuteAsync(async connection =>
            {
                var request = new QueryFileInfoRequest
                {
                    GroupName = groupName,
                    FileName = fileName
                };

                var response = await connection.SendRequestAsync<QueryFileInfoRequest, QueryFileInfoResponse>(request, cancellationToken).ConfigureAwait(false);
                return response.FileInfo ?? new FastDFSFileInfo();
            }, cancellationToken).ConfigureAwait(false);
        }

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
        public async Task SetMetadataAsync(StorageServerInfo server, string groupName, string fileName, FastDFSMetadata metadata, MetadataFlag flag, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var pool = GetOrCreateStoragePool(server);
            await pool.ExecuteAsync(async connection =>
            {
                var request = new SetMetadataRequest
                {
                    GroupName = groupName,
                    FileName = fileName,
                    Metadata = metadata,
                    Flag = flag
                };

                await connection.SendRequestAsync<SetMetadataRequest, SetMetadataResponse>(request, cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }

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
        public async Task<FastDFSMetadata> GetMetadataAsync(StorageServerInfo server, string groupName, string fileName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var pool = GetOrCreateStoragePool(server);
            return await pool.ExecuteAsync(async connection =>
            {
                var request = new GetMetadataRequest
                {
                    GroupName = groupName,
                    FileName = fileName
                };

                var response = await connection.SendRequestAsync<GetMetadataRequest, GetMetadataResponse>(request, cancellationToken).ConfigureAwait(false);
                return response.Metadata;
            }, cancellationToken).ConfigureAwait(false);
        }

        private IConnectionPool GetOrCreateStoragePool(StorageServerInfo server)
        {
            var endpoint = new ConnectionEndpoint(server.IpAddress, server.Port);
            return _poolProvider.GetOrCreate(endpoint);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StorageClient));
        }

        /// <summary>
        /// Disposes the storage client and all connection pools.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_ownsPoolProvider)
            {
                _poolProvider.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public override string ToString() =>
            $"StorageClient [ActiveStoragePools={_poolProvider.GetEndpoints().Count}]";
    }
}
