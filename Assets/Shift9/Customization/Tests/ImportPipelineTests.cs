using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Shift9.Customization.Caching;
using Shift9.Customization.Mapping;
using Shift9.Customization.Pipeline;
using Shift9.Customization.Validation;

namespace Shift9.Customization.Tests
{
    public sealed class ImportPipelineTests
    {
        private const string ManifestUrl = "https://cdn.example.com/league.json";
        private string _dir;

        [SetUp]
        public void Setup() =>
            _dir = Path.Combine(Path.GetTempPath(), "shift9pipe_" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void Teardown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private sealed class StateRecorder : IProgress<ImportState>
        {
            public readonly List<ImportState> States = new();
            public void Report(ImportState s) => States.Add(s);
        }

        private (ImportPipeline pipeline, StateRecorder rec) Build(FakeFetcher fetcher)
        {
            var cfg = ValidationConfig.Default;
            var validator = new ContentValidator(cfg);
            var cache = new AssetCache(_dir, fetcher, validator, 16 * 1024 * 1024, cfg.MaxImageBytes);
            var rec = new StateRecorder();
            return (new ImportPipeline(fetcher, validator, cfg, cache, rec), rec);
        }

        [Test]
        public void ImportLeague_SuccessMapsAndReachesApplied()
        {
            var fetcher = new FakeFetcher();
            fetcher.Map[ManifestUrl] = TestData.Utf8(TestData.LeagueJson());
            fetcher.Map["https://cdn.example.com/floor.png"] = TestData.Png(128, 128);
            fetcher.Map["https://cdn.example.com/home.png"] = TestData.Png(64, 64);

            var (pipeline, rec) = Build(fetcher);
            ImportResult<RuntimeLeague> res =
                pipeline.ImportLeagueAsync(ManifestUrl, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(res.Success, res.Error);
            Assert.AreEqual("wnba", res.Value.Id);
            Assert.AreEqual(1, res.Value.Teams.Count);
            Assert.AreEqual(1, res.Value.Teams[0].Uniforms.Count);   // insecure away uniform dropped
            Assert.AreEqual(99, res.Value.Teams[0].Players[0].Number); // 250 clamped
            Assert.Contains(ImportState.Applied, rec.States);
            Assert.IsFalse(rec.States.Contains(ImportState.Failed));
        }

        [Test]
        public void ImportLeague_RejectsUnsafeUrl()
        {
            var (pipeline, rec) = Build(new FakeFetcher());
            var res = pipeline.ImportLeagueAsync("http://insecure/league.json",
                CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsFalse(res.Success);
            Assert.Contains(ImportState.Failed, rec.States);
        }

        [Test]
        public void ImportLeague_FailsOnFetchError()
        {
            var fetcher = new FakeFetcher(); // empty map → 404 for manifest
            var (pipeline, _) = Build(fetcher);
            var res = pipeline.ImportLeagueAsync(ManifestUrl, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsFalse(res.Success);
            StringAssert.Contains("Fetch failed", res.Error);
        }

        [Test]
        public void ImportLeague_FailsOnInvalidJson()
        {
            var fetcher = new FakeFetcher();
            fetcher.Map[ManifestUrl] = TestData.Utf8("{ this is not valid json ]");
            var (pipeline, _) = Build(fetcher);
            var res = pipeline.ImportLeagueAsync(ManifestUrl, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsFalse(res.Success);
            StringAssert.Contains("JSON", res.Error);
        }

        [Test]
        public void ImportLeague_FailsOnSchemaMismatch()
        {
            var fetcher = new FakeFetcher();
            fetcher.Map[ManifestUrl] = TestData.Utf8("{\"schema\":99,\"league\":{\"id\":\"x\"},\"teams\":[]}");
            var (pipeline, _) = Build(fetcher);
            var res = pipeline.ImportLeagueAsync(ManifestUrl, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsFalse(res.Success);
            StringAssert.Contains("schema", res.Error);
        }

        [Test]
        public void ImportSneakers_SuccessDropsInsecure()
        {
            var fetcher = new FakeFetcher();
            fetcher.Map["https://cdn.example.com/sneakers.json"] = TestData.Utf8(TestData.SneakerJson());
            fetcher.Map["https://cdn.example.com/kicks.png"] = TestData.Png(96, 96);
            var (pipeline, _) = Build(fetcher);

            var res = pipeline.ImportSneakersAsync("https://cdn.example.com/sneakers.json",
                CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(res.Success, res.Error);
            Assert.AreEqual(1, res.Value.Count);          // insecure s2 dropped
            Assert.AreEqual("s1", res.Value[0].Id);
        }
    }
}
