using System;
using FastDFS.Client.Protocol;
using FastDFS.Client.Protocol.Responses;
using FastDFS.Client.Tracker;
using FastDFS.Client.Utilities;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Responses
{
    /// <summary>
    /// Unit tests for ListStorageServersResponse.
    /// </summary>
    public class ListStorageServersResponseTests
    {
        [Fact]
        public void Decode_WithInvalidBodyLength_ShouldThrowProtocolException()
        {
            // Arrange
            var response = new ListStorageServersResponse();
            var header = new FastDFSHeader(1, 0, 0);
            byte[] body = new byte[1];

            // Act
            Action act = () => response.Decode(header, body);

            // Assert
            act.Should().Throw<FastDFS.Client.Exceptions.FastDFSProtocolException>();
        }

        [Fact]
        public void Decode_WithSingleServerBlock_ShouldParseSuccessfully()
        {
            // Arrange
            var response = new ListStorageServersResponse();
            byte[] body = new byte[600];
            body[0] = (byte)StorageServerStatus.Active;
            WriteFixedString(body, 1, 16, "192.168.0.10");
            WriteFixedString(body, 17, 16, "192.168.0.11");
            WriteFixedString(body, 33, 128, "storage-a.example.com");
            WriteFixedString(body, 161, 6, "6.12");
            WriteInt64(body, 167, 1700000000L);
            WriteInt64(body, 175, 1700000060L);
            WriteInt64(body, 183, 1024);
            WriteInt64(body, 191, 512);
            WriteInt64(body, 199, 10);
            WriteInt64(body, 207, 2);
            WriteInt64(body, 215, 256);
            WriteInt64(body, 223, 1);
            WriteInt64(body, 231, 23000);
            WriteInt64(body, 239, 8080);
            WriteInt64(body, 247, 11);
            WriteInt64(body, 255, 10);
            WriteInt64(body, 263, 9);
            WriteInt64(body, 271, 8);
            WriteInt64(body, 279, 7);
            WriteInt64(body, 287, 6);
            WriteInt64(body, 295, 5);
            WriteInt64(body, 303, 4);
            WriteInt64(body, 311, 3);
            WriteInt64(body, 319, 2);
            WriteInt64(body, 327, 1);
            WriteInt64(body, 335, 1);
            WriteInt64(body, 343, 20);
            WriteInt64(body, 351, 19);
            WriteInt64(body, 359, 18);
            WriteInt64(body, 367, 17);
            WriteInt64(body, 375, 1700000120L);
            WriteInt64(body, 383, 1700000180L);

            var header = new FastDFSHeader(body.Length, 0, 0);

            // Act
            response.Decode(header, body);

            // Assert
            response.Servers.Should().HaveCount(1);
            var server = response.Servers[0];
            server.Status.Should().Be(StorageServerStatus.Active);
            server.Id.Should().Be("192.168.0.10");
            server.IpAddress.Should().Be("192.168.0.10");
            server.SourceIpAddress.Should().Be("192.168.0.11");
            server.DomainName.Should().Be("storage-a.example.com");
            server.Version.Should().Be("6.12");
            server.StoragePort.Should().Be(23000);
            server.StorageHttpPort.Should().Be(8080);
            server.TotalMB.Should().Be(1024);
            server.FreeMB.Should().Be(512);
        }

        [Fact]
        public void Decode_With592ByteServerBlock_ShouldParseSuccessfully()
        {
            var response = new ListStorageServersResponse();
            byte[] body = new byte[592];
            body[0] = (byte)StorageServerStatus.Active;
            WriteFixedString(body, 1, 16, "192.168.0.12");
            WriteFixedString(body, 17, 16, "192.168.0.13");
            WriteFixedString(body, 33, 128, "storage-b.example.com");
            WriteFixedString(body, 161, 6, "6.11");
            WriteInt64(body, 167, 1700000000L);
            WriteInt64(body, 175, 1700000060L);
            WriteInt64(body, 183, 2048);
            WriteInt64(body, 191, 1024);
            WriteInt64(body, 231, 23000);
            WriteInt64(body, 239, 8080);
            WriteInt64(body, 375, 1700000120L);
            WriteInt64(body, 383, 1700000180L);

            var header = new FastDFSHeader(body.Length, 0, 0);

            response.Decode(header, body);

            response.Servers.Should().HaveCount(1);
            response.Servers[0].Id.Should().Be("192.168.0.12");
            response.Servers[0].DomainName.Should().Be("storage-b.example.com");
            response.Servers[0].TotalMB.Should().Be(2048);
            response.Servers[0].FreeMB.Should().Be(1024);
        }

        private static void WriteFixedString(byte[] buffer, int offset, int length, string value)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(value);
            Array.Copy(bytes, 0, buffer, offset, Math.Min(bytes.Length, length));
        }

        private static void WriteInt64(byte[] buffer, int offset, long value)
        {
            ByteConverter.WriteInt64(value, buffer, offset);
        }
    }
}
