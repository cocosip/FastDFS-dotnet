using System;
using FastDFS.Client.Protocol.Encoding;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Encoding
{
    public class ProtocolFieldLengthGuardTests
    {
        [Fact]
        public void EnsureUtf8FitsFixedField_WithExactBoundary_ShouldNotThrow()
        {
            Action act = () => ProtocolFieldLengthGuard.EnsureUtf8FitsFixedField("groupName", "group1", 16);

            act.Should().NotThrow();
        }

        [Fact]
        public void EnsureUtf8FitsFixedField_WithOversizedAsciiValue_ShouldThrowArgumentException()
        {
            Action act = () => ProtocolFieldLengthGuard.EnsureUtf8FitsFixedField("groupName", "12345678901234567", 16);

            act.Should().Throw<ArgumentException>()
                .WithMessage("*groupName*16*");
        }

        [Fact]
        public void EnsureUtf8FitsFixedField_WithNullValue_ShouldThrowArgumentNullException()
        {
            Action act = () => ProtocolFieldLengthGuard.EnsureUtf8FitsFixedField("fileExtension", null!, 6);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("value");
        }
    }
}
