namespace FastDFS.Client.Configuration
{
    /// <summary>
    /// Configuration for connection pool.
    /// </summary>
    public class ConnectionPoolConfiguration
    {
        /// <summary>
        /// Gets or sets the maximum number of connections per server.
        /// Default is 50.
        /// </summary>
        public int MaxConnectionPerServer { get; set; } = 50;

        /// <summary>
        /// Gets or sets the minimum number of connections per server.
        /// Default is 5.
        /// </summary>
        public int MinConnectionPerServer { get; set; } = 5;

        /// <summary>
        /// Gets or sets the connection idle timeout in seconds.
        /// Connections idle for longer than this will be closed.
        /// Default is 300 seconds (5 minutes). Set to 0 to disable.
        /// </summary>
        public int ConnectionIdleTimeout { get; set; } = 300;

        /// <summary>
        /// Gets or sets the maximum lifetime of a connection in seconds.
        /// Connections older than this will be closed even if active.
        /// Default is 3600 seconds (1 hour). Set to 0 to disable.
        /// </summary>
        public int ConnectionLifetime { get; set; } = 3600;

        /// <summary>
        /// Gets or sets the connection timeout in milliseconds.
        /// Default is 30000 milliseconds (30 seconds).
        /// </summary>
        public int ConnectionTimeout { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the send timeout in milliseconds.
        /// Default is 30000 milliseconds (30 seconds).
        /// </summary>
        public int SendTimeout { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the receive timeout in milliseconds.
        /// Default is 30000 milliseconds (30 seconds).
        /// </summary>
        public int ReceiveTimeout { get; set; } = 30000;

        /// <summary>
        /// Validates the configuration options.
        /// </summary>
        /// <exception cref="System.ArgumentException">Thrown when configuration is invalid.</exception>
        public void Validate()
        {
            if (MaxConnectionPerServer <= 0)
                throw new System.ArgumentException("MaxConnectionPerServer must be greater than 0.", nameof(MaxConnectionPerServer));

            if (MinConnectionPerServer < 0)
                throw new System.ArgumentException("MinConnectionPerServer must be greater than or equal to 0.", nameof(MinConnectionPerServer));

            if (MinConnectionPerServer > MaxConnectionPerServer)
                throw new System.ArgumentException("MinConnectionPerServer cannot be greater than MaxConnectionPerServer.");

            if (ConnectionIdleTimeout < 0)
                throw new System.ArgumentException("ConnectionIdleTimeout must be greater than or equal to 0.", nameof(ConnectionIdleTimeout));

            if (ConnectionLifetime < 0)
                throw new System.ArgumentException("ConnectionLifetime must be greater than or equal to 0.", nameof(ConnectionLifetime));

            if (ConnectionTimeout <= 0)
                throw new System.ArgumentException("ConnectionTimeout must be greater than 0.", nameof(ConnectionTimeout));

            if (SendTimeout <= 0)
                throw new System.ArgumentException("SendTimeout must be greater than 0.", nameof(SendTimeout));

            if (ReceiveTimeout <= 0)
                throw new System.ArgumentException("ReceiveTimeout must be greater than 0.", nameof(ReceiveTimeout));
        }
    }
}
