using System;

namespace FastDFS.Client.Exceptions
{
    /// <summary>
    /// Exception thrown when a network error occurs during FastDFS operations.
    /// </summary>
    public class FastDFSNetworkException : FastDFSException
    {
        /// <summary>
        /// Gets the remote endpoint (IP:Port) where the error occurred.
        /// </summary>
        public string? RemoteEndpoint { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSNetworkException"/> class.
        /// </summary>
        public FastDFSNetworkException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSNetworkException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public FastDFSNetworkException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSNetworkException"/> class with a specified error message and remote endpoint.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="remoteEndpoint">The remote endpoint (IP:Port) where the error occurred.</param>
        public FastDFSNetworkException(string message, string remoteEndpoint) : base(message)
        {
            RemoteEndpoint = remoteEndpoint;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSNetworkException"/> class with a specified error message and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public FastDFSNetworkException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastDFSNetworkException"/> class with a specified error message, remote endpoint, and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="remoteEndpoint">The remote endpoint (IP:Port) where the error occurred.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public FastDFSNetworkException(string message, string remoteEndpoint, Exception innerException) : base(message, innerException)
        {
            RemoteEndpoint = remoteEndpoint;
        }
    }
}
