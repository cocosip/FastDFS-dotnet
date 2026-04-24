using System;
using FastDFS.Client.Protocol.Requests;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Requests
{
    public class DownloadFileRequestTests
    {
        [Fact]
        public void Encode_WithOversizedGroupName_ShouldThrowArgumentException()
        {
            var request = new DownloadFileRequest
            {
                GroupName = "12345678901234567",
                FileName = "M00/00/00/file.txt"
            };

            Action act = () => request.Encode();

            act.Should().Throw<ArgumentException>();
        }
    }
}
