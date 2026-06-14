using Shift9.Sim.Core;
using Shift9.Sim.Match;
using UnityEngine;

namespace Shift9.Presentation.Players
{
    /// <summary>
    /// Presentation-side view of the shared <see cref="Formation"/> (the static blockout builder
    /// uses this), plus an in-bounds helper. Positions are sourced from the sim so the showcase and
    /// the live possession agree.
    /// </summary>
    public static class PlayerPlacement
    {
        public const int TeamSize = Formation.TeamSize;

        public static Vector3[] Offense() => Formation.Offense();
        public static Vector3[] Defense() => Formation.Defense();

        /// <summary>True if a floor position is inside the playing surface.</summary>
        public static bool InBounds(Vector3 p) =>
            Mathf.Abs(p.x) <= SimConstants.CourtHalfWidth &&
            Mathf.Abs(p.z) <= SimConstants.CourtHalfLength;
    }
}
