using NUnit.Framework;
using Shift9.Sim.Core;
using Shift9.Sim.Players;
using Shift9.Sim.Shooting;
using UnityEngine;

namespace Shift9.Sim.Tests
{
    public sealed class ShotResolverTests
    {
        private static readonly ShotModelConfig Cfg = ShotModelConfig.Default;
        private static readonly Vector3 ThreeSpot = new Vector3(0, 0, -17f);  // beyond home arc

        private static ShotContext Ctx(Vector3 pos, byte rating, float openness, float dt,
            bool freeThrow = false, float stamina = 1f, float hot = 1f)
            => new ShotContext
            {
                Position = pos, HomeBasket = true,
                Attributes = AttributeProfile.Uniform(rating),
                Dynamics = new PlayerDynamics { Stamina = stamina, HotHand = hot },
                Openness = openness, ReleaseErrorSeconds = dt, IsFreeThrow = freeThrow
            };

        [Test]
        public void Resolve_IsDeterministicForSameSeed()
        {
            var ctx = Ctx(ThreeSpot, 75, openness: 0.8f, dt: 0.04f);
            var a = new DeterministicRng(2024);
            var b = new DeterministicRng(2024);
            var ra = ShotResolver.Resolve(ctx, ref a, Cfg);
            var rb = ShotResolver.Resolve(ctx, ref b, Cfg);
            Assert.AreEqual(ra.Made, rb.Made);
            Assert.AreEqual(ra.Probability, rb.Probability);
        }

        [Test]
        public void Resolve_HigherRatingRaisesProbability()
        {
            var rng = new DeterministicRng(1);
            float low = ShotResolver.Resolve(Ctx(ThreeSpot, 30, 1f, 0f), ref rng, Cfg).Probability;
            float high = ShotResolver.Resolve(Ctx(ThreeSpot, 90, 1f, 0f), ref rng, Cfg).Probability;
            Assert.Greater(high, low);
        }

        [Test]
        public void Resolve_GreenBeatsBadTiming()
        {
            var rng = new DeterministicRng(5);
            float green = ShotResolver.Resolve(Ctx(ThreeSpot, 60, 1f, 0f), ref rng, Cfg).Probability;
            float bad = ShotResolver.Resolve(Ctx(ThreeSpot, 60, 1f, 0.3f), ref rng, Cfg).Probability;
            Assert.Greater(green, bad);
        }

        [Test]
        public void Resolve_OpenBeatsContested()
        {
            var rng = new DeterministicRng(9);
            float open = ShotResolver.Resolve(Ctx(ThreeSpot, 60, 1f, 0f), ref rng, Cfg).Probability;
            float contested = ShotResolver.Resolve(Ctx(ThreeSpot, 60, 0f, 0f), ref rng, Cfg).Probability;
            Assert.Greater(open, contested);
        }

        [Test]
        public void Resolve_GreenFlagSetOnPerfectRelease()
        {
            var rng = new DeterministicRng(3);
            Assert.IsTrue(ShotResolver.Resolve(Ctx(ThreeSpot, 60, 1f, 0f), ref rng, Cfg).IsGreen);
            Assert.IsFalse(ShotResolver.Resolve(Ctx(ThreeSpot, 60, 1f, 0.2f), ref rng, Cfg).IsGreen);
        }

        [Test]
        public void Resolve_FreeThrow_UsesFreeThrowRatingAndIgnoresContest()
        {
            var rng = new DeterministicRng(11);
            // 99 FT shooter, fully smothered + bad timing must STILL be high (FTs ignore both).
            var ctx = Ctx(Vector3.zero, 99, openness: 0f, dt: 0.5f, freeThrow: true);
            var res = ShotResolver.Resolve(ctx, ref rng, Cfg);
            Assert.AreEqual(ShotZone.FreeThrow, res.Zone);
            Assert.IsFalse(res.IsGreen);
            Assert.Greater(res.Probability, 0.9f);
        }

        [Test]
        public void Resolve_ProbabilityAlwaysWithinClamp()
        {
            var rng = new DeterministicRng(77);
            // Worst possible jumper.
            float worst = ShotResolver.Resolve(Ctx(ThreeSpot, 0, 0f, 1f, stamina: 0f), ref rng, Cfg).Probability;
            // Best possible layup.
            float best = ShotResolver.Resolve(Ctx(new Vector3(0, 0, -39.75f), 99, 1f, 0f, hot: 1.15f),
                ref rng, Cfg).Probability;
            Assert.GreaterOrEqual(worst, Cfg.MinProbability);
            Assert.LessOrEqual(best, Cfg.MaxProbability);
        }
    }
}
