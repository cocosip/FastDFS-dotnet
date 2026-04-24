using FastDFS.Client.Protocol.Decoding;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Decoding
{
    public class ProtocolFieldReaderTests
    {
        [Fact]
        public void ReadFixedUtf8_WithNullPadding_ShouldTrimTrailingNulls()
        {
            var buffer = new byte[16];
            System.Text.Encoding.UTF8.GetBytes("group1").CopyTo(buffer, 0);

            string value = ProtocolFieldReader.ReadFixedUtf8(buffer, 0, 16);

            value.Should().Be("group1");
        }

        [Fact]
        public void ReadFixedUtf8_WithOffset_ShouldReadRequestedSegment()
        {
            var buffer = new byte[32];
            System.Text.Encoding.UTF8.GetBytes("storage-a").CopyTo(buffer, 16);

            string value = ProtocolFieldReader.ReadFixedUtf8(buffer, 16, 16);

            value.Should().Be("storage-a");
        }

        [Fact]
        public void ReadFixedUtf8_WithAllZeroBytes_ShouldReturnEmptyString()
        {
            var buffer = new byte[16];

            string value = ProtocolFieldReader.ReadFixedUtf8(buffer, 0, 16);

            value.Should().BeEmpty();
        }
    }
}
