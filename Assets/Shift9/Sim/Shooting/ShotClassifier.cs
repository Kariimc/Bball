using Shift9.Sim.Core;
using UnityEngine;

namespace Shift9.Sim.Shooting
{
    public enum ShotZone : byte { AtRim, Close, MidRange, ThreePoint, FreeThrow }

    public readonly struct ShotClassification
    {
        public readonly ShotZone Zone;
        public readonly float Distance; // floor distance from the rim, in feet
        public ShotClassification(ShotZone zone, float distance) { Zone = zone; Distance = distance; }
    }

    /// <summary>
    /// Turns a shooter's floor position into a shot zone. Three-point detection defers to the
    /// real court line (CourtGeometry); everything inside is bucketed by distance to the rim.
    /// </summary>
    public static class ShotClassifier
    {
        public const float AtRimRadius = 4f;   // dunks/layups
        public const float CloseRadius = 10f;  // floaters / short shots

        public static ShotClassification Classify(Vector3 position, bool homeBasket)
        {
            Vector3 hoop = homeBasket ? SimConstants.HoopHome : SimConstants.HoopAway;
            float dx = position.x - hoop.x;
            float dz = position.z - hoop.z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);

            ShotZone zone;
            if (CourtGeometry.IsBeyondThreePointArc(position, homeBasket)) zone = ShotZone.ThreePoint;
            else if (distance <= AtRimRadius) zone = ShotZone.AtRim;
            else if (distance <= CloseRadius) zone = ShotZone.Close;
            else zone = ShotZone.MidRange;

            return new ShotClassification(zone, distance);
        }
    }
}
