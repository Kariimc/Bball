using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shift9.Customization.Pipeline;
using Shift9.Customization.Validation;
using UnityEngine;

namespace Shift9.Customization.Caching
{
    /// <summary>
    /// Two-tier content-addressed cache for imported images.
    ///   Disk tier:   raw validated bytes at {cacheDir}/{sha256(url)} — survives restarts, enables
    ///                offline re-imports and dedup across teams that reuse a logo.
    ///   Memory tier: decoded <see cref="Sprite"/>s under an LRU byte budget.
    /// Not thread-safe: the pipeline drives it sequentially on the main thread because texture
    /// decoding (LoadImage) and Sprite creation are main-thread-only in Unity.
    /// </summary>
    public sealed class AssetCache
    {
        private readonly string _dir;
        private readonly IContentFetcher _fetcher;
        private readonly ContentValidator _validator;
        private readonly long _memoryBudgetBytes;
        private readonly long _maxImageBytes;

        private sealed class Entry { public Sprite Sprite; public long Bytes; }
        private readonly Dictionary<string, Entry> _memory = new();
        private readonly LinkedList<string> _lru = new(); // front = most recently used
        private long _memoryBytes;

        public AssetCache(string cacheDir, IContentFetcher fetcher, ContentValidator validator,
            long memoryBudgetBytes, long maxImageBytes)
        {
            _dir = cacheDir;
            _fetcher = fetcher;
            _validator = validator;
            _memoryBudgetBytes = memoryBudgetBytes;
            _maxImageBytes = maxImageBytes;
            Directory.CreateDirectory(_dir);
        }

        /// <summary>
        /// Returns validated raw image bytes for a URL, hitting disk first. On a miss it fetches
        /// (size-capped), validates the header (format + dimensions), and persists to disk.
        /// Returns null on any failure; <paramref name="error"/> carries the reason.
        /// </summary>
        public async Task<byte[]> GetRawAsync(string url, CancellationToken ct, ResultBox error)
        {
            string path = Path.Combine(_dir, HashUtil.Sha256Hex(url));
            if (File.Exists(path))
            {
                try { return File.ReadAllBytes(path); }
                catch (IOException) { /* corrupt/locked — fall through to re-fetch */ }
            }

            FetchResponse res = await _fetcher.FetchAsync(url, _maxImageBytes, ct);
            if (!res.Success) { error.Set($"Fetch failed ({res.StatusCode}): {res.Error}"); return null; }

            ValidationResult v = _validator.ValidateImageBytes(res.Data);
            if (!v.Ok) { error.Set(v.Error); return null; }

            try { File.WriteAllBytes(path, res.Data); }
            catch (IOException) { /* cache write is best-effort; serve the bytes regardless */ }
            return res.Data;
        }

        /// <summary>
        /// Returns a decoded Sprite for a URL, hitting the memory LRU first. Decode happens on the
        /// calling (main) thread. Re-validates decoded dimensions as defense-in-depth before the
        /// texture is retained. Returns null on failure.
        /// </summary>
        public async Task<Sprite> GetSpriteAsync(string url, CancellationToken ct, ResultBox error)
        {
            string key = HashUtil.Sha256Hex(url);
            if (_memory.TryGetValue(key, out var hit)) { Touch(key); return hit.Sprite; }

            byte[] raw = await GetRawAsync(url, ct, error);
            if (raw == null) return null;

            // mipChain=false, linear=false: crisp 2D sprite; no GPU mip allocation.
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!tex.LoadImage(raw, markNonReadable: true))
            {
                Object.Destroy(tex);
                error.Set("Texture decode failed.");
                return null;
            }
            if (tex.width > 2048 || tex.height > 2048) // matches ValidationConfig.MaxImageDimension ceiling
            {
                Object.Destroy(tex);
                error.Set($"Decoded texture {tex.width}x{tex.height} exceeds limit.");
                return null;
            }

            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            long bytes = (long)tex.width * tex.height * 4;
            Insert(key, sprite, bytes);
            return sprite;
        }

        private void Insert(string key, Sprite sprite, long bytes)
        {
            _memory[key] = new Entry { Sprite = sprite, Bytes = bytes };
            _lru.AddFirst(key);
            _memoryBytes += bytes;
            EvictToBudget();
        }

        private void Touch(string key)
        {
            _lru.Remove(key);
            _lru.AddFirst(key);
        }

        // Evict least-recently-used sprites until under the byte budget. Never evicts the entry
        // just inserted into oversize-budget situations to zero — leaves at least the newest.
        private void EvictToBudget()
        {
            while (_memoryBytes > _memoryBudgetBytes && _lru.Count > 1)
            {
                string victim = _lru.Last.Value;
                _lru.RemoveLast();
                if (_memory.TryGetValue(victim, out var e))
                {
                    _memoryBytes -= e.Bytes;
                    if (e.Sprite != null)
                    {
                        if (e.Sprite.texture != null) Object.Destroy(e.Sprite.texture);
                        Object.Destroy(e.Sprite);
                    }
                    _memory.Remove(victim);
                }
            }
        }

        public long MemoryBytes => _memoryBytes;
        public int MemoryCount => _memory.Count;
    }

    /// <summary>Tiny mutable box for returning an error string out of an async method by reference.</summary>
    public sealed class ResultBox
    {
        public string Error { get; private set; }
        public void Set(string e) => Error = e;
    }
}
