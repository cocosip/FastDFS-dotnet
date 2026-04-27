using System;
using FastDFS.Client.Exceptions;
using FastDFS.Client.Protocol;
using FastDFS.Client.Protocol.Responses;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Responses
{
    public class QueryStoreAllResponseTests
    {
        [Fact]
        public void Decode_WithMisalignedBody_ShouldThrowFastDFSProtocolException()
        {
            var response = new QueryStoreAllResponse();
            var header = new FastDFSHeader(41, 107, 0);

            Action act = () => response.Decode(header, new byte[41]);

            act.Should().Throw<FastDFSProtocolException>();
        }

        [Fact]
        public void Decode_WithSingleBlock_ShouldParseServer()
        {
            var response = new QueryStoreAllResponse();
            var body = new byte[40];
            System.Text.Encoding.UTF8.GetBytes("group1").CopyTo(body, 0);
            System.Text.Encoding.UTF8.GetBytes("192.168.0.10").CopyTo(body, 16);
            FastDFS.Client.Utilities.ByteConverter.WriteInt64(23000, body, 31);
            body[39] = 1;

            response.Decode(new FastDFSHeader(body.Length, 107, 0), body);

            response.ServerInfos.Should().HaveCount(1);
            response.ServerInfos[0].GroupName.Should().Be("group1");
            response.ServerInfos[0].IpAddress.Should().Be("192.168.0.10");
            response.ServerInfos[0].Port.Should().Be(23000);
            response.ServerInfos[0].StorePathIndex.Should().Be(1);
        }
    }
}
