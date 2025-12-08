using System;

namespace FastDFS.Client.Exceptions
{
    /// <summary>
    /// Base exception class for all FastDFS-related exceptions.
    /// </summary>
    public class FastDFSException : Exception
    {
        /// <summary>
        /// Gets the FastDFS error code if available.
        /// </summary>
        public byte? ErrorCode { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSException"/> class.
        /// </summary>
        public FastDFSException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public FastDFSException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSException"/> class with a specified error message and error code.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="errorCode">The FastDFS error code.</param>
        public FastDFSException(string message, byte errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSException"/> class with a specified error message and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public FastDFSException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSException"/> class with a specified error message, error code, and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="errorCode">The FastDFS error code.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public FastDFSException(string message, byte errorCode, Exception innerException) : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }
}
