using FastDFS.Client.Protocol.Encoding;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Encoding
{
    public class ProtocolFieldWriterTests
    {
        [Fact]
        public void WriteFixedUtf8_WithEmptyString_ShouldZeroFillTheField()
        {
            var buffer = new byte[6];

            ProtocolFieldWriter.WriteFixedUtf8(buffer, 0, 6, "fileExtension", string.Empty);

            buffer.Should().Equal(new byte[] { 0, 0, 0, 0, 0, 0 });
        }

        [Fact]
        public void WriteFixedUtf8_WithShortAsciiValue_ShouldWriteAndPad()
        {
            var buffer = new byte[6];

            ProtocolFieldWriter.WriteFixedUtf8(buffer, 0, 6, "fileExtension", "jpg");

            buffer[0].Should().Be((byte)'j');
            buffer[1].Should().Be((byte)'p');
            buffer[2].Should().Be((byte)'g');
            buffer[3].Should().Be(0);
            buffer[4].Should().Be(0);
            buffer[5].Should().Be(0);
        }
    }
}
