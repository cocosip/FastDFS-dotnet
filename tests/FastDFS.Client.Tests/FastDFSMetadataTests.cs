using System;
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
    }
}
