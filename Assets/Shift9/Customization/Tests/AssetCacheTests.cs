using System.IO;
using System.Threading;
using NUnit.Framework;
using Shift9.Customization.Caching;
using Shift9.Customization.Validation;

namespace Shift9.Customization.Tests
{
    public sealed class AssetCacheTests
    {
        private string _dir;

        [SetUp]
        public void Setup()
        {
            _dir = Path.Combine(Path.GetTempPath(), "shift9cache_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private AssetCache NewCache(FakeFetcher f) =>
            new AssetCache(_dir, f, new ContentValidator(ValidationConfig.Default),
                memoryBudgetBytes: 16 * 1024 * 1024, maxImageBytes: ValidationConfig.Default.MaxImageBytes);

        [Test]
        public void GetRaw_FetchesOnceThenServesFromDisk()
        {
            const string url = "https://cdn.example.com/logo.png";
            var fetcher = new FakeFetcher();
            fetcher.Map[url] = TestData.Png(64, 64);
            var cache = NewCache(fetcher);

            var box = new ResultBox();
            byte[] first = cache.GetRawAsync(url, CancellationToken.None, box).GetAwaiter().GetResult();
            Assert.IsNotNull(first);
            Assert.AreEqual(1, fetcher.Calls);

            // Second call must hit disk, not the network.
            byte[] second = cache.GetRawAsync(url, CancellationToken.None, box).GetAwaiter().GetResult();
            Assert.IsNotNull(second);
            Assert.AreEqual(1, fetcher.Calls);
            Assert.AreEqual(first.Length, second.Length);
        }

        [Test]
        public void GetRaw_RejectsInvalidImageBytes()
        {
            const string url = "https://cdn.example.com/notimage.png";
            var fetcher = new FakeFetcher();
            fetcher.Map[url] = TestData.Utf8("<html>not an image</html>");
            var cache = NewCache(fetcher);

            var box = new ResultBox();
            byte[] raw = cache.GetRawAsync(url, CancellationToken.None, box).GetAwaiter().GetResult();
            Assert.IsNull(raw);
            Assert.IsNotNull(box.Error);
            Assert.IsFalse(File.Exists(Path.Combine(_dir, HashUtil.Sha256Hex(url)))); // nothing cached
        }

        [Test]
        public void GetRaw_PropagatesFetchFailure()
        {
            var fetcher = new FakeFetcher(); // empty map → 404
            var cache = NewCache(fetcher);
            var box = new ResultBox();
            byte[] raw = cache.GetRawAsync("https://cdn.example.com/missing.png",
                CancellationToken.None, box).GetAwaiter().GetResult();
            Assert.IsNull(raw);
            StringAssert.Contains("Fetch failed", box.Error);
        }
    }
}
