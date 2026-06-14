using Shift9.Sim.Core;
using UnityEngine;

namespace Shift9.Sim.Ball
{
    /// <summary>
    /// The physical ball in flight. Integrates under gravity with velocity-Verlet (exact for
    /// constant acceleration) and resolves rim/backboard/net contacts each step. Deterministic:
    /// no Unity physics, no per-frame randomness. Stores Prev/Curr positions so the renderer can
    /// smoothly interpolate between the 60 Hz simulation steps.
    /// </summary>
    public sealed class BallBody
    {
        public Vector3 Position { get; private set; }
        public Vector3 PrevPosition { get; private set; }
        public Vector3 Velocity { get; private set; }
        public float Radius { get; }

        public BallBody(float radius = SimConstants.BallRadius) => Radius = radius;

        public void Launch(Vector3 origin, Vector3 velocity)
        {
            Position = PrevPosition = origin;
            Velocity = velocity;
        }

        /// <summary>
        /// Advances one fixed step. Returns what the ball struck this step (None / Rim /
        /// Backboard / ThroughNet). Pass the rim solver for the basket being attacked, or null
        /// for free flight (e.g., a pass).
        /// </summary>
        public BallContact Step(float dt, RimSolver rim)
        {
            PrevPosition = Position;

            Vector3 a = new Vector3(0f, -SimConstants.GravityMagnitude, 0f);
            // Constant-acceleration integration: exact, so flight reproduces the solved arc.
            Vector3 newPos = Position + Velocity * dt + 0.5f * a * dt * dt;
            Vector3 newVel = Velocity + a * dt;

            BallContact contact = BallContact.None;
            if (rim != null)
                contact = rim.Resolve(ref newPos, ref newVel, Radius, dt);

            Position = newPos;
            Velocity = newVel;
            return contact;
        }
    }
}
