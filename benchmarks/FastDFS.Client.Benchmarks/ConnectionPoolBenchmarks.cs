using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using FastDFS.Client.Configuration;
using FastDFS.Client.Connection;

namespace FastDFS.Client.Benchmarks
{
    /// <summary>
    /// Performance benchmarks for connection pool operations.
    /// Note: These benchmarks test pool logic, not actual network operations.
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net80)]
    public class ConnectionPoolBenchmarks
    {
        private ConnectionPoolConfiguration _config = null!;
        private ConnectionPool _pool = null!;

        [GlobalSetup]
        public void Setup()
        {
            _config = new ConnectionPoolConfiguration
            {
                MaxConnectionPerServer = 100,
                MinConnectionPerServer = 10,
                ConnectionIdleTimeout = 300,
                ConnectionLifetime = 3600,
                ConnectionTimeout = 30000,
                SendTimeout = 30000,
                ReceiveTimeout = 30000
            };

            _pool = new ConnectionPool("localhost", 22122, _config);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _pool?.Dispose();
        }

        [Benchmark]
        public void ConnectionPoolCreation()
        {
            using var pool = new ConnectionPool("localhost", 22122, _config);
        }

        [Benchmark]
        public ConnectionPoolConfiguration ConfigurationCreation()
        {
            return new ConnectionPoolConfiguration
            {
                MaxConnectionPerServer = 100,
                MinConnectionPerServer = 10,
                ConnectionIdleTimeout = 300,
                ConnectionLifetime = 3600
            };
        }

        [Benchmark]
        public void ConfigurationValidation()
        {
            _config.Validate();
        }

        // Note: GetConnectionAsync and ReturnConnection benchmarks require
        // actual network connections, so they are commented out.
        // These would be part of integration benchmarks.

        /*
        [Benchmark]
        public async Task GetAndReturnConnection()
        {
            var conn = await _pool.GetConnectionAsync();
            _pool.ReturnConnection(conn);
        }
        */
    }
}
