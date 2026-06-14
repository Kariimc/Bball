using NUnit.Framework;
using Shift9.Sim.Core;
using UnityEngine;

namespace Shift9.Sim.Tests
{
    public sealed class CourtGeometryTests
    {
        [Test]
        public void OutOfBounds_DetectsSidelineAndBaseline()
        {
            Assert.IsFalse(CourtGeometry.IsOutOfBounds(new Vector3(0, 0, 0)));
            Assert.IsFalse(CourtGeometry.IsOutOfBounds(new Vector3(-25, 0, -47))); // exactly on lines = in
            Assert.IsTrue(CourtGeometry.IsOutOfBounds(new Vector3(26, 0, 0)));     // past sideline
            Assert.IsTrue(CourtGeometry.IsOutOfBounds(new Vector3(0, 0, 48)));     // past baseline
        }

        [Test]
        public void InPaint_HomeEnd()
        {
            Assert.IsTrue(CourtGeometry.IsInPaint(new Vector3(0, 0, -40), homeBasket: true));
            Assert.IsFalse(CourtGeometry.IsInPaint(new Vector3(0, 0, -20), homeBasket: true));  // too far out
            Assert.IsFalse(CourtGeometry.IsInPaint(new Vector3(10, 0, -40), homeBasket: true)); // too wide
            Assert.IsFalse(CourtGeometry.IsInPaint(new Vector3(0, 0, -48), homeBasket: true));  // behind baseline (the fix)
        }

        [Test]
        public void InPaint_AwayEnd()
        {
            Assert.IsTrue(CourtGeometry.IsInPaint(new Vector3(0, 0, 40), homeBasket: false));
            Assert.IsFalse(CourtGeometry.IsInPaint(new Vector3(0, 0, 20), homeBasket: false));
        }

        [Test]
        public void ThreePointArc_CornerIsStraightLine()
        {
            // Near the baseline the line is straight at +/-22 ft.
            Assert.IsTrue(CourtGeometry.IsBeyondThreePointArc(new Vector3(23, 0, -46), homeBasket: true));
            Assert.IsFalse(CourtGeometry.IsBeyondThreePointArc(new Vector3(21, 0, -46), homeBasket: true));
        }

        [Test]
        public void ThreePointArc_TopOfKeyIsRadial()
        {
            // Above the break it is an arc measured from the rim (home rim at z = -41.75).
            Assert.IsTrue(CourtGeometry.IsBeyondThreePointArc(new Vector3(0, 0, -17), homeBasket: true));
            Assert.IsFalse(CourtGeometry.IsBeyondThreePointArc(new Vector3(0, 0, -25), homeBasket: true));
        }
    }
}
