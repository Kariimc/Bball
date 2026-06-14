using UnityEngine;

namespace Shift9.Sim.Core
{
    /// <summary>
    /// Fixed physical and court dimensions, all in FEET so distances read like real basketball.
    /// Gravity is stored as a POSITIVE magnitude; code applies the downward sign explicitly.
    /// (The original Shift9 draft stored a negative gravity and fed it straight into the shot
    /// formula, which forced every shot to register as impossible — see ProjectileSolver.)
    /// </summary>
    public static class SimConstants
    {
        // --- Time ---
        public const float FixedTimestep = 1f / 60f;   // simulation runs at a steady 60 Hz

        // --- Physics ---
        public const float GravityMagnitude = 32.174f; // ft/s^2, applied downward

        // --- Ball / rim ---
        public const float BallRadius = 0.395f;        // ~9.5" diameter ball
        public const float RimRadius = 0.75f;          // 18" rim
        public const float RimHeight = 10f;

        // --- Court (full court 94 x 50 ft; values below are HALF extents from center) ---
        public const float CourtHalfLength = 47f;      // baseline at +/- 47 (z axis = length)
        public const float CourtHalfWidth = 25f;       // sideline at +/- 25 (x axis = width)
        public const float RimInsetFromBaseline = 5.25f; // rim center sits 5.25 ft in from baseline

        // --- Painted area (16 ft wide, 19 ft deep from baseline) ---
        public const float PaintHalfWidth = 8f;
        public const float PaintDepth = 19f;

        // --- Three-point line ---
        public const float ArcRadius = 23.75f;         // top-of-key distance from rim
        public const float CornerThreeX = 22f;         // straight corner line distance
        public const float CornerStraightLength = 14f; // corners are straight for 14 ft off the baseline

        // Home basket sits toward -z, away basket toward +z.
        public static readonly Vector3 HoopHome = new Vector3(0f, RimHeight, -(CourtHalfLength - RimInsetFromBaseline));
        public static readonly Vector3 HoopAway = new Vector3(0f, RimHeight, +(CourtHalfLength - RimInsetFromBaseline));
    }
}
