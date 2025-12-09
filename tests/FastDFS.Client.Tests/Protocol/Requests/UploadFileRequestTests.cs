using FastDFS.Client.Protocol;
using FastDFS.Client.Protocol.Requests;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Requests
{
    /// <summary>
    /// Unit tests for UploadFileRequest.
    /// </summary>
    public class UploadFileRequestTests
    {
        [Fact]
        public void Encode_ShouldSerializeCorrectly()
        {
            // Arrange
            var request = new UploadFileRequest
            {
                StorePathIndex = 0,
                FileContent = new byte[] { 0x01, 0x02, 0x03, 0x04 },
                FileExtension = "jpg"
            };

            // Act
            byte[] encoded = request.Encode();

            // Assert
            // Header (10 bytes) + Body
            // Body = 1 byte (store_path_index) + 8 bytes (file_size) + 6 bytes (file_ext_name) + file content
            int expectedLength = 10 + 1 + 8 + 6 + 4;
            encoded.Should().HaveCount(expectedLength);

            // Verify header
            var header = FastDFSHeader.Parse(encoded, 0);
            header.Command.Should().Be(StorageCommand.UploadFile);
            header.Status.Should().Be(0);
            header.BodyLength.Should().Be(1 + 8 + 6 + 4); // Body size

            // Verify store path index
            encoded[10].Should().Be(0);

            // Verify file extension (padded to 6 bytes)
            encoded[19].Should().Be((byte)'j');
            encoded[20].Should().Be((byte)'p');
            encoded[21].Should().Be((byte)'g');
            encoded[22].Should().Be(0);
            encoded[23].Should().Be(0);
            encoded[24].Should().Be(0);

            // Verify file content
            encoded[25].Should().Be(0x01);
            encoded[26].Should().Be(0x02);
            encoded[27].Should().Be(0x03);
            encoded[28].Should().Be(0x04);
        }

        [Fact]
        public void Encode_WithLongExtension_ShouldTruncateTo6Chars()
        {
            // Arrange
            var request = new UploadFileRequest
            {
                StorePathIndex = 1,
                FileContent = new byte[] { 0xFF },
                FileExtension = "verylongext" // More than 6 chars
            };

            // Act
            byte[] encoded = request.Encode();

            // Assert
            // File extension should be truncated to "verylo"
            encoded[19].Should().Be((byte)'v');
            encoded[20].Should().Be((byte)'e');
            encoded[21].Should().Be((byte)'r');
            encoded[22].Should().Be((byte)'y');
            encoded[23].Should().Be((byte)'l');
            encoded[24].Should().Be((byte)'o');
        }

        [Fact]
        public void Encode_WithMinimalContent_ShouldSerializeCorrectly()
        {
            // Arrange
            var request = new UploadFileRequest
            {
                StorePathIndex = 0,
                FileContent = new byte[] { 0x00 }, // 1 byte content
                FileExtension = "txt"
            };

            // Act
            byte[] encoded = request.Encode();

            // Assert
            int expectedLength = 10 + 1 + 8 + 6 + 1; // Header + store_path + file_size + ext + content
            encoded.Should().HaveCount(expectedLength);

            var header = FastDFSHeader.Parse(encoded, 0);
            header.BodyLength.Should().Be(1 + 8 + 6 + 1);
        }

        [Fact]
        public void Command_ShouldBeUploadFile()
        {
            // Arrange
            var request = new UploadFileRequest();

            // Assert
            request.Command.Should().Be(StorageCommand.UploadFile);
            request.Command.Should().Be(11);
        }
    }
}
