using System;
using System.Reflection;
using FastDFS.Client.Protocol;
using FluentAssertions;

namespace FastDFS.Client.Tests.Protocol
{
    public class ProtocolFieldLengthGuardTests
    {
        [Fact]
        public void EnsureUtf8FitsFixedField_WithValueWithinLimit_ShouldNotThrow()
        {
            Action act = () => InvokeEnsureUtf8FitsFixedField("groupName", "group1", 16);

            act.Should().NotThrow();
        }

        [Fact]
        public void EnsureUtf8FitsFixedField_WithAsciiValueExceedingLimit_ShouldThrowArgumentException()
        {
            Action act = () => InvokeEnsureUtf8FitsFixedField("groupName", "12345678901234567", 16);

            var exception = act.Should().Throw<ArgumentException>().Which;
            exception.ParamName.Should().Be("value");
            exception.Message.Should().Contain("groupName");
            exception.Message.Should().Contain("16");
        }

        [Fact]
        public void EnsureUtf8FitsFixedField_WithNullValue_ShouldThrowArgumentNullException()
        {
            Action act = () => InvokeEnsureUtf8FitsFixedField("groupName", null!, 16);

            var exception = act.Should().Throw<ArgumentNullException>().Which;
            exception.ParamName.Should().Be("value");
        }

        private static void InvokeEnsureUtf8FitsFixedField(string fieldName, string value, int maxBytes)
        {
            var type = typeof(FastDFSHeader).Assembly.GetType(
                "FastDFS.Client.Protocol.ProtocolFieldLengthGuard",
                throwOnError: true)!;

            var method = type.GetMethod(
                "EnsureUtf8FitsFixedField",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

            try
            {
                method.Invoke(null, new object[] { fieldName, value, maxBytes });
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        }
    }
}
