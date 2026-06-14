using NUnit.Framework;
using Shift9.Customization.Model;
using Shift9.Customization.Validation;

namespace Shift9.Customization.Tests
{
    public sealed class ContentValidatorTests
    {
        private ContentValidator _v;
        private ValidationConfig _cfg;

        [SetUp]
        public void Setup()
        {
            _cfg = ValidationConfig.Default;
            _v = new ContentValidator(_cfg);
        }

        [Test]
        public void ManifestBytes_RejectsEmptyAndOversized()
        {
            Assert.IsFalse(_v.ValidateManifestBytes(null).Ok);
            Assert.IsFalse(_v.ValidateManifestBytes(new byte[0]).Ok);
            Assert.IsFalse(_v.ValidateManifestBytes(new byte[_cfg.MaxManifestBytes + 1]).Ok);
            Assert.IsTrue(_v.ValidateManifestBytes(new byte[16]).Ok);
        }

        [TestCase("https://cdn.example.com/x.png", true)]
        [TestCase("http://cdn.example.com/x.png", false)]   // not https
        [TestCase("file:///etc/passwd", false)]             // local file
        [TestCase("https://localhost/x.png", false)]        // localhost
        [TestCase("https://127.0.0.1/x.png", false)]        // loopback
        [TestCase("https://10.0.0.5/x.png", false)]         // RFC1918
        [TestCase("https://192.168.1.4/x.png", false)]      // RFC1918
        [TestCase("https://169.254.1.1/x.png", false)]      // link-local
        [TestCase("not a url", false)]
        [TestCase("", false)]
        public void IsUrlSafe_EnforcesHttpsAndPublicHost(string url, bool expected)
        {
            Assert.AreEqual(expected, _v.IsUrlSafe(url));
        }

        [Test]
        public void ImageBytes_AcceptsSmallPngAndJpeg()
        {
            Assert.IsTrue(_v.ValidateImageBytes(TestData.Png(256, 256)).Ok);
            Assert.IsTrue(_v.ValidateImageBytes(TestData.Jpeg(128, 64)).Ok);
        }

        [Test]
        public void ImageBytes_RejectsOversizedDimensions()
        {
            var res = _v.ValidateImageBytes(TestData.Png(5000, 32));
            Assert.IsFalse(res.Ok);
            StringAssert.Contains("exceeds max side", res.Error);
        }

        [Test]
        public void ImageBytes_RejectsNonImage()
        {
            Assert.IsFalse(_v.ValidateImageBytes(TestData.Utf8("<html>nope</html>")).Ok);
        }

        [Test]
        public void League_RejectsSchemaMismatchAndEmptyTeams()
        {
            Assert.IsFalse(_v.ValidateLeague(new LeagueManifest { Schema = 99 }).Ok);
            Assert.IsFalse(_v.ValidateLeague(
                new LeagueManifest { Schema = 1, League = new LeagueDef { Id = "l" } }).Ok);
        }

        [Test]
        public void League_SanitizesClampsAndDropsUnsafe()
        {
            var m = new LeagueManifest
            {
                Schema = 1,
                League = new LeagueDef { Id = "l", Name = "W <b>League</b>", Type = "WNBA" },
                Teams = new System.Collections.Generic.List<TeamDef>
                {
                    new TeamDef
                    {
                        Id = "t1", Name = "Lib<script>", Primary = "#1d428a", Secondary = "bad",
                        Uniforms = new System.Collections.Generic.List<UniformDef>
                        {
                            new UniformDef { Slot = "Home", BaseUrl = "https://cdn.example.com/h.png" },
                            new UniformDef { Slot = "Away", BaseUrl = "http://insecure/a.png" } // unsafe → dropped
                        },
                        Players = new System.Collections.Generic.List<PlayerDef>
                        {
                            new PlayerDef { Id = "p1", Name = "Star", Number = 250,
                                Attributes = new AttributeBlock { Speed = 150, ThreePoint = 80 } }
                        }
                    }
                }
            };

            var res = _v.ValidateLeague(m);
            Assert.IsTrue(res.Ok);

            Assert.AreEqual("W League", m.League.Name);          // rich-text tag span stripped
            Assert.AreEqual("Lib", m.Teams[0].Name);             // injected <script> tag removed
            Assert.AreEqual("#1D428A", m.Teams[0].Primary);      // normalized upper
            Assert.AreEqual("#FFFFFF", m.Teams[0].Secondary);    // invalid → fallback
            Assert.AreEqual(1, m.Teams[0].Uniforms.Count);       // insecure uniform dropped
            Assert.AreEqual(99, m.Teams[0].Players[0].Number);   // clamped 0..99
            Assert.AreEqual(99, m.Teams[0].Players[0].Attributes.Speed);   // clamped
            Assert.AreEqual(80, m.Teams[0].Players[0].Attributes.ThreePoint);
            Assert.IsNotNull(res.Warnings);
            Assert.IsTrue(res.Warnings.Count >= 2);              // color fallback + dropped uniform
        }

        [Test]
        public void League_RejectsTooManyTeams()
        {
            var m = new LeagueManifest { Schema = 1, League = new LeagueDef { Id = "l" },
                Teams = new System.Collections.Generic.List<TeamDef>() };
            for (int i = 0; i <= _cfg.MaxTeams; i++)
                m.Teams.Add(new TeamDef { Id = "t" + i, Primary = "#000000", Secondary = "#FFFFFF" });
            Assert.IsFalse(_v.ValidateLeague(m).Ok);
        }

        [Test]
        public void Sneakers_DropsInvalidKeepsValid()
        {
            var m = new SneakerManifest
            {
                Schema = 1,
                Sneakers = new System.Collections.Generic.List<SneakerDef>
                {
                    new SneakerDef { Id = "s1", Name = "Good", ImageUrl = "https://cdn.example.com/k.png" },
                    new SneakerDef { Id = "s2", Name = "Bad",  ImageUrl = "http://insecure/k.png" }
                }
            };
            var res = _v.ValidateSneakers(m);
            Assert.IsTrue(res.Ok);
            Assert.AreEqual(1, m.Sneakers.Count);
            Assert.AreEqual("s1", m.Sneakers[0].Id);
        }
    }
}
