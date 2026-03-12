using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FastDFS.Client.Configuration;
using FastDFS.Client.Connection;
using FastDFS.Client.Protocol;
using FastDFS.Client.Storage;
using FastDFS.Client.Tracker;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Connection
{
    /// <summary>
    /// Unit tests for ConnectionPool.
    /// Note: These tests don't connect to a real server, they test pool logic.
    /// </summary>
    public class ConnectionPoolTests
    {
        private ConnectionPoolConfiguration CreateTestConfig()
        {
            return new ConnectionPoolConfiguration
            {
                MaxConnectionPerServer = 10,
                MinConnectionPerServer = 2,
                ConnectionIdleTimeout = 300,
                ConnectionLifetime = 3600,
                ConnectionTimeout = 5000,
                SendTimeout = 5000,
                ReceiveTimeout = 5000
            };
        }

        [Fact]
        public void Constructor_ShouldInitializeWithCorrectParameters()
        {
            // Arrange
            var config = CreateTestConfig();

            // Act
            using var pool = new ConnectionPool("localhost", 22122, config);

            // Assert
            pool.TotalConnections.Should().Be(0);
            pool.IdleConnections.Should().Be(0);
            pool.ActiveConnections.Should().Be(0);
        }

        [Fact]
        public void Constructor_WithInvalidHost_ShouldThrow()
        {
            // Arrange
            var config = CreateTestConfig();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => new ConnectionPool("", 22122, config));
            Assert.Throws<ArgumentException>(() => new ConnectionPool(null!, 22122, config));
        }

        [Fact]
        public void Constructor_WithInvalidPort_ShouldThrow()
        {
            // Arrange
            var config = CreateTestConfig();

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => new ConnectionPool("localhost", 0, config));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ConnectionPool("localhost", -1, config));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ConnectionPool("localhost", 65536, config));
        }

        [Fact]
        public void Constructor_WithNullConfig_ShouldThrow()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ConnectionPool("localhost", 22122, null!));
        }

        [Fact]
        public void TotalConnections_InitiallyZero()
        {
            // Arrange
            var config = CreateTestConfig();
            using var pool = new ConnectionPool("localhost", 22122, config);

            // Assert
            pool.TotalConnections.Should().Be(0);
        }

        [Fact]
        public void IdleConnections_InitiallyZero()
        {
            // Arrange
            var config = CreateTestConfig();
            using var pool = new ConnectionPool("localhost", 22122, config);

            // Assert
            pool.IdleConnections.Should().Be(0);
        }

        [Fact]
        public void ActiveConnections_InitiallyZero()
        {
            // Arrange
            var config = CreateTestConfig();
            using var pool = new ConnectionPool("localhost", 22122, config);

            // Assert
            pool.ActiveConnections.Should().Be(0);
        }

        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var config = CreateTestConfig();
            using var pool = new ConnectionPool("localhost", 22122, config);

            // Act
            string result = pool.ToString();

            // Assert
            result.Should().Contain("localhost:22122");
            result.Should().Contain("Total=0");
            result.Should().Contain("Idle=0");
            result.Should().Contain("Active=0");
        }

        [Fact]
        public void Dispose_ShouldNotThrowWhenCalledMultipleTimes()
        {
            // Arrange
            var config = CreateTestConfig();
            var pool = new ConnectionPool("localhost", 22122, config);

            // Act & Assert
            pool.Dispose();
            pool.Dispose(); // Should not throw
        }

        [Fact]
        public void Dispose_ShouldResetCounters()
        {
            // Arrange
            var config = CreateTestConfig();
            var pool = new ConnectionPool("localhost", 22122, config);

            // Act
            pool.Dispose();

            // Assert
            pool.TotalConnections.Should().Be(0);
            pool.IdleConnections.Should().Be(0);
            pool.ActiveConnections.Should().Be(0);
        }

        [Fact]
        public async Task GetConnectionAsync_AfterDispose_ShouldThrow()
        {
            // Arrange
            var config = CreateTestConfig();
            var pool = new ConnectionPool("localhost", 22122, config);
            pool.Dispose();

            // Act & Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await pool.GetConnectionAsync());
        }

        [Fact]
        public void ReturnConnection_WithNullConnection_ShouldThrow()
        {
            // Arrange
            var config = CreateTestConfig();
            using var pool = new ConnectionPool("localhost", 22122, config);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => pool.ReturnConnection(null!));
        }

        [Fact]
        public void ReturnConnection_WithForeignConnection_ShouldBeIgnoredWithoutCorruptingCounters()
        {
            // Arrange
            var config = CreateTestConfig();
            using var pool = new ConnectionPool("localhost", 22122, config);
            using var foreignConnection = new FastDFSConnection("localhost", 22122);

            // Act
            pool.ReturnConnection(foreignConnection);

            // Assert
            pool.TotalConnections.Should().Be(0);
            pool.IdleConnections.Should().Be(0);
            pool.ActiveConnections.Should().Be(0);
        }

        [Fact]
        public void FastDFSClientManager_WithIdenticalConfigurations_ShouldReturnDistinctLogicalClientsWithOwnNames()
        {
            using var manager = new FastDFSClientManager();

            var configuration = new FastDFSConfiguration
            {
                TrackerServers = new List<string> { "127.0.0.1:22122" },
                ConnectionPool = new ConnectionPoolConfiguration()
            };

            manager.AddClient("cluster-a", configuration);
            manager.AddClient("cluster-b", configuration.Clone());

            var clientA = manager.GetClient("cluster-a");
            var clientB = manager.GetClient("cluster-b");

            clientA.Should().NotBeSameAs(clientB);
            clientA.Name.Should().Be("cluster-a");
            clientB.Name.Should().Be("cluster-b");
        }

        [Fact]
        public void FastDFSClientManager_RemoveClient_ForLazyRegisteredClient_ShouldReturnTrue()
        {
            using var manager = new FastDFSClientManager();

            manager.AddClient("lazy", new FastDFSConfiguration
            {
                TrackerServers = new List<string> { "127.0.0.1:22122" },
                ConnectionPool = new ConnectionPoolConfiguration()
            });

            var removed = manager.RemoveClient("lazy");

            removed.Should().BeTrue();
            manager.HasClient("lazy").Should().BeFalse();
        }

        [Fact]
        public async Task StorageClient_ServerBasedApis_WithNullServer_ShouldThrowArgumentNullException()
        {
            using var client = new StorageClient(new ConnectionPoolConfiguration());
            var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            var writable = new MemoryStream();
            var metadata = new FastDFSMetadata(new Dictionary<string, string> { ["k"] = "v" });

            await Assert.ThrowsAsync<ArgumentNullException>(() => client.UploadAsync(null!, new byte[] { 1 }, "txt"));
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.UploadAsync(null!, stream, stream.Length, "txt"));
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.UploadAppenderFileAsync(null!, new byte[] { 1 }, "txt"));
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.AppendFileAsync(null!, "file", new byte[] { 1 }));
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.DownloadAsync(null!, "group1", "file", 0, 0));
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.DownloadAsync(null!, "group1", "file", writable, 0, 0));
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.DeleteAsync(null!, "group1", "file"));
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.QueryFileInfoAsync(null!, "group1", "file"));
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.SetMetadataAsync(null!, "group1", "file", metadata, MetadataFlag.Overwrite));
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetMetadataAsync(null!, "group1", "file"));
        }

        // Note: Tests that require actual network connections are in integration tests
        // These unit tests focus on pool logic and state management
    }
}
