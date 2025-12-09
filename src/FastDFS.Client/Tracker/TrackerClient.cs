using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastDFS.Client.Configuration;
using FastDFS.Client.Connection;
using FastDFS.Client.Exceptions;
using FastDFS.Client.Protocol.Requests;
using FastDFS.Client.Protocol.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FastDFS.Client.Tracker
{
    /// <summary>
    /// FastDFS Tracker client for querying storage servers.
    /// Supports multiple tracker servers with automatic failover.
    /// </summary>
    public class TrackerClient : ITrackerClient, IDisposable
    {
        private readonly List<TrackerServerEndpoint> _trackerEndpoints;
        private readonly Dictionary<string, IConnectionPool> _connectionPools;
        private readonly ConnectionPoolConfiguration _poolOptions;
        private readonly ILogger _logger;
        private int _currentTrackerIndex;
        private bool _disposed;

        /// <summary>
        /// Represents a tracker server endpoint.
        /// </summary>
        private class TrackerServerEndpoint
        {
            public string Host { get; set; } = string.Empty;
            public int Port { get; set; }
            public string Key => $"{Host}:{Port}";
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TrackerClient"/> class.
        /// </summary>
        /// <param name="trackerServers">List of tracker server addresses in the format "host:port".</param>
        /// <param name="configuration">Connection pool options.</param>
        /// <param name="loggerFactory">Optional logger factory for creating loggers.</param>
        public TrackerClient(
            IEnumerable<string> trackerServers,
            ConnectionPoolConfiguration configuration,
            ILoggerFactory? loggerFactory = null)
        {
            if (trackerServers == null || !trackerServers.Any())
                throw new ArgumentException("At least one tracker server must be specified.", nameof(trackerServers));
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            configuration.Validate();

            _poolOptions = configuration;
            _logger = loggerFactory?.CreateLogger<TrackerClient>() ?? NullLogger<TrackerClient>.Instance;
            _trackerEndpoints = [];
            _connectionPools = [];
            _currentTrackerIndex = 0;

            // Parse tracker server addresses
            foreach (var server in trackerServers)
            {
                var parts = server.Split(':');
                if (parts.Length != 2)
                    throw new ArgumentException($"Invalid tracker server address format: {server}. Expected format: 'host:port'");

                if (!int.TryParse(parts[1], out int port))
                    throw new ArgumentException($"Invalid port number in tracker server address: {server}");

                var endpoint = new TrackerServerEndpoint
                {
                    Host = parts[0].Trim(),
                    Port = port
                };

                _trackerEndpoints.Add(endpoint);

                // Create a connection pool for each tracker server
                var poolLogger = loggerFactory?.CreateLogger<ConnectionPool>();
                _connectionPools[endpoint.Key] = new ConnectionPool(endpoint.Host, endpoint.Port, _poolOptions, poolLogger);
            }

            _logger.LogInformation("TrackerClient initialized with {TrackerCount} tracker server(s): {TrackerSerkers}",
                _trackerEndpoints.Count, string.Join(", ", _trackerEndpoints.Select(e => e.Key)));
        }

        /// <summary>
        /// Queries a Storage server for uploading files.
        /// </summary>
        /// <param name="groupName">The storage group name. If null or empty, the tracker will select a group automatically.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Storage server information for upload.</returns>
        public async Task<StorageServerInfo> QueryStorageForUploadAsync(string? groupName = null, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TrackerClient));

            _logger.LogDebug("Querying storage for upload, group={GroupName}", groupName ?? "(auto-select)");

            return await ExecuteWithFailoverAsync(async (pool) =>
            {
                var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    QueryStoreResponse response;

                    if (string.IsNullOrEmpty(groupName))
                    {
                        // Query without group - let tracker select
                        var request = new QueryStoreWithoutGroupRequest();
                        response = await connection.SendRequestAsync<QueryStoreWithoutGroupRequest, QueryStoreResponse>(request, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        // Query with specific group
                        var request = new QueryStoreWithGroupRequest { GroupName = groupName! };
                        response = await connection.SendRequestAsync<QueryStoreWithGroupRequest, QueryStoreResponse>(request, cancellationToken).ConfigureAwait(false);
                    }

                    if (response.ServerInfo == null)
                        throw new FastDFSProtocolException("Tracker returned empty storage server info.");

                    return response.ServerInfo;
                }
                finally
                {
                    pool.ReturnConnection(connection);
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Queries a Storage server for downloading a file.
        /// </summary>
        /// <param name="groupName">The storage group name.</param>
        /// <param name="fileName">The file name (path on storage server).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Storage server information for download.</returns>
        public async Task<StorageServerInfo> QueryStorageForDownloadAsync(string groupName, string fileName, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TrackerClient));
            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be null or empty.", nameof(groupName));
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            return await ExecuteWithFailoverAsync(async (pool) =>
            {
                var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var request = new QueryFetchRequest
                    {
                        GroupName = groupName,
                        FileName = fileName
                    };

                    var response = await connection.SendRequestAsync<QueryFetchRequest, QueryFetchResponse>(request, cancellationToken).ConfigureAwait(false);

                    if (response.ServerInfo == null)
                        throw new FastDFSProtocolException("Tracker returned empty storage server info.");

                    return response.ServerInfo;
                }
                finally
                {
                    pool.ReturnConnection(connection);
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Queries a Storage server for updating a file (delete, set metadata, etc.).
        /// </summary>
        /// <param name="groupName">The storage group name.</param>
        /// <param name="fileName">The file name (path on storage server).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Storage server information for update.</returns>
        public async Task<StorageServerInfo> QueryStorageForUpdateAsync(string groupName, string fileName, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TrackerClient));
            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be null or empty.", nameof(groupName));
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            return await ExecuteWithFailoverAsync(async (pool) =>
            {
                var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var request = new QueryUpdateRequest
                    {
                        GroupName = groupName,
                        FileName = fileName
                    };

                    var response = await connection.SendRequestAsync<QueryUpdateRequest, QueryFetchResponse>(request, cancellationToken).ConfigureAwait(false);

                    if (response.ServerInfo == null)
                        throw new FastDFSProtocolException("Tracker returned empty storage server info.");

                    return response.ServerInfo;
                }
                finally
                {
                    pool.ReturnConnection(connection);
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Queries all available Storage servers for uploading files.
        /// </summary>
        /// <param name="groupName">The storage group name. If null or empty, the tracker will select a group automatically.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of all available storage servers for upload.</returns>
        public async Task<List<StorageServerInfo>> QueryAllStoragesForUploadAsync(string? groupName = null, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TrackerClient));

            return await ExecuteWithFailoverAsync(async (pool) =>
            {
                var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    QueryStoreAllResponse response;

                    if (string.IsNullOrEmpty(groupName))
                    {
                        // Query without group - let tracker select
                        var request = new QueryStoreWithoutGroupAllRequest();
                        response = await connection.SendRequestAsync<QueryStoreWithoutGroupAllRequest, QueryStoreAllResponse>(request, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        // Query with specific group
                        var request = new QueryStoreWithGroupAllRequest { GroupName = groupName! };
                        response = await connection.SendRequestAsync<QueryStoreWithGroupAllRequest, QueryStoreAllResponse>(request, cancellationToken).ConfigureAwait(false);
                    }

                    if (response.ServerInfos == null || response.ServerInfos.Count == 0)
                        throw new FastDFSProtocolException("Tracker returned no available storage servers.");

                    return response.ServerInfos;
                }
                finally
                {
                    pool.ReturnConnection(connection);
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Queries all Storage servers for downloading a file.
        /// </summary>
        /// <param name="groupName">The storage group name.</param>
        /// <param name="fileName">The file name (path on storage server).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of all storage servers that have the file.</returns>
        public async Task<List<StorageServerInfo>> QueryAllStoragesForDownloadAsync(string groupName, string fileName, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TrackerClient));
            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be null or empty.", nameof(groupName));
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            return await ExecuteWithFailoverAsync(async (pool) =>
            {
                var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var request = new QueryFetchAllRequest
                    {
                        GroupName = groupName,
                        FileName = fileName
                    };

                    var response = await connection.SendRequestAsync<QueryFetchAllRequest, QueryFetchAllResponse>(request, cancellationToken).ConfigureAwait(false);

                    if (response.ServerInfos == null || response.ServerInfos.Count == 0)
                        throw new FastDFSProtocolException("Tracker returned no available storage servers for the file.");

                    return response.ServerInfos;
                }
                finally
                {
                    pool.ReturnConnection(connection);
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes an operation with automatic failover to other tracker servers if one fails.
        /// </summary>
        /// <typeparam name="T">The return type.</typeparam>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The operation result.</returns>
        private async Task<T> ExecuteWithFailoverAsync<T>(
            Func<IConnectionPool, Task<T>> operation,
            CancellationToken cancellationToken)
        {
            Exception? lastException = null;
            int attempts = 0;
            int maxAttempts = _trackerEndpoints.Count;

            // Try each tracker server in round-robin fashion
            while (attempts < maxAttempts)
            {
                // Thread-safe read of current tracker index
                var baseIndex = Interlocked.CompareExchange(ref _currentTrackerIndex, 0, 0);
                var currentIndex = (baseIndex + attempts) % _trackerEndpoints.Count;
                var endpoint = _trackerEndpoints[currentIndex];
                var pool = _connectionPools[endpoint.Key];

                _logger.LogDebug("Attempting operation on tracker {Tracker} (attempt {Attempt}/{MaxAttempts})",
                    endpoint.Key, attempts + 1, maxAttempts);

                try
                {
                    var result = await operation(pool).ConfigureAwait(false);

                    // Success - update the current tracker index for next request (thread-safe)
                    Interlocked.Exchange(ref _currentTrackerIndex, currentIndex);

                    if (attempts > 0)
                    {
                        _logger.LogInformation("Operation succeeded on tracker {Tracker} after {Attempts} failed attempt(s)",
                            endpoint.Key, attempts);
                    }
                    else
                    {
                        _logger.LogDebug("Operation succeeded on tracker {Tracker}", endpoint.Key);
                    }

                    return result;
                }
                catch (FastDFSNetworkException ex)
                {
                    // Network error - try next tracker
                    _logger.LogWarning(ex, "Network error on tracker {Tracker}, attempting failover (attempt {Attempt}/{MaxAttempts})",
                        endpoint.Key, attempts + 1, maxAttempts);
                    lastException = ex;
                    attempts++;
                }
                catch (TimeoutException ex)
                {
                    // Timeout - try next tracker
                    _logger.LogWarning(ex, "Timeout on tracker {Tracker}, attempting failover (attempt {Attempt}/{MaxAttempts})",
                        endpoint.Key, attempts + 1, maxAttempts);
                    lastException = ex;
                    attempts++;
                }
                catch (Exception ex)
                {
                    // Other errors (protocol errors, etc.) should not trigger failover
                    _logger.LogError(ex, "Non-recoverable error on tracker {Tracker}", endpoint.Key);
                    throw;
                }
            }

            // All tracker servers failed
            _logger.LogError(lastException, "All {TrackerCount} tracker server(s) failed after {MaxAttempts} attempts",
                _trackerEndpoints.Count, maxAttempts);
            throw new FastDFSException(
                $"All tracker servers failed after {maxAttempts} attempts. Last error: {lastException?.Message}",
                lastException!);
        }

        // ==================== Management Operations ====================

        /// <inheritdoc/>
        public async Task<List<GroupInfo>> ListAllGroupsAsync(CancellationToken cancellationToken = default)
        {
            var request = new ListAllGroupsRequest();
            var response = await ExecuteWithFailoverAsync(async (pool) =>
            {
                var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    return await connection.SendRequestAsync<ListAllGroupsRequest, ListAllGroupsResponse>(request, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    pool.ReturnConnection(connection);
                }
            }, cancellationToken).ConfigureAwait(false);

            return response.Groups;
        }

        /// <inheritdoc/>
        public async Task<List<StorageServerDetail>> ListStorageServersAsync(string groupName, string? storageServerId = null, CancellationToken cancellationToken = default)
        {
            var request = new ListStorageServersRequest
            {
                GroupName = groupName,
                StorageServerId = storageServerId
            };

            var response = await ExecuteWithFailoverAsync(async (pool) =>
            {
                var connection = await pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    return await connection.SendRequestAsync<ListStorageServersRequest, ListStorageServersResponse>(request, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    pool.ReturnConnection(connection);
                }
            }, cancellationToken).ConfigureAwait(false);

            return response.Servers;
        }

        /// <summary>
        /// Disposes the tracker client and all connection pools.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Dispose all connection pools
            foreach (var pool in _connectionPools.Values)
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

            _connectionPools.Clear();

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Returns a string representation of the tracker client.
        /// </summary>
        public override string ToString()
        {
            var servers = string.Join(", ", _trackerEndpoints.Select(e => e.Key));
            return $"TrackerClient [Servers={servers}, CurrentIndex={_currentTrackerIndex}]";
        }
    }
}
