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

        [Fact]
        public void EnsureExactBlockMultiple_WithMisalignedLength_ShouldThrowFastDFSProtocolException()
        {
            Action act = () => ProtocolBlockGuard.EnsureExactBlockMultiple("QueryStoreAllResponse", 41, 40);

            act.Should().Throw<FastDFSProtocolException>();
        }

        [Fact]
        public void ResolveSupportedBlockSize_WithSupported592ByteLength_ShouldReturn592()
        {
            int blockSize = ProtocolBlockGuard.ResolveSupportedBlockSize("ListStorageServersResponse", 1184, new[] { 600, 592 });

            blockSize.Should().Be(592);
        }

        [Fact]
        public void ResolveSupportedBlockSize_WithUnsupportedLength_ShouldThrowFastDFSProtocolException()
        {
            Action act = () => ProtocolBlockGuard.ResolveSupportedBlockSize("ListStorageServersResponse", 601, new[] { 600, 592 });

            act.Should().Throw<FastDFSProtocolException>();
        }
    }
}
