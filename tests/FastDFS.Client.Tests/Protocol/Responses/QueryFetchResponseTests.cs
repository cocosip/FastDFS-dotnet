using System;
using FastDFS.Client.Exceptions;
using FastDFS.Client.Protocol;
using FastDFS.Client.Protocol.Responses;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Responses
{
    public class QueryFetchResponseTests
    {
        [Fact]
        public void Decode_WithShortBody_ShouldThrowFastDFSProtocolException()
        {
            var response = new QueryFetchResponse();
            var header = new FastDFSHeader(38, 102, 0);

            Action act = () => response.Decode(header, new byte[38]);

            act.Should().Throw<FastDFSProtocolException>();
        }
    }
}
