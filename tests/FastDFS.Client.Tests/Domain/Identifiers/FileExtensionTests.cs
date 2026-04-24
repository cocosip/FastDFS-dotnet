using System;
using FastDFS.Client.Domain.Identifiers;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Domain.Identifiers
{
    public class FileExtensionTests
    {
        [Fact]
        public void Create_WithNull_ShouldThrowArgumentNullException()
        {
            Action act = () => FileExtension.Create(null!);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Create_WithLeadingDot_ShouldNormalizeWithoutDot()
        {
            var extension = FileExtension.Create(".jpg");

            extension.Value.Should().Be("jpg");
        }

        [Fact]
        public void Create_WithEmptyString_ShouldRemainEmpty()
        {
            var extension = FileExtension.Create(string.Empty);

            extension.Value.Should().BeEmpty();
        }
    }
}
