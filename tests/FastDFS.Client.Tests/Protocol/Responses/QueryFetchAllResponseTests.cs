using System;
using FastDFS.Client.Exceptions;
using FastDFS.Client.Protocol;
using FastDFS.Client.Protocol.Responses;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Responses
{
    public class QueryFetchAllResponseTests
    {
        [Fact]
        public void Decode_WithMisalignedBody_ShouldThrowFastDFSProtocolException()
        {
            var response = new QueryFetchAllResponse();
            var header = new FastDFSHeader(40, 106, 0);

            Action act = () => response.Decode(header, new byte[40]);

            act.Should().Throw<FastDFSProtocolException>();
        }

        [Fact]
        public void Decode_WithSingleBlock_ShouldParseServer()
        {
            var response = new QueryFetchAllResponse();
            var body = new byte[39];
            System.Text.Encoding.UTF8.GetBytes("group1").CopyTo(body, 0);
            System.Text.Encoding.UTF8.GetBytes("192.168.0.20").CopyTo(body, 16);
            FastDFS.Client.Utilities.ByteConverter.WriteInt64(23000, body, 31);

            response.Decode(new FastDFSHeader(body.Length, 106, 0), body);

            response.ServerInfos.Should().HaveCount(1);
            response.ServerInfos[0].GroupName.Should().Be("group1");
            response.ServerInfos[0].IpAddress.Should().Be("192.168.0.20");
            response.ServerInfos[0].Port.Should().Be(23000);
            response.ServerInfos[0].StorePathIndex.Should().Be(0);
        }
    }
}
