using FastDFS.Client.Connection;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Connection
{
    /// <summary>
    /// Unit tests for FastDFSConnection.
    /// Note: Tests that require actual network are in integration tests.
    /// </summary>
    public class FastDFSConnectionTests
    {
        [Fact]
        public void Constructor_ShouldInitializeWithCorrectParameters()
        {
            // Arrange & Act
            using var connection = new FastDFSConnection("localhost", 22122);

            // Assert
            connection.RemoteEndpoint.Should().Be("localhost:22122");
            connection.CreatedTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            connection.LastUsedTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void Constructor_WithCustomTimeouts_ShouldInitialize()
        {
            // Arrange & Act
            using var connection = new FastDFSConnection("localhost", 22122, sendTimeout: 10000, receiveTimeout: 20000);

            // Assert
            connection.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithInvalidHost_ShouldThrow()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new FastDFSConnection("", 22122));
            Assert.Throws<ArgumentException>(() => new FastDFSConnection("  ", 22122));
            Assert.Throws<ArgumentException>(() => new FastDFSConnection(null!, 22122));
        }

        [Fact]
        public void Constructor_WithInvalidPort_ShouldThrow()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => new FastDFSConnection("localhost", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FastDFSConnection("localhost", -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FastDFSConnection("localhost", 65536));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FastDFSConnection("localhost", 70000));
        }

        [Fact]
        public void IsAlive_BeforeConnect_ShouldBeFalse()
        {
            // Arrange
            using var connection = new FastDFSConnection("localhost", 22122);

            // Assert
            connection.IsAlive.Should().BeFalse();
        }

        [Fact]
        public void IsAlive_AfterDispose_ShouldBeFalse()
        {
            // Arrange
            var connection = new FastDFSConnection("localhost", 22122);

            // Act
            connection.Dispose();

            // Assert
            connection.IsAlive.Should().BeFalse();
        }

        [Fact]
        public void RemoteEndpoint_ShouldReturnCorrectFormat()
        {
            // Arrange
            using var connection = new FastDFSConnection("192.168.1.100", 22122);

            // Assert
            connection.RemoteEndpoint.Should().Be("192.168.1.100:22122");
        }

        [Fact]
        public void CreatedTime_ShouldBeSetOnConstruction()
        {
            // Arrange
            var beforeCreate = DateTime.UtcNow;

            // Act
            using var connection = new FastDFSConnection("localhost", 22122);
            var afterCreate = DateTime.UtcNow;

            // Assert
            connection.CreatedTime.Should().BeOnOrAfter(beforeCreate);
            connection.CreatedTime.Should().BeOnOrBefore(afterCreate);
        }

        [Fact]
        public void LastUsedTime_ShouldInitiallyEqualCreatedTime()
        {
            // Arrange & Act
            using var connection = new FastDFSConnection("localhost", 22122);

            // Assert
            connection.LastUsedTime.Should().Be(connection.CreatedTime);
        }

        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            // Arrange
            using var connection = new FastDFSConnection("localhost", 22122);

            // Act
            string result = connection.ToString();

            // Assert
            result.Should().Contain("localhost:22122");
            result.Should().Contain("Alive=False");
            result.Should().Contain("Created=");
            result.Should().Contain("LastUsed=");
        }

        [Fact]
        public void Dispose_ShouldNotThrowWhenCalledMultipleTimes()
        {
            // Arrange
            var connection = new FastDFSConnection("localhost", 22122);

            // Act & Assert
            connection.Dispose();
            connection.Dispose(); // Should not throw
        }

        [Fact]
        public void Close_ShouldCallDispose()
        {
            // Arrange
            var connection = new FastDFSConnection("localhost", 22122);

            // Act
            connection.Close();

            // Assert
            connection.IsAlive.Should().BeFalse();
        }

        [Fact]
        public async Task ConnectAsync_AfterDispose_ShouldThrow()
        {
            // Arrange
            var connection = new FastDFSConnection("localhost", 22122);
            connection.Dispose();

            // Act & Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await connection.ConnectAsync());
        }

        // Note: Actual connection tests require a running FastDFS server
        // and are placed in integration tests
    }
}
