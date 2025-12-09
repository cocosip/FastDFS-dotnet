using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using FastDFS.Client.Protocol;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Benchmarks
{
    /// <summary>
    /// Performance benchmarks for FastDFS protocol serialization/deserialization.
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net80)]
    public class ProtocolBenchmarks
    {
        private byte[] _headerBytes = null!;
        private FastDFSHeader _header = null!;

        [GlobalSetup]
        public void Setup()
        {
            _header = new FastDFSHeader(1024, 101, 0);
            _headerBytes = _header.ToBytes();
        }

        [Benchmark]
        public FastDFSHeader HeaderParse()
        {
            return FastDFSHeader.Parse(_headerBytes, 0);
        }

        [Benchmark]
        public byte[] HeaderToBytes()
        {
            return _header.ToBytes();
        }

        [Benchmark]
        public byte[] Int64ToBigEndian()
        {
            return ByteConverter.ToBytes(1234567890L);
        }

        [Benchmark]
        public long BigEndianToInt64()
        {
            byte[] bytes = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x49, 0x96, 0x02, 0xD2 };
            return ByteConverter.ToInt64(bytes, 0);
        }

        [Benchmark]
        public byte[] Int32ToBigEndian()
        {
            return ByteConverter.ToBytes(12345678);
        }

        [Benchmark]
        public int BigEndianToInt32()
        {
            byte[] bytes = new byte[] { 0x00, 0xBC, 0x61, 0x4E };
            return ByteConverter.ToInt32(bytes, 0);
        }
    }
}
