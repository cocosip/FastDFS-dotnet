using System;
using System.Security.Cryptography;
using System.Text;

namespace FastDFS.Client.Utilities
{
    /// <summary>
    /// FastDFS anti-steal token generator
    /// </summary>
    public static class TokenGenerator
    {
        /// <summary>
        /// Generate anti-steal token for FastDFS Nginx module
        /// Algorithm: token = md5(file_id + secret_key + timestamp_hex)
        /// </summary>
        /// <param name="fileId">File ID (e.g., "group1/M00/00/00/xxxxx.jpg")</param>
        /// <param name="secretKey">Secret key configured in Nginx module</param>
        /// <param name="timestamp">Unix timestamp</param>
        /// <returns>MD5 token string (32 characters, lowercase hex)</returns>
        public static string GenerateToken(string fileId, string secretKey, long timestamp)
        {
            if (string.IsNullOrWhiteSpace(fileId))
            {
                throw new ArgumentException("File ID cannot be null or empty", nameof(fileId));
            }

            if (string.IsNullOrWhiteSpace(secretKey))
            {
                throw new ArgumentException("Secret key cannot be null or empty", nameof(secretKey));
            }

            // FastDFS token format: md5(file_id + secret_key + timestamp_hex)
            // Note: file_id should NOT include leading slash
            var normalizedFileId = fileId.TrimStart('/');

            // Convert timestamp to hex (lowercase)
            var timestampHex = timestamp.ToString("x");

            // Combine: file_id + secret_key + timestamp_hex
            var input = normalizedFileId + secretKey + timestampHex;

            // Calculate MD5 hash
            using (var md5 = MD5.Create())
            {
                var inputBytes = Encoding.UTF8.GetBytes(input);
                var hashBytes = md5.ComputeHash(inputBytes);

                // Convert to lowercase hex string
                var sb = new StringBuilder(32);
                foreach (var b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// Generate token with current time + expire seconds
        /// </summary>
        /// <param name="fileId">File ID</param>
        /// <param name="secretKey">Secret key</param>
        /// <param name="expireSeconds">Token expiration time in seconds from now</param>
        /// <returns>Tuple of (token, timestamp)</returns>
        public static (string token, long timestamp) GenerateTokenWithExpire(string fileId, string secretKey, int expireSeconds)
        {
            if (expireSeconds <= 0)
            {
                throw new ArgumentException("Expire seconds must be greater than 0", nameof(expireSeconds));
            }

            // Calculate expiration timestamp
            var expireTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expireSeconds;

            // Generate token
            var token = GenerateToken(fileId, secretKey, expireTimestamp);

            return (token, expireTimestamp);
        }
    }
}
