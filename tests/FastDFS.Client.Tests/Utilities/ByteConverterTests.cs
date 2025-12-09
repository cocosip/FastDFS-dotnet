using FastDFS.Client.Utilities;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Utilities
{
    /// <summary>
    /// Unit tests for ByteConverter class (big-endian conversion).
    /// </summary>
    public class ByteConverterTests
    {
        #region Int32 Tests

        [Fact]
        public void ToInt32_FromBigEndianBytes_ShouldConvertCorrectly()
        {
            // Arrange
            byte[] bytes = new byte[] { 0x00, 0x00, 0x04, 0x00 }; // 1024 in big-endian

            // Act
            int result = ByteConverter.ToInt32(bytes, 0);

            // Assert
            result.Should().Be(1024);
        }

        [Fact]
        public void ToInt32_WithOffset_ShouldConvertCorrectly()
        {
            // Arrange
            byte[] bytes = new byte[] { 0xFF, 0xFF, 0x00, 0x00, 0x04, 0x00, 0xFF }; // 1024 at offset 2

            // Act
            int result = ByteConverter.ToInt32(bytes, 2);

            // Assert
            result.Should().Be(1024);
        }

        [Fact]
        public void ToBytes_Int32_ShouldConvertToBigEndian()
        {
            // Arrange
            int value = 1024;

            // Act
            byte[] bytes = ByteConverter.ToBytes(value);

            // Assert
            bytes.Should().HaveCount(4);
            bytes[0].Should().Be(0x00);
            bytes[1].Should().Be(0x00);
            bytes[2].Should().Be(0x04);
            bytes[3].Should().Be(0x00);
        }

        [Theory]
        [InlineData(0, new byte[] { 0x00, 0x00, 0x00, 0x00 })]
        [InlineData(1, new byte[] { 0x00, 0x00, 0x00, 0x01 })]
        [InlineData(256, new byte[] { 0x00, 0x00, 0x01, 0x00 })]
        [InlineData(65536, new byte[] { 0x00, 0x01, 0x00, 0x00 })]
        [InlineData(16777216, new byte[] { 0x01, 0x00, 0x00, 0x00 })]
        [InlineData(-1, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF })]
        public void ToBytes_Int32_VariousValues_ShouldConvertCorrectly(int value, byte[] expected)
        {
            // Act
            byte[] result = ByteConverter.ToBytes(value);

            // Assert
            result.Should().Equal(expected);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(1024)]
        [InlineData(-1024)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public void Int32_RoundTrip_ShouldPreserveValue(int original)
        {
            // Act
            byte[] bytes = ByteConverter.ToBytes(original);
            int result = ByteConverter.ToInt32(bytes, 0);

            // Assert
            result.Should().Be(original);
        }

        #endregion

        #region Int64 Tests

        [Fact]
        public void ToInt64_FromBigEndianBytes_ShouldConvertCorrectly()
        {
            // Arrange
            byte[] bytes = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00 }; // 1024 in big-endian

            // Act
            long result = ByteConverter.ToInt64(bytes, 0);

            // Assert
            result.Should().Be(1024);
        }

        [Fact]
        public void ToInt64_WithOffset_ShouldConvertCorrectly()
        {
            // Arrange
            byte[] bytes = new byte[] { 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0xFF }; // 1024 at offset 1

            // Act
            long result = ByteConverter.ToInt64(bytes, 1);

            // Assert
            result.Should().Be(1024);
        }

        [Fact]
        public void ToBytes_Int64_ShouldConvertToBigEndian()
        {
            // Arrange
            long value = 1024;

            // Act
            byte[] bytes = ByteConverter.ToBytes(value);

            // Assert
            bytes.Should().HaveCount(8);
            bytes[0].Should().Be(0x00);
            bytes[1].Should().Be(0x00);
            bytes[2].Should().Be(0x00);
            bytes[3].Should().Be(0x00);
            bytes[4].Should().Be(0x00);
            bytes[5].Should().Be(0x00);
            bytes[6].Should().Be(0x04);
            bytes[7].Should().Be(0x00);
        }

        [Theory]
        [InlineData(0L, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 })]
        [InlineData(1L, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01 })]
        [InlineData(256L, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00 })]
        [InlineData(4294967296L, new byte[] { 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00 })]
        [InlineData(-1L, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF })]
        public void ToBytes_Int64_VariousValues_ShouldConvertCorrectly(long value, byte[] expected)
        {
            // Act
            byte[] result = ByteConverter.ToBytes(value);

            // Assert
            result.Should().Equal(expected);
        }

        [Theory]
        [InlineData(0L)]
        [InlineData(1L)]
        [InlineData(1024L)]
        [InlineData(-1024L)]
        [InlineData(1048576L)]
        [InlineData(9999999999L)]
        [InlineData(long.MaxValue)]
        [InlineData(long.MinValue)]
        public void Int64_RoundTrip_ShouldPreserveValue(long original)
        {
            // Act
            byte[] bytes = ByteConverter.ToBytes(original);
            long result = ByteConverter.ToInt64(bytes, 0);

            // Assert
            result.Should().Be(original);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void ToInt32_MaxValue_ShouldConvertCorrectly()
        {
            // Arrange
            byte[] bytes = new byte[] { 0x7F, 0xFF, 0xFF, 0xFF };

            // Act
            int result = ByteConverter.ToInt32(bytes, 0);

            // Assert
            result.Should().Be(int.MaxValue);
        }

        [Fact]
        public void ToInt32_MinValue_ShouldConvertCorrectly()
        {
            // Arrange
            byte[] bytes = new byte[] { 0x80, 0x00, 0x00, 0x00 };

            // Act
            int result = ByteConverter.ToInt32(bytes, 0);

            // Assert
            result.Should().Be(int.MinValue);
        }

        [Fact]
        public void ToInt64_MaxValue_ShouldConvertCorrectly()
        {
            // Arrange
            byte[] bytes = new byte[] { 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

            // Act
            long result = ByteConverter.ToInt64(bytes, 0);

            // Assert
            result.Should().Be(long.MaxValue);
        }

        [Fact]
        public void ToInt64_MinValue_ShouldConvertCorrectly()
        {
            // Arrange
            byte[] bytes = new byte[] { 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

            // Act
            long result = ByteConverter.ToInt64(bytes, 0);

            // Assert
            result.Should().Be(long.MinValue);
        }

        #endregion
    }
}
