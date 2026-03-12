using System;

namespace FastDFS.Client.Connection
{
    /// <summary>
    /// Represents a FastDFS server endpoint.
    /// </summary>
    public readonly struct ConnectionEndpoint : IEquatable<ConnectionEndpoint>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionEndpoint"/> struct.
        /// </summary>
        /// <param name="host">The server host.</param>
        /// <param name="port">The server port.</param>
        public ConnectionEndpoint(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host cannot be null or empty.", nameof(host));
            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");

            Host = NormalizeHost(host);
            Port = port;
        }

        /// <summary>
        /// Gets the server host.
        /// </summary>
        public string Host { get; }

        /// <summary>
        /// Gets the server port.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Gets the normalized endpoint key.
        /// </summary>
        public string Key => Host.IndexOf(':') >= 0
            ? $"[{Host}]:{Port}"
            : $"{Host}:{Port}";

        /// <summary>
        /// Parses an endpoint from a string in the format <c>host:port</c>.
        /// </summary>
        /// <param name="value">The endpoint string.</param>
        /// <returns>The parsed endpoint.</returns>
        public static ConnectionEndpoint Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Endpoint cannot be null or empty.", nameof(value));

            var trimmed = value.Trim();
            string host;
            string portText;

            if (trimmed[0] == '[')
            {
                int closingBracketIndex = trimmed.IndexOf(']');
                if (closingBracketIndex <= 1 || closingBracketIndex == trimmed.Length - 1 || trimmed[closingBracketIndex + 1] != ':')
                    throw new ArgumentException($"Invalid endpoint address format: {value}. Expected format: 'host:port' or '[ipv6]:port'", nameof(value));

                host = trimmed.Substring(1, closingBracketIndex - 1);
                portText = trimmed.Substring(closingBracketIndex + 2);
            }
            else
            {
                int separatorIndex = trimmed.LastIndexOf(':');
                if (separatorIndex <= 0 || separatorIndex == trimmed.Length - 1)
                    throw new ArgumentException($"Invalid endpoint address format: {value}. Expected format: 'host:port' or '[ipv6]:port'", nameof(value));

                host = trimmed.Substring(0, separatorIndex);
                portText = trimmed.Substring(separatorIndex + 1);
            }

            if (!int.TryParse(portText, out int port))
                throw new ArgumentException($"Invalid port number in endpoint address: {value}", nameof(value));

            return new ConnectionEndpoint(host, port);
        }

        /// <inheritdoc/>
        public bool Equals(ConnectionEndpoint other) =>
            Port == other.Port &&
            string.Equals(Host, other.Host, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is ConnectionEndpoint other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.OrdinalIgnoreCase.GetHashCode(Host) * 397) ^ Port;
            }
        }

        /// <inheritdoc/>
        public override string ToString() => Key;

        private static string NormalizeHost(string host)
        {
            var normalized = host.Trim();
            if (normalized.Length >= 2 && normalized[0] == '[' && normalized[normalized.Length - 1] == ']')
            {
                normalized = normalized.Substring(1, normalized.Length - 2);
            }

            if (string.IsNullOrWhiteSpace(normalized))
                throw new ArgumentException("Host cannot be null or empty.", nameof(host));

            return normalized;
        }

        /// <summary>
        /// Determines whether two endpoints are equal.
        /// </summary>
        public static bool operator ==(ConnectionEndpoint left, ConnectionEndpoint right) => left.Equals(right);

        /// <summary>
        /// Determines whether two endpoints are not equal.
        /// </summary>
        public static bool operator !=(ConnectionEndpoint left, ConnectionEndpoint right) => !left.Equals(right);
    }
}
