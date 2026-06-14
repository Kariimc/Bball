using NUnit.Framework;
using Shift9.Sim.Core;
using UnityEngine;

namespace Shift9.Sim.Tests
{
    public sealed class ProjectileSolverTests
    {
        [Test]
        public void Solve_ReachableShot_LandsOnTarget()
        {
            var from = new Vector3(0f, 7f, 0f);
            var target = SimConstants.HoopHome;   // (0, 10, -41.75)

            bool ok = ProjectileSolver.TrySolveLaunch(from, target, 50f, out var vel, out float t);

            Assert.IsTrue(ok, "A normal jump shot must be solvable — this was the original bug.");
            Assert.Greater(t, 0f);

            // The predicted ball position at flight-time must match the target.
            Vector3 landing = ProjectileSolver.PredictPosition(from, vel, t);
            Assert.Less(Vector3.Distance(landing, target), 0.05f);
        }

        [Test]
        public void Solve_RejectsUnreachableTarget()
        {
            // Target far above, at a shallow angle => geometrically impossible.
            bool ok = ProjectileSolver.TrySolveLaunch(
                new Vector3(0, 0, 0), new Vector3(0, 100, 1), 10f, out _, out _);
            Assert.IsFalse(ok);
        }

        [Test]
        public void Solve_RejectsStraightUpShot()
        {
            bool ok = ProjectileSolver.TrySolveLaunch(
                new Vector3(0, 0, 0), new Vector3(0, 5, 0), 45f, out _, out _);
            Assert.IsFalse(ok); // no horizontal distance => no parabolic solution
        }
    }
}
