using System;
using FastDFS.Client.Domain.Identifiers;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Domain.Identifiers
{
    public class StorageServerIdTests
    {
        [Fact]
        public void Create_WithNull_ShouldThrowArgumentNullException()
        {
            Action act = () => StorageServerId.Create(null!);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Create_WithOversizedUtf8Value_ShouldThrowArgumentException()
        {
            Action act = () => StorageServerId.Create("12345678901234567");

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Create_WithValidValue_ShouldPreserveValue()
        {
            var serverId = StorageServerId.Create("192.168.0.10");

            serverId.Value.Should().Be("192.168.0.10");
        }
    }
}
