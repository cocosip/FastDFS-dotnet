using FastDFS.Client.Utilities;

namespace FastDFS.Client.Domain.Identifiers
{
    internal readonly struct FastDFSFileId
    {
        private FastDFSFileId(GroupName groupName, string fileName)
        {
            GroupName = groupName;
            FileName = fileName;
        }

        public GroupName GroupName { get; }
        public string FileName { get; }

        public static FastDFSFileId Parse(string fileId, string? defaultGroupName)
        {
            FileIdHelper.ParseFileId(fileId, out string groupName, out string fileName, defaultGroupName);
            return new FastDFSFileId(GroupName.Create(groupName), fileName);
        }
    }
}
