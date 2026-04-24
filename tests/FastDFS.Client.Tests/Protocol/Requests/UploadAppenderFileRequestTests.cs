using FastDFS.Client.Protocol.Requests;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Requests
{
    public class UploadAppenderFileRequestTests
    {
        [Fact]
        public void Encode_WithEmptyExtension_ShouldZeroFillExtensionField()
        {
            var request = new UploadAppenderFileRequest
            {
                StorePathIndex = 0,
                FileContent = new byte[] { 0x02 },
                FileExtension = string.Empty
            };

            byte[] encoded = request.Encode();

            encoded[19].Should().Be(0);
            encoded[24].Should().Be(0);
        }
    }
}
