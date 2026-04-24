using System;
using FastDFS.Client.Domain.Identifiers;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Domain.Identifiers
{
    public class GroupNameTests
    {
        [Fact]
        public void Create_WithOversizedUtf8Value_ShouldThrowArgumentException()
        {
            Action act = () => GroupName.Create("12345678901234567");

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Create_WithValidValue_ShouldPreserveValue()
        {
            var groupName = GroupName.Create("group1");

            groupName.Value.Should().Be("group1");
        }
    }
}
