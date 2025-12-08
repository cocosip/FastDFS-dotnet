using System;

namespace FastDFS.Client.Exceptions
{
    /// <summary>
    /// Exception thrown when a FastDFS protocol error occurs.
    /// </summary>
    public class FastDFSProtocolException : FastDFSException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSProtocolException"/> class.
        /// </summary>
        public FastDFSProtocolException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSProtocolException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public FastDFSProtocolException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSProtocolException"/> class with a specified error message and error code.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="errorCode">The FastDFS error code.</param>
        public FastDFSProtocolException(string message, byte errorCode) : base(message, errorCode)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSProtocolException"/> class with a specified error message and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public FastDFSProtocolException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSProtocolException"/> class with a specified error message, error code, and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="errorCode">The FastDFS error code.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public FastDFSProtocolException(string message, byte errorCode, Exception innerException) : base(message, errorCode, innerException)
        {
        }
    }
}
