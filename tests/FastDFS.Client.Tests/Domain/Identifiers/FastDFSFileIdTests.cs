using System;
using FastDFS.Client.Domain.Identifiers;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Domain.Identifiers
{
    public class FastDFSFileIdTests
    {
        [Fact]
        public void Parse_WithFullFileId_ShouldSplitGroupAndFileName()
        {
            var fileId = FastDFSFileId.Parse("group1/M00/00/00/file.txt", null);

            fileId.GroupName.Value.Should().Be("group1");
            fileId.FileName.Should().Be("M00/00/00/file.txt");
        }

        [Fact]
        public void Parse_WithStoragePathAndDefaultGroup_ShouldUseDefaultGroup()
        {
            var fileId = FastDFSFileId.Parse("M00/00/00/file.txt", "group1");

            fileId.GroupName.Value.Should().Be("group1");
            fileId.FileName.Should().Be("M00/00/00/file.txt");
        }

        [Fact]
        public void Parse_WithStoragePathAndNoDefaultGroup_ShouldThrowArgumentException()
        {
            Action act = () => FastDFSFileId.Parse("M00/00/00/file.txt", null);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Parse_WithAmbiguousGroupPrefix_ShouldPreserveGroup()
        {
            var fileId = FastDFSFileId.Parse("data-prod/M00/00/00/file.txt", null);

            fileId.GroupName.Value.Should().Be("data-prod");
            fileId.FileName.Should().Be("M00/00/00/file.txt");
        }

        [Fact]
        public void Parse_WithM1PrefixedGroup_ShouldPreserveGroup()
        {
            var fileId = FastDFSFileId.Parse("M1group/M00/00/00/file.txt", null);

            fileId.GroupName.Value.Should().Be("M1group");
            fileId.FileName.Should().Be("M00/00/00/file.txt");
        }
    }
}
