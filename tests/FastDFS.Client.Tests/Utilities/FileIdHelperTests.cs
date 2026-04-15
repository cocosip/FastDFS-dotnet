using System;
using FastDFS.Client.Utilities;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Utilities
{
    public class FileIdHelperTests
    {
        [Fact]
        public void ParseFileId_WithStoragePathOnly_UsesDefaultGroup()
        {
            FileIdHelper.ParseFileId("M00/00/00/file.txt", out string groupName, out string fileName, "group1");

            groupName.Should().Be("group1");
            fileName.Should().Be("M00/00/00/file.txt");
        }

        [Fact]
        public void ParseFileId_WithGroupNameStartingWithMAndDigits_PreservesGroup()
        {
            FileIdHelper.ParseFileId("M1group/M00/00/00/file.txt", out string groupName, out string fileName);

            groupName.Should().Be("M1group");
            fileName.Should().Be("M00/00/00/file.txt");
        }

        [Fact]
        public void ParseFileId_WithGroupNameStartingWithData_PreservesGroup()
        {
            FileIdHelper.ParseFileId("data-prod/M00/00/00/file.txt", out string groupName, out string fileName);

            groupName.Should().Be("data-prod");
            fileName.Should().Be("M00/00/00/file.txt");
        }

        [Fact]
        public void HasGroupName_WithAmbiguousGroupPrefix_ReturnsTrue()
        {
            FileIdHelper.HasGroupName("data-prod/M00/00/00/file.txt").Should().BeTrue();
            FileIdHelper.HasGroupName("M1group/M00/00/00/file.txt").Should().BeTrue();
        }

        [Fact]
        public void NormalizeFileId_WithAmbiguousGroupPrefix_DoesNotPrependDefaultGroup()
        {
            string fileId = FileIdHelper.NormalizeFileId("data-prod/M00/00/00/file.txt", "group1");

            fileId.Should().Be("data-prod/M00/00/00/file.txt");
        }

        [Fact]
        public void ParseFileId_WithStoragePathOnlyAndNoDefaultGroup_Throws()
        {
            Action act = () => FileIdHelper.ParseFileId("data0/00/00/file.txt", out _, out _);

            act.Should().Throw<ArgumentException>();
        }
    }
}
