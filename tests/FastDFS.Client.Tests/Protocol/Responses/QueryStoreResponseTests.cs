using System;
using FastDFS.Client.Exceptions;
using FastDFS.Client.Protocol;
using FastDFS.Client.Protocol.Responses;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Responses
{
    public class QueryStoreResponseTests
    {
        [Fact]
        public void Decode_WithShortBody_ShouldThrowFastDFSProtocolException()
        {
            var response = new QueryStoreResponse();
            var header = new FastDFSHeader(39, 100, 0);

            Action act = () => response.Decode(header, new byte[39]);

            act.Should().Throw<FastDFSProtocolException>();
        }
    }
}
