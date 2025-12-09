using FastDFS.Client.Configuration;
using FastDFS.Client.Connection;
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

        // Note: Tests that require actual network connections are in integration tests
        // These unit tests focus on pool logic and state management
    }
}
