using NUnit.Framework;
using Shift9.Sim.Core;
using Shift9.Sim.Moves;
using Shift9.Sim.Players;

namespace Shift9.Sim.Tests
{
    public sealed class MoveSelectorTests
    {
        [Test]
        public void SignatureCrossover_OnlyForEliteHandles()
        {
            var elite = AttributeProfile.Uniform(99);
            var scrub = AttributeProfile.Uniform(40);
            var rng = new DeterministicRng(1);

            int eliteSig = 0, scrubSig = 0;
            for (int i = 0; i < 400; i++)
            {
                if (MoveSelector.SelectDribble(elite, pressured: true, ref rng) == DribbleMove.SignatureCrossover) eliteSig++;
                if (MoveSelector.SelectDribble(scrub, pressured: true, ref rng) == DribbleMove.SignatureCrossover) scrubSig++;
            }
            Assert.Greater(eliteSig, 0, "elite handle should sometimes break ankles");
            Assert.AreEqual(0, scrubSig, "a 40 handle must never get the signature crossover");
        }

        [Test]
        public void SignatureDunk_OnlyForEliteFinishers_WhenOpen()
        {
            var elite = AttributeProfile.Uniform(99);
            var scrub = AttributeProfile.Uniform(50);
            var rng = new DeterministicRng(2);

            int eliteSig = 0, scrubSig = 0, eliteDunks = 0;
            for (int i = 0; i < 400; i++)
            {
                var e = MoveSelector.SelectFinish(elite, atRim: true, contested: false, ref rng);
                if (e == FinishMove.SignatureDunk) eliteSig++;
                if (e == FinishMove.Dunk || e == FinishMove.SignatureDunk) eliteDunks++;
                if (MoveSelector.SelectFinish(scrub, atRim: true, contested: false, ref rng) == FinishMove.SignatureDunk) scrubSig++;
            }
            Assert.Greater(eliteSig, 0);
            Assert.Greater(eliteDunks, 0);
            Assert.AreEqual(0, scrubSig);
        }

        [Test]
        public void SignatureBlock_OnlyForEliteRimProtectors()
        {
            var wemby = AttributeProfile.Uniform(99);
            var guard = AttributeProfile.Uniform(60);
            var rng = new DeterministicRng(3);

            int wembyBlocks = 0, guardBlocks = 0;
            for (int i = 0; i < 400; i++)
            {
                if (MoveSelector.SelectBlock(wemby, ref rng) == DefenseMove.SignatureBlock) wembyBlocks++;
                if (MoveSelector.SelectBlock(guard, ref rng) == DefenseMove.SignatureBlock) guardBlocks++;
            }
            Assert.Greater(wembyBlocks, 0);
            Assert.AreEqual(0, guardBlocks);
        }

        [Test]
        public void PostMove_RequiresStrongInsideScorer()
        {
            var big = AttributeProfile.Uniform(90);
            var wing = AttributeProfile.Uniform(70);
            var rng = new DeterministicRng(4);

            bool bigPosted = false;
            for (int i = 0; i < 50; i++)
                if (MoveSelector.SelectPost(big, ref rng) != PostMove.None) { bigPosted = true; break; }

            Assert.IsTrue(bigPosted);
            Assert.AreEqual(PostMove.None, MoveSelector.SelectPost(wing, ref rng)); // below the gate
        }
    }
}
