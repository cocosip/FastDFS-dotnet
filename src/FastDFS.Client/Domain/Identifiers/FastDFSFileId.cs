using System;

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
            Split(fileId, defaultGroupName, out string groupName, out string fileName);
            return new FastDFSFileId(GroupName.Create(groupName), fileName);
        }

        internal static void Split(string fileId, string? defaultGroupName, out string groupName, out string fileName)
        {
            if (string.IsNullOrEmpty(fileId))
                throw new ArgumentException("File ID cannot be null or empty.", nameof(fileId));

            if (LooksLikeStoragePath(fileId))
            {
                if (string.IsNullOrEmpty(defaultGroupName))
                    throw new ArgumentException($"File ID appears to be in simple format (without group name): {fileId}. Please provide a default group name.", nameof(fileId));

                groupName = defaultGroupName!;
                fileName = fileId;
            }
            else
            {
                int firstSlashIndex = fileId.IndexOf('/');

                if (firstSlashIndex > 0 && firstSlashIndex < fileId.Length - 1)
                {
                    groupName = fileId.Substring(0, firstSlashIndex);
                    fileName = fileId.Substring(firstSlashIndex + 1);
                }
                else
                {
                    if (string.IsNullOrEmpty(defaultGroupName))
                        throw new ArgumentException($"File ID does not contain group name: {fileId}. Please provide a default group name.", nameof(fileId));

                    groupName = defaultGroupName!;
                    fileName = fileId;
                }
            }

            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be empty.", nameof(fileId));
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be empty.", nameof(fileId));
        }

        internal static bool HasExplicitGroupNamePrefix(string fileId)
        {
            if (string.IsNullOrEmpty(fileId))
                return false;

            int firstSlashIndex = fileId.IndexOf('/');
            return firstSlashIndex > 0 && firstSlashIndex < fileId.Length - 1 && !LooksLikeStoragePath(fileId);
        }

        private static bool LooksLikeStoragePath(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            int firstSlashIndex = value.IndexOf('/');
            string firstSegment = firstSlashIndex >= 0 ? value.Substring(0, firstSlashIndex) : value;

            if (firstSegment.Length >= 2
                && firstSegment[0] == 'M'
                && char.IsDigit(firstSegment[1])
                && HasOnlyDigits(firstSegment, 1))
            {
                return true;
            }

            if (firstSegment.Length == 4 && firstSegment.Equals("data", StringComparison.OrdinalIgnoreCase))
                return true;

            return firstSegment.Length > 4
                && firstSegment.StartsWith("data", StringComparison.OrdinalIgnoreCase)
                && HasOnlyDigits(firstSegment, 4);
        }

        private static bool HasOnlyDigits(string value, int startIndex)
        {
            for (int i = startIndex; i < value.Length; i++)
            {
                if (!char.IsDigit(value[i]))
                    return false;
            }

            return value.Length > startIndex;
        }
    }
}
