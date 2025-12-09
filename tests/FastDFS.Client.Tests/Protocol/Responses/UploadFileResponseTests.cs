using FastDFS.Client.Protocol;
using FastDFS.Client.Protocol.Responses;
using FluentAssertions;
using System.Text;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Responses
{
    /// <summary>
    /// Unit tests for UploadFileResponse.
    /// </summary>
    public class UploadFileResponseTests
    {
        [Fact]
        public void Decode_ShouldParseCorrectly()
        {
            // Arrange
            var response = new UploadFileResponse();
            string fileName = "M00/00/00/test.jpg";
            var header = new FastDFSHeader(16 + fileName.Length, 0, 0); // 16 (group) + file path length

            // Create body: group_name (16 bytes) + file_name (exact length)
            byte[] body = new byte[16 + fileName.Length];

            // Group name: "group1" padded to 16 bytes
            string groupName = "group1";
            Encoding.UTF8.GetBytes(groupName).CopyTo(body, 0);

            // File name: exact length, no padding
            Encoding.UTF8.GetBytes(fileName).CopyTo(body, 16);

            // Act
            response.Decode(header, body);

            // Assert
            response.IsSuccess.Should().BeTrue();
            response.GroupName.Should().Be(groupName);
            response.FileName.Should().Be(fileName);
        }

        [Fact]
        public void Decode_WithPaddedGroupName_ShouldTrimNullBytes()
        {
            // Arrange
            var response = new UploadFileResponse();
            string fileName = "M00/00/00/abc.png";
            var header = new FastDFSHeader(16 + fileName.Length, 0, 0);

            byte[] body = new byte[16 + fileName.Length];

            // Group name with padding
            Encoding.UTF8.GetBytes("g1").CopyTo(body, 0);
            // Rest is null bytes (already zeroed)

            // File name (exact length)
            Encoding.UTF8.GetBytes(fileName).CopyTo(body, 16);

            // Act
            response.Decode(header, body);

            // Assert
            response.GroupName.Should().Be("g1");
            response.FileName.Should().Be(fileName);
        }

        [Fact]
        public void Decode_WithErrorStatus_ShouldMarkAsNotSuccess()
        {
            // Arrange
            var response = new UploadFileResponse();
            var header = new FastDFSHeader(0, 0, 2); // Status = 2 (error)

            // Act
            response.Decode(header, null);

            // Assert
            response.IsSuccess.Should().BeFalse();
        }

        [Fact]
        public void Decode_WithLongFileName_ShouldParseCorrectly()
        {
            // Arrange
            var response = new UploadFileResponse();
            string fileName = "M00/00/00/wKgBaGVlYWRlYWRlYWRlYWRlYWRlYWRlYWRlYS5qcGc";
            var header = new FastDFSHeader(16 + fileName.Length, 0, 0);

            byte[] body = new byte[16 + fileName.Length];
            Encoding.UTF8.GetBytes("group1").CopyTo(body, 0);
            Encoding.UTF8.GetBytes(fileName).CopyTo(body, 16);

            // Act
            response.Decode(header, body);

            // Assert
            response.GroupName.Should().Be("group1");
            response.FileName.Should().Be(fileName);
        }

        [Fact]
        public void IsSuccess_WithStatusZero_ShouldBeTrue()
        {
            // Arrange
            var response = new UploadFileResponse();
            var header = new FastDFSHeader(39, 0, 0);
            byte[] body = new byte[39];

            // Act
            response.Decode(header, body);

            // Assert
            response.IsSuccess.Should().BeTrue();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(22)]
        [InlineData(255)]
        public void IsSuccess_WithNonZeroStatus_ShouldBeFalse(byte status)
        {
            // Arrange
            var response = new UploadFileResponse();
            var header = new FastDFSHeader(0, 0, status);

            // Act
            response.Decode(header, null);

            // Assert
            response.IsSuccess.Should().BeFalse();
        }
    }
}
