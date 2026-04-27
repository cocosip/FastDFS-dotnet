using System;
using System.Collections.Generic;
using FastDFS.Client.Exceptions;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests
{
    /// <summary>
    /// Unit tests for FastDFSMetadata.
    /// </summary>
    public class FastDFSMetadataTests
    {
        [Fact]
        public void Constructor_WithRecordSeparatorInKey_ShouldThrowArgumentException()
        {
            Action act = () => new FastDFSMetadata(new Dictionary<string, string>
            {
                ["bad\x01key"] = "value"
            });

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Constructor_WithFieldSeparatorInValue_ShouldThrowArgumentException()
        {
            Action act = () => new FastDFSMetadata(new Dictionary<string, string>
            {
                ["key"] = "bad\x02value"
            });

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Add_WithRecordSeparatorInKey_ShouldThrow()
        {
            var metadata = new FastDFSMetadata();

            Action act = () => metadata.Add("bad\x01key", "value");

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Add_WithFieldSeparatorInValue_ShouldThrow()
        {
            var metadata = new FastDFSMetadata();

            Action act = () => metadata.Add("key", "bad\x02value");

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void IndexerSetter_WithRecordSeparatorInValue_ShouldThrowArgumentException()
        {
            var metadata = new FastDFSMetadata();

            Action act = () => metadata["key"] = "bad\x01value";

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Add_WithNullValue_ShouldStoreEmptyString()
        {
            var metadata = new FastDFSMetadata();

            metadata.Add("key", null!);

            metadata["key"].Should().Be(string.Empty);
        }

        [Fact]
        public void IndexerSetter_WithNullValue_ShouldStoreEmptyString()
        {
            var metadata = new FastDFSMetadata();

            metadata["key"] = null!;

            metadata["key"].Should().Be(string.Empty);
        }

        [Fact]
        public void Decode_WithBlankKeyRecord_ShouldThrowArgumentException()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("\x02value");

            Action act = () => FastDFSMetadata.Decode(data);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Decode_WithRecordWithoutFieldSeparator_ShouldIgnoreMalformedRecord()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("bad-record-without-separator");

            var metadata = FastDFSMetadata.Decode(data);

            metadata.Count.Should().Be(0);
        }

        [Fact]
        public void Decode_WithValidMetadata_ShouldRoundTrip()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("key\x02value\x01other\x02two");

            var metadata = FastDFSMetadata.Decode(data);

            metadata["key"].Should().Be("value");
            metadata["other"].Should().Be("two");
        }
    }
}
