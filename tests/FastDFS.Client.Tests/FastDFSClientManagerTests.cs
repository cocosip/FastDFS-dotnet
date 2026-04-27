using System.Collections.Generic;
using System.Threading.Tasks;
using FastDFS.Client.Configuration;
using FastDFS.Client.Connection;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests
{
    public class FastDFSClientManagerTests
    {
        [Fact]
        public void GetClientNames_WithAddedButNotYetCreatedClient_ShouldContainName()
        {
            using var manager = new FastDFSClientManager();
            manager.AddClient("cluster-a", new FastDFSConfiguration
            {
                TrackerServers = new List<string> { "127.0.0.1:22122" },
                ConnectionPool = new ConnectionPoolConfiguration()
            });

            manager.GetClientNames().Should().Contain("cluster-a");
        }

        [Fact]
        public void RemoveClient_ShouldRemoveClientFromDiscoveryApis()
        {
            using var manager = new FastDFSClientManager();
            manager.RegisterClient("runtime", new FastDFSConfiguration
            {
                TrackerServers = new List<string> { "127.0.0.1:22122" },
                ConnectionPool = new ConnectionPoolConfiguration()
            });

            manager.RemoveClient("runtime").Should().BeTrue();
            manager.HasClient("runtime").Should().BeFalse();
            manager.GetClientNames().Should().NotContain("runtime");
        }

        [Fact]
        public void GetClient_AfterAddClient_ShouldNotChangeDiscoveryAnswers()
        {
            using var manager = new FastDFSClientManager();
            manager.AddClient("cluster-a", new FastDFSConfiguration
            {
                TrackerServers = new List<string> { "127.0.0.1:22122" },
                ConnectionPool = new ConnectionPoolConfiguration()
            });

            manager.GetClient("cluster-a");

            manager.HasClient("cluster-a").Should().BeTrue();
            manager.GetClientNames().Should().Contain("cluster-a");
        }

        [Fact]
        public void Compose_WhenClientIsDisposed_ShouldDisposeOwnedPoolProvider()
        {
            var provider = new TrackingConnectionPoolProvider();
            var factory = new TrackingConnectionPoolProviderFactory(provider);
            var configuration = new FastDFSConfiguration
            {
                TrackerServers = new List<string> { "127.0.0.1:22122" },
                ConnectionPool = new ConnectionPoolConfiguration()
            };

            var client = FastDFSClientComposer.Compose(configuration, "non-di", factory);

            ((System.IDisposable)client).Dispose();

            provider.DisposeCalled.Should().BeTrue();
        }

        private sealed class TrackingConnectionPoolProviderFactory : IConnectionPoolProviderFactory
        {
            private readonly IConnectionPoolProvider _provider;

            public TrackingConnectionPoolProviderFactory(IConnectionPoolProvider provider)
            {
                _provider = provider;
            }

            public IConnectionPoolProvider Create(ConnectionPoolConfiguration configuration)
            {
                return _provider;
            }
        }

        private sealed class TrackingConnectionPoolProvider : IConnectionPoolProvider
        {
            public bool DisposeCalled { get; private set; }

            public IConnectionPool GetOrCreate(ConnectionEndpoint endpoint)
            {
                throw new System.NotSupportedException();
            }

            public bool TryGet(ConnectionEndpoint endpoint, out IConnectionPool? pool)
            {
                pool = null;
                return false;
            }

            public IReadOnlyCollection<ConnectionEndpoint> GetEndpoints()
            {
                return System.Array.Empty<ConnectionEndpoint>();
            }

            public void Dispose()
            {
                DisposeCalled = true;
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return default;
            }
        }
    }
}
