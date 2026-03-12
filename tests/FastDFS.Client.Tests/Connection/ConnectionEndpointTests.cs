using FastDFS.Client.Connection;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Connection
{
    public class ConnectionEndpointTests
    {
        [Fact]
        public void Parse_WithIPv4Endpoint_ShouldSucceed()
        {
            var endpoint = ConnectionEndpoint.Parse("127.0.0.1:22122");

            endpoint.Host.Should().Be("127.0.0.1");
            endpoint.Port.Should().Be(22122);
            endpoint.Key.Should().Be("127.0.0.1:22122");
        }

        [Fact]
        public void Parse_WithBracketedIPv6Endpoint_ShouldSucceed()
        {
            var endpoint = ConnectionEndpoint.Parse("[2001:db8::1]:22122");

            endpoint.Host.Should().Be("2001:db8::1");
            endpoint.Port.Should().Be(22122);
            endpoint.Key.Should().Be("[2001:db8::1]:22122");
        }

        [Fact]
        public void Parse_WithUnbracketedIPv6Endpoint_ShouldSucceed()
        {
            var endpoint = ConnectionEndpoint.Parse("2001:db8::1:22122");

            endpoint.Host.Should().Be("2001:db8::1");
            endpoint.Port.Should().Be(22122);
        }

        [Fact]
        public void Parse_WithInvalidBracketedEndpoint_ShouldThrow()
        {
            var act = () => ConnectionEndpoint.Parse("[2001:db8::1]22122");

            act.Should().Throw<System.ArgumentException>();
        }
    }
}
