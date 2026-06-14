using Shift9.Sim.Core;
using UnityEngine;

namespace Shift9.Presentation.Players
{
    /// <summary>
    /// A static 5-on-5 half-court set for the showcase view: the offense spaced around the away
    /// (+z) key, each defender a step toward the basket from their man. Floor positions (y = 0),
    /// kept inside the court bounds. Pure, so placement is unit-testable.
    /// </summary>
    public static class PlayerPlacement
    {
        public const int TeamSize = 5;

        // Offense attacks the +z basket: PG / right wing / left wing / right block / left block.
        public static Vector3[] Offense() => new[]
        {
            new Vector3(0f, 0f, 22f),
            new Vector3(15f, 0f, 28f),
            new Vector3(-15f, 0f, 28f),
            new Vector3(6f, 0f, 36f),
            new Vector3(-6f, 0f, 36f)
        };

        // Defenders sit between each offensive player and the basket.
        public static Vector3[] Defense() => new[]
        {
            new Vector3(0f, 0f, 26f),
            new Vector3(13f, 0f, 31f),
            new Vector3(-13f, 0f, 31f),
            new Vector3(5f, 0f, 38f),
            new Vector3(-5f, 0f, 38f)
        };

        /// <summary>True if a floor position is inside the playing surface.</summary>
        public static bool InBounds(Vector3 p) =>
            Mathf.Abs(p.x) <= SimConstants.CourtHalfWidth &&
            Mathf.Abs(p.z) <= SimConstants.CourtHalfLength;
    }
}
