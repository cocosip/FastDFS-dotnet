using System;
using FastDFS.Client.Configuration;
using Microsoft.Extensions.Logging;

namespace FastDFS.Client.Connection
{
    /// <summary>
    /// Default implementation of <see cref="IConnectionPoolProviderFactory"/>.
    /// </summary>
    public class DefaultConnectionPoolProviderFactory : IConnectionPoolProviderFactory
    {
        private readonly ILoggerFactory? _loggerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultConnectionPoolProviderFactory"/> class.
        /// </summary>
        /// <param name="loggerFactory">Optional logger factory.</param>
        public DefaultConnectionPoolProviderFactory(ILoggerFactory? loggerFactory = null)
        {
            _loggerFactory = loggerFactory;
        }

        /// <inheritdoc/>
        public IConnectionPoolProvider Create(ConnectionPoolConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            return new ConnectionPoolProvider(configuration, _loggerFactory);
        }
    }
}
