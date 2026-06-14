using UnityEngine;

namespace Shift9.Sim.Match
{
    /// <summary>
    /// Canonical half-court 5-on-5 set positions (floor, y = 0), offense attacking the +z basket.
    /// Lives in the sim so both the possession simulation and the presentation blockout share one
    /// source of truth.
    /// </summary>
    public static class Formation
    {
        public const int TeamSize = 5;

        // PG / right wing / left wing / right block / left block.
        public static Vector3[] Offense() => new[]
        {
            new Vector3(0f, 0f, 22f),
            new Vector3(15f, 0f, 28f),
            new Vector3(-15f, 0f, 28f),
            new Vector3(6f, 0f, 36f),
            new Vector3(-6f, 0f, 36f)
        };

        // Each defender a step goalside of their man.
        public static Vector3[] Defense() => new[]
        {
            new Vector3(0f, 0f, 26f),
            new Vector3(13f, 0f, 31f),
            new Vector3(-13f, 0f, 31f),
            new Vector3(5f, 0f, 38f),
            new Vector3(-5f, 0f, 38f)
        };
    }
}
