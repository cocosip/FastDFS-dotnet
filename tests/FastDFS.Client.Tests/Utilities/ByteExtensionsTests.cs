using System;
using FastDFS.Client.Utilities;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Utilities
{
    public class ByteExtensionsTests
    {
        [Fact]
        public void CopyFixedString_WithOverlongValue_ShouldThrow()
        {
            var buffer = new byte[4];

            Action act = () => ByteExtensions.CopyFixedString("abcde", buffer, 0, 4);

            act.Should().Throw<ArgumentException>()
                .WithMessage("*cannot exceed 4 bytes*");
        }
    }
}
