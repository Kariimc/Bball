using UnityEngine;

namespace Shift9.Sim.Core
{
    /// <summary>
    /// Solves and predicts pure-parabola ball flight (gravity only, no spin). This is the math
    /// that aims a shot or a pass so the ball arrives at a target.
    /// </summary>
    public static class ProjectileSolver
    {
        /// <summary>
        /// Finds the launch velocity that sends the ball from <paramref name="from"/> through
        /// <paramref name="target"/> at a chosen launch angle. Returns false when the target is
        /// unreachable at that angle.
        ///
        /// Aim identity:  v^2 = g * x^2 / ( 2 * cos^2(a) * (x*tan(a) - y) ),  where g is the
        /// gravity MAGNITUDE, x = horizontal distance, y = vertical offset. Feeding a negative
        /// gravity here (the Shift9 bug) made v^2 negative and rejected every real shot.
        /// </summary>
        public static bool TrySolveLaunch(Vector3 from, Vector3 target, float launchAngleDegrees,
            out Vector3 launchVelocity, out float flightTime)
        {
            launchVelocity = Vector3.zero;
            flightTime = 0f;

            Vector3 displacement = target - from;
            Vector3 planar = new Vector3(displacement.x, 0f, displacement.z);
            float x = planar.magnitude;
            float y = displacement.y;

            if (x < 1e-4f) return false;                   // straight-up shot: not a parabola

            float angle = launchAngleDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle);
            if (angle <= 0f || cos < 1e-4f) return false;  // degenerate angle

            float denom = 2f * (x * Mathf.Tan(angle) - y) * cos * cos;
            if (denom <= 1e-4f) return false;              // target above the angle's reach

            float v2 = (SimConstants.GravityMagnitude * x * x) / denom;
            float v = Mathf.Sqrt(v2);

            float horizontalSpeed = v * cos;
            flightTime = x / horizontalSpeed;

            launchVelocity = planar / x * horizontalSpeed; // direction * horizontal speed
            launchVelocity.y = v * Mathf.Sin(angle);
            return true;
        }

        /// <summary>
        /// Closed-form position of a launched ball at time t (for drawing the aim arc).
        /// p(t) = from + v*t + 0.5 * gravity * t^2.
        /// </summary>
        public static Vector3 PredictPosition(Vector3 from, Vector3 launchVelocity, float t)
        {
            float dy = -0.5f * SimConstants.GravityMagnitude * t * t;
            return new Vector3(
                from.x + launchVelocity.x * t,
                from.y + launchVelocity.y * t + dy,
                from.z + launchVelocity.z * t);
        }
    }
}
