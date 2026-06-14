using System;
using System.Security.Cryptography;
using System.Text;

namespace Shift9.Customization.Caching
{
    /// <summary>Content-addressing helper: stable SHA-256 hex of a URL, used as the cache key/filename.</summary>
    public static class HashUtil
    {
        // SHA256 is not thread-safe across concurrent calls; one instance per call keeps the
        // import pipeline (which may fetch several images in parallel) correct. Cheap to create.
        public static string Sha256Hex(string value)
        {
            if (value == null) value = string.Empty;
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
