using Shift9.Sim.Core;
using UnityEngine;

namespace Shift9.Sim.Ball
{
    /// <summary>
    /// Deterministic collision response for one hoop: the rim ring, the backboard, and the net.
    /// Bounces use an explicit coefficient of restitution so behaviour is reproducible (Unity's
    /// physic materials are avoided on purpose). Configure one per basket via the factories.
    /// </summary>
    public sealed class RimSolver
    {
        private readonly Vector3 _rimCenter;
        private readonly float _rimRadius;
        private readonly Plane _backboard;

        // Material response (tuned, overridable by callers that construct directly).
        private readonly float _rimRestitution;
        private readonly float _rimTangential;
        private readonly float _boardRestitution;
        private readonly float _boardTangential;
        private readonly float _netDamp;
        private readonly float _netDownBias;

        public const float BackboardInset = 1.25f; // backboard sits this far behind the rim center

        public RimSolver(Vector3 rimCenter, Plane backboard,
            float rimRestitution = 0.40f, float rimTangential = 0.20f,
            float boardRestitution = 0.50f, float boardTangential = 0.35f,
            float netDamp = 0.12f, float netDownBias = 1.2f)
        {
            _rimCenter = rimCenter;
            _rimRadius = SimConstants.RimRadius;
            _backboard = backboard;
            _rimRestitution = rimRestitution;
            _rimTangential = rimTangential;
            _boardRestitution = boardRestitution;
            _boardTangential = boardTangential;
            _netDamp = netDamp;
            _netDownBias = netDownBias;
        }

        public static RimSolver ForHome() => new RimSolver(
            SimConstants.HoopHome,
            new Plane(new Vector3(0, 0, 1), new Vector3(0, SimConstants.RimHeight, SimConstants.HoopHome.z - BackboardInset)));

        public static RimSolver ForAway() => new RimSolver(
            SimConstants.HoopAway,
            new Plane(new Vector3(0, 0, -1), new Vector3(0, SimConstants.RimHeight, SimConstants.HoopAway.z + BackboardInset)));

        /// <summary>
        /// Resolves the ball over one step. Adjusts <paramref name="pos"/>/<paramref name="vel"/>
        /// in place and reports what was struck. Checked in order: rim ring, backboard, then the
        /// net (a clean descent through the hoop interior).
        /// </summary>
        public BallContact Resolve(ref Vector3 pos, ref Vector3 vel, float ballRadius, float dt)
        {
            // --- Rim ring: nearest point on the circle at rim height ---
            Vector3 flat = pos - _rimCenter; flat.y = 0f;
            float ringDist = flat.magnitude;
            Vector3 nearest = ringDist > 1e-4f
                ? _rimCenter + flat / ringDist * _rimRadius
                : _rimCenter + new Vector3(_rimRadius, 0f, 0f);
            nearest.y = _rimCenter.y;

            Vector3 delta = pos - nearest;
            if (delta.sqrMagnitude < ballRadius * ballRadius)
            {
                Vector3 n = delta.sqrMagnitude > 1e-8f ? delta.normalized : Vector3.up;
                pos = nearest + n * ballRadius;                       // push out of the rim
                vel = Reflect(vel, n, _rimRestitution, _rimTangential);
                return BallContact.Rim;
            }

            // --- Backboard plane ---
            float d = _backboard.GetDistanceToPoint(pos);
            if (d < ballRadius && Vector3.Dot(vel, _backboard.normal) < 0f)
            {
                pos += _backboard.normal * (ballRadius - d);
                vel = Reflect(vel, _backboard.normal, _boardRestitution, _boardTangential);
                return BallContact.Backboard;
            }

            // --- Net: clean descent through the hoop interior ---
            if (ringDist < _rimRadius * 0.85f && pos.y < _rimCenter.y && vel.y < 0f)
            {
                vel *= (1f - _netDamp);          // net resistance
                vel.y -= _netDownBias * dt;      // pull straight down for a clean drop
                return BallContact.ThroughNet;
            }

            return BallContact.None;
        }

        /// <summary>
        /// Bounce a velocity off a surface: the normal component reverses and is scaled by the
        /// restitution <paramref name="e"/>; the sideways component is bled by
        /// <paramref name="tangentialFriction"/>. Result energy is always &lt;= input for e,ft in [0,1].
        ///   v' = -e*(v·n)n + (1 - ft)*(v - (v·n)n)
        /// </summary>
        public static Vector3 Reflect(Vector3 v, Vector3 n, float e, float tangentialFriction)
        {
            float vn = Vector3.Dot(v, n);
            Vector3 normalComp = vn * n;
            Vector3 tangentComp = v - normalComp;
            return -e * normalComp + (1f - tangentialFriction) * tangentComp;
        }
    }
}
