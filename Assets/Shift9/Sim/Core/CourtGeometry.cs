using UnityEngine;

namespace Shift9.Sim.Core
{
    /// <summary>
    /// Pure court-position queries: out of bounds, inside the paint, behind the three-point line.
    /// Stateless and deterministic. Coordinate convention: x = width (+/-25), z = length (+/-47),
    /// y = height. <paramref name="homeBasket"/> selects which end to evaluate against.
    ///
    /// Cleaned up from the Shift9 draft: removed the unused 'sideSign' dead code and added the
    /// missing baseline bound so points behind the baseline no longer falsely report "in paint".
    /// </summary>
    public static class CourtGeometry
    {
        public static bool IsOutOfBounds(Vector3 p) =>
            Mathf.Abs(p.x) > SimConstants.CourtHalfWidth ||
            Mathf.Abs(p.z) > SimConstants.CourtHalfLength;

        public static bool IsInPaint(Vector3 p, bool homeBasket)
        {
            if (Mathf.Abs(p.x) > SimConstants.PaintHalfWidth) return false;
            if (homeBasket)
            {
                float baseline = -SimConstants.CourtHalfLength;            // -47
                return p.z >= baseline && p.z <= baseline + SimConstants.PaintDepth; // [-47, -28]
            }
            else
            {
                float baseline = SimConstants.CourtHalfLength;            // +47
                return p.z <= baseline && p.z >= baseline - SimConstants.PaintDepth;  // [28, 47]
            }
        }

        public static bool IsBeyondThreePointArc(Vector3 p, bool homeBasket)
        {
            // How far the point is from its own baseline, along the length of the court.
            float distFromBaseline = SimConstants.CourtHalfLength - Mathf.Abs(p.z);

            // Near the baseline the three-point line is a straight vertical at +/-22 ft.
            if (distFromBaseline <= SimConstants.CornerStraightLength)
                return Mathf.Abs(p.x) >= SimConstants.CornerThreeX;

            // Elsewhere it is an arc measured from the rim (height ignored — it's a floor distance).
            Vector3 hoop = homeBasket ? SimConstants.HoopHome : SimConstants.HoopAway;
            float dx = p.x - hoop.x;
            float dz = p.z - hoop.z;
            return Mathf.Sqrt(dx * dx + dz * dz) >= SimConstants.ArcRadius;
        }
    }
}
