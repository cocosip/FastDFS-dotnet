using System;
using FastDFS.Client.Protocol.Requests;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Requests
{
    public class ListStorageServersRequestTests
    {
        [Fact]
        public void Encode_WithGroupOnly_ShouldWriteSingleFixedField()
        {
            var request = new ListStorageServersRequest
            {
                GroupName = "group1"
            };

            byte[] encoded = request.Encode();

            encoded.Should().HaveCount(10 + 16);
            encoded[10].Should().Be((byte)'g');
            encoded[15].Should().Be((byte)'1');
            encoded[25].Should().Be(0);
        }

        [Fact]
        public void Encode_WithGroupAndServerId_ShouldWriteBothFixedFields()
        {
            var request = new ListStorageServersRequest
            {
                GroupName = "group1",
                StorageServerId = "192.168.0.10"
            };

            byte[] encoded = request.Encode();

            encoded.Should().HaveCount(10 + 32);
            encoded[26].Should().Be((byte)'1');
            encoded[37].Should().Be((byte)'0');
        }

        [Fact]
        public void Encode_WithOversizedGroupName_ShouldThrowArgumentException()
        {
            var request = new ListStorageServersRequest
            {
                GroupName = "12345678901234567"
            };

            Action act = () => request.Encode();

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Encode_WithOversizedStorageServerId_ShouldThrowArgumentException()
        {
            var request = new ListStorageServersRequest
            {
                GroupName = "group1",
                StorageServerId = "12345678901234567"
            };

            Action act = () => request.Encode();

            act.Should().Throw<ArgumentException>();
        }
    }
}
