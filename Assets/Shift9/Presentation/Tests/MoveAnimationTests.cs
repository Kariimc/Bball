using NUnit.Framework;
using Shift9.Presentation.Animation;
using Shift9.Sim.Moves;

namespace Shift9.Presentation.Tests
{
    // The animator state graph (AnimatorControllerFactory) routes on these exact MoveId values,
    // so they must stay locked to what the driver sends.
    public sealed class MoveAnimationTests
    {
        [Test]
        public void DribbleIds_AreStable()
        {
            Assert.AreEqual(0, MoveAnimation.Id(DribbleMove.None));
            Assert.AreEqual(1, MoveAnimation.Id(DribbleMove.Crossover));
            Assert.AreEqual(2, MoveAnimation.Id(DribbleMove.Hesitation));
            Assert.AreEqual(3, MoveAnimation.Id(DribbleMove.BehindBack));
            Assert.AreEqual(4, MoveAnimation.Id(DribbleMove.BetweenLegs));
            Assert.AreEqual(5, MoveAnimation.Id(DribbleMove.SignatureCrossover));
        }

        [Test]
        public void FinishIds_AreStable()
        {
            Assert.AreEqual(0, MoveAnimation.Id(FinishMove.JumpShot)); // uses the Shoot trigger, not a move
            Assert.AreEqual(10, MoveAnimation.Id(FinishMove.Layup));
            Assert.AreEqual(11, MoveAnimation.Id(FinishMove.Floater));
            Assert.AreEqual(12, MoveAnimation.Id(FinishMove.Dunk));
            Assert.AreEqual(13, MoveAnimation.Id(FinishMove.SignatureDunk));
        }

        [Test]
        public void BlockId_IsStable()
        {
            Assert.AreEqual(20, MoveAnimation.BlockId);
            Assert.AreEqual(0, MoveAnimation.None);
        }
    }
}
