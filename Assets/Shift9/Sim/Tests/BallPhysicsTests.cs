using NUnit.Framework;
using Shift9.Sim.Ball;
using Shift9.Sim.Core;
using UnityEngine;

namespace Shift9.Sim.Tests
{
    public sealed class BallPhysicsTests
    {
        [Test]
        public void Reflect_ReversesNormalAndLosesEnergy()
        {
            Vector3 r = RimSolver.Reflect(new Vector3(0, -10, 0), Vector3.up, e: 0.4f, tangentialFriction: 0.2f);
            Assert.Greater(r.y, 0f);                                  // bounced upward
            Assert.Less(r.magnitude, 10f);                           // energy lost
            Assert.AreEqual(4f, r.y, 0.001f);                        // -0.4 * (-10)

            Vector3 r2 = RimSolver.Reflect(new Vector3(5, -10, 0), Vector3.up, 0.4f, 0.2f);
            Assert.AreEqual(4f, r2.x, 0.001f);                       // tangential bled by 0.2
            Assert.Less(r2.magnitude, new Vector3(5, -10, 0).magnitude);
        }

        [Test]
        public void LaunchedShot_SwishesThroughNet()
        {
            var from = new Vector3(0f, 7f, 0f);
            Assert.IsTrue(ProjectileSolver.TrySolveLaunch(from, SimConstants.HoopHome, 50f, out var vel, out _));

            var ball = new BallBody();
            ball.Launch(from, vel);
            var rim = RimSolver.ForHome();

            bool swished = false;
            for (int i = 0; i < 600; i++)
            {
                if (ball.Step(SimConstants.FixedTimestep, rim) == BallContact.ThroughNet) { swished = true; break; }
            }
            Assert.IsTrue(swished, "A shot aimed at the rim center should pass through the net.");
        }

        [Test]
        public void ShotAimedAtRing_HitsRim()
        {
            var from = new Vector3(0f, 7f, 0f);
            var ringPoint = SimConstants.HoopHome + new Vector3(SimConstants.RimRadius, 0f, 0f);
            Assert.IsTrue(ProjectileSolver.TrySolveLaunch(from, ringPoint, 50f, out var vel, out _));

            var ball = new BallBody();
            ball.Launch(from, vel);
            var rim = RimSolver.ForHome();

            bool hitRim = false;
            for (int i = 0; i < 600; i++)
            {
                if (ball.Step(SimConstants.FixedTimestep, rim) == BallContact.Rim) { hitRim = true; break; }
            }
            Assert.IsTrue(hitRim, "A shot aimed at the ring should strike the rim.");
        }

        [Test]
        public void Flight_IsDeterministic()
        {
            var from = new Vector3(0f, 7f, 0f);
            ProjectileSolver.TrySolveLaunch(from, SimConstants.HoopHome, 50f, out var vel, out _);

            var a = new BallBody(); a.Launch(from, vel);
            var b = new BallBody(); b.Launch(from, vel);
            var rimA = RimSolver.ForHome();
            var rimB = RimSolver.ForHome();

            for (int i = 0; i < 120; i++)
            {
                a.Step(SimConstants.FixedTimestep, rimA);
                b.Step(SimConstants.FixedTimestep, rimB);
                Assert.AreEqual(a.Position, b.Position);
            }
        }
    }
}
