using System.Collections.Generic;
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
    }
}
