using System;
using System.Globalization;
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

        public static void EnsureExactBlockMultiple(string responseName, int actualLength, int blockSize)
        {
            if (actualLength % blockSize != 0)
            {
                throw new FastDFSProtocolException(
                    $"{responseName} body length {actualLength} is not a multiple of the expected block size {blockSize} bytes.");
            }
        }

        public static int ResolveSupportedBlockSize(string responseName, int actualLength, int[] supportedBlockSizes)
        {
            if (supportedBlockSizes == null)
                throw new ArgumentNullException(nameof(supportedBlockSizes));

            foreach (int blockSize in supportedBlockSizes)
            {
                if (actualLength % blockSize == 0)
                    return blockSize;
            }

            throw new FastDFSProtocolException(
                $"{responseName} body length {actualLength.ToString(CultureInfo.InvariantCulture)} does not match any supported block size: {string.Join(", ", supportedBlockSizes)}.");
        }
    }
}
