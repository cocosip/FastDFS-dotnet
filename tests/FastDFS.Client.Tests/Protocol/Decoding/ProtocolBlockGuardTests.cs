using System;
using FastDFS.Client.Exceptions;
using FastDFS.Client.Protocol.Decoding;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Decoding
{
    public class ProtocolBlockGuardTests
    {
        [Fact]
        public void EnsureMinimumBodyLength_WithTooShortBody_ShouldThrowFastDFSProtocolException()
        {
            Action act = () => ProtocolBlockGuard.EnsureMinimumBodyLength("QueryFetchResponse", 38, 39);

            act.Should().Throw<FastDFSProtocolException>();
        }
    }
}
