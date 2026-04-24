using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FastDFS.Client.Exceptions;
using FastDFS.Client.Protocol;
using FastDFS.Client.Storage;
using FastDFS.Client.Tracker;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests
{
    public class FastDFSClientTests
    {
        [Fact]
        public async Task DownloadFileAsync_WhenDownloadFails_ShouldPreserveExistingFile()
        {
            string tempDirectory = CreateTempDirectory();
            try
            {
                string localFilePath = Path.Combine(tempDirectory, "existing.txt");
                await File.WriteAllTextAsync(localFilePath, "original");

                using var client = CreateClient(new ThrowingStorageClient(new FastDFSException("download failed")));

                Func<Task> act = () => client.DownloadFileAsync("group1/M00/00/00/file.txt", localFilePath);

                await act.Should().ThrowAsync<FastDFSException>();
                string content = await File.ReadAllTextAsync(localFilePath);
                content.Should().Be("original");
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public async Task DownloadFileAsync_WhenDownloadSucceeds_ShouldReplaceExistingFile()
        {
            string tempDirectory = CreateTempDirectory();
            try
            {
                string localFilePath = Path.Combine(tempDirectory, "existing.txt");
                await File.WriteAllTextAsync(localFilePath, "original");

                using var client = CreateClient(new WritingStorageClient(new byte[] { 1, 2, 3, 4 }));

                await client.DownloadFileAsync("group1/M00/00/00/file.txt", localFilePath);

                byte[] content = await File.ReadAllBytesAsync(localFilePath);
                content.Should().Equal(new byte[] { 1, 2, 3, 4 });
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public async Task DownloadAsync_WithNegativeOffsetInFileIdOverload_ShouldThrowArgumentOutOfRangeException()
        {
            using var client = CreateClient(new ThrowingStorageClient(new InvalidOperationException("Should not reach storage.")));

            var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => client.DownloadAsync("group1/M00/00/00/file.txt", -1, 0));

            exception.ParamName.Should().Be("offset");
        }

        [Fact]
        public async Task DownloadAsync_WithNegativeLengthInFileIdOverload_ShouldThrowArgumentOutOfRangeException()
        {
            using var client = CreateClient(new ThrowingStorageClient(new InvalidOperationException("Should not reach storage.")));

            var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => client.DownloadAsync("group1/M00/00/00/file.txt", 0, -1));

            exception.ParamName.Should().Be("length");
        }

        [Fact]
        public async Task UploadFileAsync_WithExtensionlessLocalFile_ShouldPassEmptyExtensionToStorageClient()
        {
            string tempDirectory = CreateTempDirectory();
            try
            {
                string localFilePath = Path.Combine(tempDirectory, "README");
                await File.WriteAllTextAsync(localFilePath, "payload");
                var storage = new CapturingStorageClient();

                using var client = CreateClient(storage);

                await client.UploadFileAsync("group1", localFilePath);

                storage.LastFileExtension.Should().Be(string.Empty);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        private static FastDFSClient CreateClient(IStorageClient storageClient)
        {
            return new FastDFSClient(new StubTrackerClient(), storageClient);
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "FastDFSClientTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private sealed class StubTrackerClient : ITrackerClient
        {
            private static readonly StorageServerInfo Server = new StorageServerInfo
            {
                GroupName = "group1",
                IpAddress = "127.0.0.1",
                Port = 23000
            };

            public Task<StorageServerInfo> QueryStorageForUploadAsync(string? groupName = null, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Server);
            }

            public Task<List<StorageServerInfo>> QueryAllStoragesForUploadAsync(string? groupName = null, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new List<StorageServerInfo> { Server });
            }

            public Task<StorageServerInfo> QueryStorageForDownloadAsync(string groupName, string fileName, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Server);
            }

            public Task<List<StorageServerInfo>> QueryAllStoragesForDownloadAsync(string groupName, string fileName, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new List<StorageServerInfo> { Server });
            }

            public Task<StorageServerInfo> QueryStorageForUpdateAsync(string groupName, string fileName, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<List<GroupInfo>> ListAllGroupsAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<List<StorageServerDetail>> ListStorageServersAsync(string groupName, string? storageServerId = null, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class ThrowingStorageClient : IStorageClient
        {
            private readonly Exception _exception;

            public ThrowingStorageClient(Exception exception)
            {
                _exception = exception;
            }

            public Task<string> UploadAsync(StorageServerInfo server, byte[] content, string fileExtension, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<string> UploadAsync(StorageServerInfo server, Stream contentStream, long contentLength, string fileExtension, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<string> UploadAppenderFileAsync(StorageServerInfo server, byte[] content, string fileExtension, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task AppendFileAsync(StorageServerInfo server, string fileName, byte[] content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<byte[]> DownloadAsync(StorageServerInfo server, string groupName, string fileName, long offset, long length, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task DeleteAsync(StorageServerInfo server, string groupName, string fileName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<FastDFSFileInfo> QueryFileInfoAsync(StorageServerInfo server, string groupName, string fileName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task SetMetadataAsync(StorageServerInfo server, string groupName, string fileName, FastDFSMetadata metadata, MetadataFlag flag, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<FastDFSMetadata> GetMetadataAsync(StorageServerInfo server, string groupName, string fileName, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task DownloadAsync(StorageServerInfo server, string groupName, string fileName, Stream destination, long offset, long length, CancellationToken cancellationToken = default)
            {
                return Task.FromException(_exception);
            }
        }

        private sealed class WritingStorageClient : IStorageClient
        {
            private readonly byte[] _content;

            public WritingStorageClient(byte[] content)
            {
                _content = content;
            }

            public Task<string> UploadAsync(StorageServerInfo server, byte[] content, string fileExtension, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<string> UploadAsync(StorageServerInfo server, Stream contentStream, long contentLength, string fileExtension, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<string> UploadAppenderFileAsync(StorageServerInfo server, byte[] content, string fileExtension, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task AppendFileAsync(StorageServerInfo server, string fileName, byte[] content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<byte[]> DownloadAsync(StorageServerInfo server, string groupName, string fileName, long offset, long length, CancellationToken cancellationToken = default) => Task.FromResult(_content);
            public Task DeleteAsync(StorageServerInfo server, string groupName, string fileName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<FastDFSFileInfo> QueryFileInfoAsync(StorageServerInfo server, string groupName, string fileName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task SetMetadataAsync(StorageServerInfo server, string groupName, string fileName, FastDFSMetadata metadata, MetadataFlag flag, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<FastDFSMetadata> GetMetadataAsync(StorageServerInfo server, string groupName, string fileName, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public async Task DownloadAsync(StorageServerInfo server, string groupName, string fileName, Stream destination, long offset, long length, CancellationToken cancellationToken = default)
            {
                await destination.WriteAsync(_content, 0, _content.Length, cancellationToken).ConfigureAwait(false);
            }
        }

        private sealed class CapturingStorageClient : IStorageClient
        {
            public string? LastFileExtension { get; private set; }

            public Task<string> UploadAsync(StorageServerInfo server, Stream contentStream, long contentLength, string fileExtension, CancellationToken cancellationToken = default)
            {
                LastFileExtension = fileExtension;
                return Task.FromResult("group1/M00/00/00/file");
            }

            public Task<string> UploadAsync(StorageServerInfo server, byte[] content, string fileExtension, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<string> UploadAppenderFileAsync(StorageServerInfo server, byte[] content, string fileExtension, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task AppendFileAsync(StorageServerInfo server, string fileName, byte[] content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<byte[]> DownloadAsync(StorageServerInfo server, string groupName, string fileName, long offset, long length, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task DownloadAsync(StorageServerInfo server, string groupName, string fileName, Stream destination, long offset, long length, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task DeleteAsync(StorageServerInfo server, string groupName, string fileName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<FastDFSFileInfo> QueryFileInfoAsync(StorageServerInfo server, string groupName, string fileName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task SetMetadataAsync(StorageServerInfo server, string groupName, string fileName, FastDFSMetadata metadata, MetadataFlag flag, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<FastDFSMetadata> GetMetadataAsync(StorageServerInfo server, string groupName, string fileName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }
    }
}
