using System.Collections.Generic;
using FastDFS.Client.Configuration;
using FastDFS.Client.Connection;
using FastDFS.Client.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FastDFS.Client.Tests.DependencyInjection
{
    public class FastDFSClientFactoryTests
    {
        [Fact]
        public void HasClient_WithConfiguredButNotYetCreatedClient_ShouldReturnTrue()
        {
            var factory = CreateFactory(new Dictionary<string, FastDFSConfiguration>
            {
                ["cluster-a"] = new FastDFSConfiguration
                {
                    TrackerServers = new List<string> { "127.0.0.1:22122" }
                }
            });

            factory.HasClient("cluster-a").Should().BeTrue();
        }

        [Fact]
        public void GetClientNames_ShouldContainRuntimeRegisteredClient()
        {
            var factory = CreateFactory(new Dictionary<string, FastDFSConfiguration>());

            factory.RegisterClient("runtime", new FastDFSConfiguration
            {
                TrackerServers = new List<string> { "127.0.0.1:22122" },
                ConnectionPool = new ConnectionPoolConfiguration()
            });

            factory.GetClientNames().Should().Contain("runtime");
        }

        [Fact]
        public void GetConfigKey_ShouldIncludeStreamCopyBufferSize()
        {
            var configA = new FastDFSConfiguration
            {
                TrackerServers = new List<string> { "127.0.0.1:22122" },
                ConnectionPool = new ConnectionPoolConfiguration
                {
                    StreamCopyBufferSize = 81920
                }
            };

            var configB = new FastDFSConfiguration
            {
                TrackerServers = new List<string> { "127.0.0.1:22122" },
                ConnectionPool = new ConnectionPoolConfiguration
                {
                    StreamCopyBufferSize = 163840
                }
            };

            configA.GetConfigKey().Should().NotBe(configB.GetConfigKey());
        }

        [Fact]
        public void Clone_ShouldCopyStreamCopyBufferSize()
        {
            var config = new FastDFSConfiguration
            {
                TrackerServers = new List<string> { "127.0.0.1:22122" },
                ConnectionPool = new ConnectionPoolConfiguration
                {
                    StreamCopyBufferSize = 131072
                }
            };

            var clone = config.Clone();

            clone.ConnectionPool.StreamCopyBufferSize.Should().Be(131072);
        }

        private static FastDFSClientFactory CreateFactory(Dictionary<string, FastDFSConfiguration> configurations)
        {
            var optionsMonitor = new TestOptionsMonitor(configurations);
            var providerFactory = new DefaultConnectionPoolProviderFactory(NullLoggerFactory.Instance);
            return new FastDFSClientFactory(optionsMonitor, providerFactory, NullLoggerFactory.Instance);
        }

        private sealed class TestOptionsMonitor : IOptionsMonitor<FastDFSConfiguration>
        {
            private readonly Dictionary<string, FastDFSConfiguration> _configurations;

            public TestOptionsMonitor(Dictionary<string, FastDFSConfiguration> configurations)
            {
                _configurations = configurations;
            }

            public FastDFSConfiguration CurrentValue => Get(Options.DefaultName);

            public FastDFSConfiguration Get(string? name)
            {
                var key = string.IsNullOrEmpty(name) ? Options.DefaultName : name;
                if (_configurations.TryGetValue(key, out var configuration))
                    return configuration;

                throw new OptionsValidationException(key, typeof(FastDFSConfiguration), new[] { "Configuration not found." });
            }

            public IDisposable? OnChange(System.Action<FastDFSConfiguration, string> listener) => null;
        }
    }
}
