using System;
using FastDFS.Client.Exceptions;
using FastDFS.Client.Protocol;
using FastDFS.Client.Protocol.Responses;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Responses
{
    public class ListAllGroupsResponseTests
    {
        [Fact]
        public void Decode_WithMisalignedBody_ShouldThrowFastDFSProtocolException()
        {
            var response = new ListAllGroupsResponse();
            var header = new FastDFSHeader(106, 91, 0);

            Action act = () => response.Decode(header, new byte[106]);

            act.Should().Throw<FastDFSProtocolException>();
        }

        [Fact]
        public void Decode_WithSingleBlock_ShouldParseGroup()
        {
            var response = new ListAllGroupsResponse();
            var body = new byte[105];
            System.Text.Encoding.UTF8.GetBytes("group1").CopyTo(body, 0);
            FastDFS.Client.Utilities.ByteConverter.WriteInt64(1024, body, 16);
            FastDFS.Client.Utilities.ByteConverter.WriteInt64(512, body, 24);

            response.Decode(new FastDFSHeader(body.Length, 91, 0), body);

            response.Groups.Should().HaveCount(1);
            response.Groups[0].GroupName.Should().Be("group1");
            response.Groups[0].TotalMB.Should().Be(1024);
            response.Groups[0].FreeMB.Should().Be(512);
        }
    }
}
