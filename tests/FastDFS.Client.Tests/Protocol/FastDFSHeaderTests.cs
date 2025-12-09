using FastDFS.Client.Protocol;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol
{
    /// <summary>
    /// Unit tests for FastDFSHeader class.
    /// </summary>
    public class FastDFSHeaderTests
    {
        [Fact]
        public void Constructor_ShouldInitializeProperties()
        {
            // Arrange
            long bodyLength = 1024;
            byte command = 101;
            byte status = 0;

            // Act
            var header = new FastDFSHeader(bodyLength, command, status);

            // Assert
            header.BodyLength.Should().Be(bodyLength);
            header.Command.Should().Be(command);
            header.Status.Should().Be(status);
        }

        [Fact]
        public void ToBytes_ShouldSerializeCorrectly()
        {
            // Arrange
            var header = new FastDFSHeader(1024, 101, 0);

            // Act
            byte[] bytes = header.ToBytes();

            // Assert
            bytes.Should().HaveCount(FastDFSHeader.HeaderSize);
            bytes.Should().HaveCount(10);

            // First 8 bytes: body length (big-endian)
            // 1024 = 0x0000000000000400
            bytes[0].Should().Be(0x00);
            bytes[1].Should().Be(0x00);
            bytes[2].Should().Be(0x00);
            bytes[3].Should().Be(0x00);
            bytes[4].Should().Be(0x00);
            bytes[5].Should().Be(0x00);
            bytes[6].Should().Be(0x04);
            bytes[7].Should().Be(0x00);

            // Byte 8: command
            bytes[8].Should().Be(101);

            // Byte 9: status
            bytes[9].Should().Be(0);
        }

        [Fact]
        public void Parse_ShouldDeserializeCorrectly()
        {
            // Arrange
            byte[] bytes = new byte[10]
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, // body length = 1024 (big-endian)
                101, // command
                0    // status
            };

            // Act
            var header = FastDFSHeader.Parse(bytes, 0);

            // Assert
            header.BodyLength.Should().Be(1024);
            header.Command.Should().Be(101);
            header.Status.Should().Be(0);
        }

        [Fact]
        public void Parse_WithOffset_ShouldDeserializeCorrectly()
        {
            // Arrange
            byte[] bytes = new byte[20];
            // Header starts at offset 5
            bytes[5] = 0x00;
            bytes[6] = 0x00;
            bytes[7] = 0x00;
            bytes[8] = 0x00;
            bytes[9] = 0x00;
            bytes[10] = 0x00;
            bytes[11] = 0x02;
            bytes[12] = 0x00; // body length = 512
            bytes[13] = 102;  // command
            bytes[14] = 1;    // status

            // Act
            var header = FastDFSHeader.Parse(bytes, 5);

            // Assert
            header.BodyLength.Should().Be(512);
            header.Command.Should().Be(102);
            header.Status.Should().Be(1);
        }

        [Fact]
        public void ToBytes_AndParse_ShouldRoundTripCorrectly()
        {
            // Arrange
            var originalHeader = new FastDFSHeader(123456, 103, 2);

            // Act
            byte[] bytes = originalHeader.ToBytes();
            var parsedHeader = FastDFSHeader.Parse(bytes, 0);

            // Assert
            parsedHeader.BodyLength.Should().Be(originalHeader.BodyLength);
            parsedHeader.Command.Should().Be(originalHeader.Command);
            parsedHeader.Status.Should().Be(originalHeader.Status);
        }

        [Fact]
        public void ToBytes_WithZeroBodyLength_ShouldSerializeCorrectly()
        {
            // Arrange
            var header = new FastDFSHeader(0, 101, 0);

            // Act
            byte[] bytes = header.ToBytes();

            // Assert
            bytes.Should().HaveCount(10);
            for (int i = 0; i < 8; i++)
            {
                bytes[i].Should().Be(0);
            }
            bytes[8].Should().Be(101);
            bytes[9].Should().Be(0);
        }

        [Fact]
        public void ToBytes_WithMaxBodyLength_ShouldSerializeCorrectly()
        {
            // Arrange
            long maxLength = long.MaxValue;
            var header = new FastDFSHeader(maxLength, 101, 0);

            // Act
            byte[] bytes = header.ToBytes();

            // Assert
            bytes.Should().HaveCount(10);

            // Parse it back
            var parsed = FastDFSHeader.Parse(bytes, 0);
            parsed.BodyLength.Should().Be(maxLength);
        }

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(1, 1, 1)]
        [InlineData(1024, 101, 0)]
        [InlineData(1048576, 102, 2)]
        [InlineData(9999999, 255, 255)]
        public void ToBytes_AndParse_VariousValues_ShouldRoundTripCorrectly(long bodyLength, byte command, byte status)
        {
            // Arrange
            var header = new FastDFSHeader(bodyLength, command, status);

            // Act
            byte[] bytes = header.ToBytes();
            var parsed = FastDFSHeader.Parse(bytes, 0);

            // Assert
            parsed.BodyLength.Should().Be(bodyLength);
            parsed.Command.Should().Be(command);
            parsed.Status.Should().Be(status);
        }

        [Fact]
        public void HeaderSize_ShouldBe10()
        {
            // Assert
            FastDFSHeader.HeaderSize.Should().Be(10);
        }
    }
}
