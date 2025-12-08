namespace FastDFS.Client.Protocol
{
    /// <summary>
    /// FastDFS error codes (status codes in response header).
    /// </summary>
    public enum FastDFSErrorCode : byte
    {
        /// <summary>
        /// Success (0).
        /// </summary>
        Success = 0,

        /// <summary>
        /// File not found (2 - ENOENT).
        /// </summary>
        FileNotFound = 2,

        /// <summary>
        /// I/O error (5 - EIO).
        /// </summary>
        IOError = 5,

        /// <summary>
        /// Out of memory (12 - ENOMEM).
        /// </summary>
        OutOfMemory = 12,

        /// <summary>
        /// Permission denied (13 - EACCES).
        /// </summary>
        PermissionDenied = 13,

        /// <summary>
        /// File exists (17 - EEXIST).
        /// </summary>
        FileExists = 17,

        /// <summary>
        /// Invalid argument (22 - EINVAL).
        /// </summary>
        InvalidArgument = 22,

        /// <summary>
        /// No space left on device (28 - ENOSPC).
        /// </summary>
        NoSpaceLeft = 28,

        /// <summary>
        /// Network is unreachable (101 - ENETUNREACH).
        /// </summary>
        NetworkUnreachable = 101,

        /// <summary>
        /// Connection refused (111 - ECONNREFUSED).
        /// </summary>
        ConnectionRefused = 111,

        /// <summary>
        /// Connection timed out (110 - ETIMEDOUT).
        /// </summary>
        ConnectionTimedOut = 110,

        /// <summary>
        /// Unknown error (255).
        /// </summary>
        Unknown = 255
    }

    /// <summary>
    /// Helper methods for FastDFS error codes.
    /// </summary>
    public static class FastDFSErrorCodeExtensions
    {
        /// <summary>
        /// Gets a human-readable error message for the error code.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        /// <returns>The error message.</returns>
        public static string GetErrorMessage(this FastDFSErrorCode errorCode)
        {
            return errorCode switch
            {
                FastDFSErrorCode.Success => "Success",
                FastDFSErrorCode.FileNotFound => "File not found",
                FastDFSErrorCode.IOError => "I/O error",
                FastDFSErrorCode.OutOfMemory => "Out of memory",
                FastDFSErrorCode.PermissionDenied => "Permission denied",
                FastDFSErrorCode.FileExists => "File already exists",
                FastDFSErrorCode.InvalidArgument => "Invalid argument",
                FastDFSErrorCode.NoSpaceLeft => "No space left on device",
                FastDFSErrorCode.NetworkUnreachable => "Network is unreachable",
                FastDFSErrorCode.ConnectionRefused => "Connection refused",
                FastDFSErrorCode.ConnectionTimedOut => "Connection timed out",
                FastDFSErrorCode.Unknown => "Unknown error",
                _ => $"Unknown error code: {(byte)errorCode}"
            };
        }

        /// <summary>
        /// Converts a byte status code to FastDFSErrorCode.
        /// </summary>
        /// <param name="status">The status byte.</param>
        /// <returns>The corresponding error code.</returns>
        public static FastDFSErrorCode FromByte(byte status)
        {
            if (System.Enum.IsDefined(typeof(FastDFSErrorCode), status))
            {
                return (FastDFSErrorCode)status;
            }
            return FastDFSErrorCode.Unknown;
        }
    }
}
