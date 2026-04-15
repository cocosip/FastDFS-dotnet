using System;

namespace FastDFS.Client.Utilities
{
    /// <summary>
    /// Helper class for parsing and handling FastDFS file IDs.
    /// FastDFS supports two file ID formats:
    /// 1. Full format (with group): "group1/M00/00/00/xxx.jpg"
    /// 2. Simple format (without group): "M00/00/00/xxx.jpg"
    /// </summary>
    public static class FileIdHelper
    {
        /// <summary>
        /// Parses a file ID into group name and file name components.
        /// Supports both formats:
        /// - Full format: "group1/M00/00/00/xxx.jpg"
        /// - Simple format: "M00/00/00/xxx.jpg" (requires explicit group name parameter)
        /// </summary>
        /// <param name="fileId">The file ID (e.g., "group1/M00/00/00/wKgBaFxxx.jpg" or "M00/00/00/xxx.jpg").</param>
        /// <param name="groupName">Output: The group name.</param>
        /// <param name="fileName">Output: The file name (path on storage server, without group).</param>
        /// <param name="defaultGroupName">Optional: The default group name to use if file ID doesn't contain it.</param>
        /// <exception cref="ArgumentException">Thrown when file ID format is invalid.</exception>
        public static void ParseFileId(string fileId, out string groupName, out string fileName, string? defaultGroupName = null)
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
                // Check if file ID contains a group name (format: group_name/path/filename)
                int firstSlashIndex = fileId.IndexOf('/');

                if (firstSlashIndex > 0 && firstSlashIndex < fileId.Length - 1)
                {
                    groupName = fileId.Substring(0, firstSlashIndex);
                    fileName = fileId.Substring(firstSlashIndex + 1);
                }
                else
                {
                    // No '/' found; assume simple format without path separators (rare case)
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

        private static bool LooksLikeStoragePath(string str)
        {
            if (string.IsNullOrEmpty(str))
                return false;

            int firstSlashIndex = str.IndexOf('/');
            string firstSegment = firstSlashIndex >= 0 ? str.Substring(0, firstSlashIndex) : str;

            // Store paths typically start with M followed by digits (M00, M01, etc.)
            if (firstSegment.Length >= 2
                && firstSegment[0] == 'M'
                && char.IsDigit(firstSegment[1])
                && HasOnlyDigits(firstSegment, 1))
                return true;

            // Store paths might also be "data" or "data0", "data1"
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

        private static bool HasExplicitGroupNamePrefix(string str)
        {
            if (string.IsNullOrEmpty(str))
                return false;

            int firstSlashIndex = str.IndexOf('/');
            return firstSlashIndex > 0 && firstSlashIndex < str.Length - 1 && !LooksLikeStoragePath(str);
        }

        /// <summary>
        /// Combines group name and file name into a complete file ID.
        /// Handles cases where fileName might already contain the group name.
        /// </summary>
        /// <param name="groupName">The group name.</param>
        /// <param name="fileName">The file name (path on storage server).</param>
        /// <returns>The complete file ID in the format "group_name/path/filename".</returns>
        public static string CombineFileId(string groupName, string fileName)
        {
            if (string.IsNullOrEmpty(groupName))
                throw new ArgumentException("Group name cannot be null or empty.", nameof(groupName));
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            // Check if fileName already contains the group name prefix
            if (fileName.StartsWith($"{groupName}/", StringComparison.Ordinal))
            {
                // FileName already has group name, return as is
                return fileName;
            }

            // Check if fileName already has a group name (different from the provided one)
            int firstSlashIndex = fileName.IndexOf('/');
            if (HasExplicitGroupNamePrefix(fileName))
            {
                // FileName already has a group name (possibly different), return as is
                return fileName;
            }

            // Combine group name and file name
            return $"{groupName}/{fileName}";
        }

        /// <summary>
        /// Checks if a file ID contains a group name prefix.
        /// </summary>
        /// <param name="fileId">The file ID to check.</param>
        /// <returns>True if the file ID contains a group name prefix; otherwise, false.</returns>
        public static bool HasGroupName(string fileId)
        {
            if (string.IsNullOrEmpty(fileId))
                return false;

            int firstSlashIndex = fileId.IndexOf('/');
            if (firstSlashIndex <= 0)
                return false;

            return HasExplicitGroupNamePrefix(fileId);
        }

        /// <summary>
        /// Normalizes a file ID to always include the group name.
        /// </summary>
        /// <param name="fileId">The file ID (with or without group name).</param>
        /// <param name="defaultGroupName">The group name to use if not present in fileId.</param>
        /// <returns>The normalized file ID with group name.</returns>
        public static string NormalizeFileId(string fileId, string defaultGroupName)
        {
            if (HasGroupName(fileId))
                return fileId;

            return CombineFileId(defaultGroupName, fileId);
        }
    }
}
