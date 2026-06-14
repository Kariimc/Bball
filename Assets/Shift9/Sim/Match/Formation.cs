using UnityEngine;

namespace Shift9.Sim.Match
{
    /// <summary>
    /// Canonical half-court 5-on-5 set (floor positions, y = 0), offense attacking the +z basket:
    /// a spaced look — point guard + two wings beyond the arc, two bigs on the blocks — so shots
    /// range from threes to inside finishes. Lives in the sim so the possession simulation and the
    /// presentation blockout share one source of truth.
    /// </summary>
    public static class Formation
    {
        public const int TeamSize = 5;

        // PG (top three) / right wing (three) / left wing (three) / right block / left block.
        public static Vector3[] Offense() => new[]
        {
            new Vector3(0f, 0f, 17f),
            new Vector3(17f, 0f, 24f),
            new Vector3(-17f, 0f, 24f),
            new Vector3(8f, 0f, 36f),
            new Vector3(-8f, 0f, 36f)
        };

        // Each defender a step goalside of their man.
        public static Vector3[] Defense() => new[]
        {
            new Vector3(0f, 0f, 21f),
            new Vector3(15f, 0f, 27f),
            new Vector3(-15f, 0f, 27f),
            new Vector3(7f, 0f, 38f),
            new Vector3(-7f, 0f, 38f)
        };
    }
}
