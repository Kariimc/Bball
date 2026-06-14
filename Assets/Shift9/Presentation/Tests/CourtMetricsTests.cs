using System.Collections.Generic;
using NUnit.Framework;
using Shift9.Presentation.Court;
using Shift9.Sim.Core;
using UnityEngine;

namespace Shift9.Presentation.Tests
{
    public sealed class CourtMetricsTests
    {
        [Test]
        public void Backboard_SitsFourFeetInFromBaseline()
        {
            Assert.AreEqual(-43f, CourtMetrics.BackboardZ(home: true), 0.001f);
            Assert.AreEqual(43f, CourtMetrics.BackboardZ(home: false), 0.001f);
        }

        [Test]
        public void Stanchion_SitsBehindBaseline()
        {
            Assert.AreEqual(-53f, CourtMetrics.StanchionZ(home: true, setback: 6f), 0.001f);
            Assert.Less(CourtMetrics.StanchionZ(true, 6f), CourtMetrics.BaselineZ(true)); // outside the court
        }

        [Test]
        public void Paint_SpansBaselineToFreeThrowLine()
        {
            Assert.AreEqual(-37.5f, CourtMetrics.PaintCenter(home: true).z, 0.001f); // (47 - 19/2)
            Assert.AreEqual(-28f, CourtMetrics.FreeThrowLineZ(home: true), 0.001f);   // 47 - 19
        }

        [Test]
        public void ThreePointArc_TopIsArcRadiusFromRim_EndsAtCornerWidth()
        {
            List<Vector3> arc = CourtMetrics.ThreePointArc(home: true, segments: 40);
            Vector3 rim = CourtMetrics.RimCenter(true);

            // Apex of the arc sits ArcRadius straight out from the rim, toward the court.
            Vector3 apex = arc[arc.Count / 2];
            Assert.AreEqual(0f, apex.x, 0.05f);
            Assert.AreEqual(rim.z + SimConstants.ArcRadius, apex.z, 0.05f);

            // Endpoints reach the corner three width.
            Assert.AreEqual(SimConstants.CornerThreeX, Mathf.Abs(arc[0].x), 0.05f);
            Assert.AreEqual(SimConstants.CornerThreeX, Mathf.Abs(arc[arc.Count - 1].x), 0.05f);

            // Every arc point is exactly ArcRadius from the rim center (in the floor plane).
            foreach (var p in arc)
            {
                float d = Mathf.Sqrt((p.x - rim.x) * (p.x - rim.x) + (p.z - rim.z) * (p.z - rim.z));
                Assert.AreEqual(SimConstants.ArcRadius, d, 0.01f);
            }
        }

        [Test]
        public void CornerThreeLines_RunFromBaselineToArc_AtCornerWidth()
        {
            var corners = CourtMetrics.CornerThreeLines(home: true);
            Assert.AreEqual(2, corners.Length);
            foreach (var (a, b) in corners)
            {
                Assert.AreEqual(SimConstants.CornerThreeX, Mathf.Abs(a.x), 0.01f);
                Assert.AreEqual(a.x, b.x, 0.01f);                    // stays straight in x
                Assert.AreEqual(CourtMetrics.BaselineZ(true), a.z, 0.01f); // starts at the baseline
            }
        }

        [Test]
        public void Circle_ClosesAndHoldsRadius()
        {
            var pts = CourtMetrics.Circle(Vector3.zero, 6f, 24);
            Assert.AreEqual(24, pts.Count);
            foreach (var p in pts)
                Assert.AreEqual(6f, Mathf.Sqrt(p.x * p.x + p.z * p.z), 0.001f);
        }
    }
}
