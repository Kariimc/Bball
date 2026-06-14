using System.Collections.Generic;
using Shift9.Sim.Core;
using UnityEngine;

namespace Shift9.Presentation.Court
{
    /// <summary>
    /// Pure court geometry: every position and outline the court builder needs, derived from
    /// <see cref="SimConstants"/> and the resolved arena spec (docs/arena_scene_layout.md).
    /// Kept free of scene objects so it is unit-testable.
    ///
    /// Coordinate frame: x = width (±25), z = length (±47), y = up. Home end is -z.
    /// </summary>
    public static class CourtMetrics
    {
        // Resolved from references / regulation (see arena spec). Regulation values are flagged
        // as such; they are not measured from the photographs.
        public const float BackboardOffsetFromBaseline = 4f;   // backboard overhang (confirmed by reference)
        public const float DefaultStanchionSetback = 6f;       // base 5-8 ft behind baseline (reference)
        public const float CenterCircleRadius = 6f;            // regulation
        public const float FreeThrowCircleRadius = 6f;         // regulation
        public const float BackboardWidth = 6f;                // regulation 72"
        public const float BackboardHeight = 3.5f;             // regulation 42"
        public const float BackboardBottomY = 9.5f;            // regulation lower edge
        public const float ShooterSquareWidth = 2f;            // regulation 24"
        public const float ShooterSquareHeight = 1.5f;         // regulation 18", bottom at rim height

        public static float Sign(bool home) => home ? -1f : 1f; // home end sits on -z

        public static Vector3 RimCenter(bool home) => home ? SimConstants.HoopHome : SimConstants.HoopAway;

        public static float BaselineZ(bool home) => Sign(home) * SimConstants.CourtHalfLength;

        public static float BackboardZ(bool home) =>
            Sign(home) * (SimConstants.CourtHalfLength - BackboardOffsetFromBaseline);

        public static Vector3 BackboardCenter(bool home) =>
            new Vector3(0f, BackboardBottomY + BackboardHeight * 0.5f, BackboardZ(home));

        public static float StanchionZ(bool home, float setback) =>
            Sign(home) * (SimConstants.CourtHalfLength + setback);

        /// <summary>Center of the painted key (lane) on the floor.</summary>
        public static Vector3 PaintCenter(bool home) =>
            new Vector3(0f, 0f, Sign(home) * (SimConstants.CourtHalfLength - SimConstants.PaintDepth * 0.5f));

        /// <summary>Z of the free-throw line for an end.</summary>
        public static float FreeThrowLineZ(bool home) =>
            BaselineZ(home) - Sign(home) * SimConstants.PaintDepth;

        // ---- Outlines (y = 0; the builder lifts them slightly to avoid z-fighting) ----

        public static List<Vector3> BoundaryLoop()
        {
            float x = SimConstants.CourtHalfWidth, z = SimConstants.CourtHalfLength;
            return new List<Vector3>
            {
                new Vector3(-x, 0f, -z), new Vector3(x, 0f, -z),
                new Vector3(x, 0f, z),   new Vector3(-x, 0f, z)
            };
        }

        public static List<Vector3> PaintLoop(bool home)
        {
            float x = SimConstants.PaintHalfWidth;
            float zb = BaselineZ(home);
            float zf = FreeThrowLineZ(home);
            return new List<Vector3>
            {
                new Vector3(-x, 0f, zb), new Vector3(x, 0f, zb),
                new Vector3(x, 0f, zf),  new Vector3(-x, 0f, zf)
            };
        }

        public static List<Vector3> Circle(Vector3 center, float radius, int segments)
        {
            var pts = new List<Vector3>(segments);
            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                pts.Add(center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            return pts;
        }

        /// <summary>The curved part of the three-point line for an end (corners handled separately).</summary>
        public static List<Vector3> ThreePointArc(bool home, int segments)
        {
            Vector3 rim = RimCenter(home);
            float r = SimConstants.ArcRadius;
            float dir = -Sign(home); // arc bulges toward court interior
            float thetaMax = Mathf.Asin(SimConstants.CornerThreeX / r);

            var pts = new List<Vector3>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float theta = Mathf.Lerp(-thetaMax, thetaMax, t);
                float xx = r * Mathf.Sin(theta);
                float zz = rim.z + dir * r * Mathf.Cos(theta);
                pts.Add(new Vector3(xx, 0f, zz));
            }
            return pts;
        }

        /// <summary>The two straight corner segments of the three-point line for an end.</summary>
        public static (Vector3 a, Vector3 b)[] CornerThreeLines(bool home)
        {
            Vector3 end = ThreePointArc(home, 1)[0]; // arc endpoint x = -CornerThreeX
            float zArcEnd = end.z;
            float zb = BaselineZ(home);
            float x = SimConstants.CornerThreeX;
            return new[]
            {
                (new Vector3(-x, 0f, zb), new Vector3(-x, 0f, zArcEnd)),
                (new Vector3(x, 0f, zb),  new Vector3(x, 0f, zArcEnd))
            };
        }
    }
}
