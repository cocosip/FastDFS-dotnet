using System;
using System.Collections.Generic;

namespace FastDFS.Client.Configuration
{
    /// <summary>
    /// HTTP access configuration for FastDFS Nginx module
    /// </summary>
    public class HttpConfiguration
    {
        /// <summary>
        /// HTTP server addresses for each storage group
        /// Key: group name, Value: HTTP server URL (e.g., "http://192.168.1.100")
        /// If not configured, will use storage server IP with port 80
        /// </summary>
        public Dictionary<string, string> ServerUrls { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Default HTTP server URL template
        /// Use {ip} as placeholder for storage server IP
        /// Example: "http://{ip}:8080" or "http://{ip}"
        /// Default: "http://{ip}"
        /// </summary>
        public string DefaultServerUrlTemplate { get; set; } = "http://{ip}";

        /// <summary>
        /// Secret key for anti-steal token generation
        /// Must match the configuration in FastDFS Nginx module (http.anti_steal.secret_key)
        /// </summary>
        public string? SecretKey { get; set; }

        /// <summary>
        /// Enable anti-steal token validation
        /// Default: false
        /// </summary>
        public bool AntiStealTokenEnabled { get; set; } = false;

        /// <summary>
        /// Default token expiration time in seconds
        /// Default: 3600 (1 hour)
        /// </summary>
        public int DefaultTokenExpireSeconds { get; set; } = 3600;

        /// <summary>
        /// Validate the HTTP configuration
        /// </summary>
        public void Validate()
        {
            if (AntiStealTokenEnabled && string.IsNullOrWhiteSpace(SecretKey))
            {
                throw new ArgumentException("SecretKey is required when AntiStealTokenEnabled is true");
            }

            if (DefaultTokenExpireSeconds <= 0)
            {
                throw new ArgumentException("DefaultTokenExpireSeconds must be greater than 0");
            }
        }

        /// <summary>
        /// Get HTTP server URL for a specific group
        /// </summary>
        /// <param name="groupName">Group name</param>
        /// <param name="storageIp">Storage server IP address</param>
        /// <returns>HTTP server URL</returns>
        public string GetServerUrl(string groupName, string storageIp)
        {
            // First, try to get from configured server URLs
            if (ServerUrls.TryGetValue(groupName, out var url))
            {
                return url.TrimEnd('/');
            }

            // Use default template with storage IP
            return DefaultServerUrlTemplate.Replace("{ip}", storageIp).TrimEnd('/');
        }
    }
}
