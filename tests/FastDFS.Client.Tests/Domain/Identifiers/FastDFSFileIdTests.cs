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
    }
}
