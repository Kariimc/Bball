using NUnit.Framework;
using Shift9.Sim.Shooting;
using UnityEngine;

namespace Shift9.Sim.Tests
{
    public sealed class ShotClassifierTests
    {
        [Test]
        public void ClassifiesByZone_HomeEnd()
        {
            Assert.AreEqual(ShotZone.ThreePoint,
                ShotClassifier.Classify(new Vector3(0, 0, -17f), homeBasket: true).Zone);
            Assert.AreEqual(ShotZone.AtRim,
                ShotClassifier.Classify(new Vector3(0, 0, -39.75f), homeBasket: true).Zone);
            Assert.AreEqual(ShotZone.Close,
                ShotClassifier.Classify(new Vector3(0, 0, -35f), homeBasket: true).Zone);
            Assert.AreEqual(ShotZone.MidRange,
                ShotClassifier.Classify(new Vector3(0, 0, -28f), homeBasket: true).Zone);
        }

        [Test]
        public void ReportsDistanceFromRim()
        {
            var c = ShotClassifier.Classify(new Vector3(0, 0, -39.75f), homeBasket: true);
            Assert.AreEqual(2f, c.Distance, 0.01f); // rim at z = -41.75
        }
    }
}
