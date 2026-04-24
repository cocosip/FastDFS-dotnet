using FastDFS.Client.Exceptions;

namespace FastDFS.Client.Protocol.Decoding
{
    internal static class ProtocolBlockGuard
    {
        public static void EnsureMinimumBodyLength(string responseName, int actualLength, int minimumLength)
        {
            if (actualLength < minimumLength)
            {
                throw new FastDFSProtocolException(
                    $"{responseName} body length {actualLength} is shorter than the minimum expected {minimumLength} bytes.");
            }
        }
    }
}
