using NUnit.Framework;
using Shift9.Sim.Core;

namespace Shift9.Sim.Tests
{
    public sealed class DeterministicRngTests
    {
        [Test]
        public void SameSeed_ProducesIdenticalSequence()
        {
            var a = new DeterministicRng(12345);
            var b = new DeterministicRng(12345);
            for (int i = 0; i < 16; i++)
                Assert.AreEqual(a.NextULong(), b.NextULong());
        }

        [Test]
        public void DifferentSeed_DivergesQuickly()
        {
            var a = new DeterministicRng(1);
            var b = new DeterministicRng(2);
            bool anyDifferent = false;
            for (int i = 0; i < 8; i++)
                if (a.NextULong() != b.NextULong()) { anyDifferent = true; break; }
            Assert.IsTrue(anyDifferent);
        }

        [Test]
        public void NextFloat_StaysInUnitInterval()
        {
            var rng = new DeterministicRng(99);
            for (int i = 0; i < 5000; i++)
            {
                float f = rng.NextFloat();
                Assert.GreaterOrEqual(f, 0f);
                Assert.Less(f, 1f);
            }
        }

        [Test]
        public void Range_RespectsBoundsAndEmpty()
        {
            var rng = new DeterministicRng(7);
            for (int i = 0; i < 2000; i++)
            {
                int v = rng.Range(5, 10);
                Assert.GreaterOrEqual(v, 5);
                Assert.Less(v, 10);
            }
            Assert.AreEqual(3, rng.Range(3, 3));   // empty range returns min
            Assert.AreEqual(3, rng.Range(3, 1));   // inverted range returns min
        }

        [Test]
        public void Chance_HandlesCertaintyBounds()
        {
            var rng = new DeterministicRng(42);
            Assert.IsFalse(rng.Chance(0f));
            Assert.IsTrue(rng.Chance(1f));
        }

        [Test]
        public void Fork_DoesNotDisturbParentStream()
        {
            var parent = new DeterministicRng(555);
            ulong stateBefore = parent.State;
            DeterministicRng child = parent.Fork(13);

            Assert.AreEqual(stateBefore, parent.State);          // parent untouched
            // Child is independent: its first draw should not equal the parent's next draw.
            Assert.AreNotEqual(child.NextULong(), parent.NextULong());
        }
    }
}
