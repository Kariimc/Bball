using Shift9.Sim.Core;
using Shift9.Sim.Players;
using UnityEngine;

namespace Shift9.Sim.Match
{
    /// <summary>
    /// Deterministically generates a varied set of player attribute profiles when no imported
    /// roster is supplied — mostly role players with the occasional star whose ratings clear the
    /// signature-move thresholds. Used so a self-contained game still has differentiated players.
    /// </summary>
    public static class RandomRoster
    {
        public static AttributeProfile[] Generate(ref DeterministicRng rng, int count)
        {
            var roster = new AttributeProfile[count];
            for (int i = 0; i < count; i++)
            {
                bool star = rng.NextFloat() < 0.18f;
                int baseRating = star ? rng.Range(82, 95) : rng.Range(60, 82);
                int spread = star ? 10 : 16;
                roster[i] = new AttributeProfile
                {
                    FreeThrow = Rate(ref rng, baseRating, spread),
                    ShotClose = Rate(ref rng, baseRating, spread),
                    MidRange = Rate(ref rng, baseRating, spread),
                    ThreePoint = Rate(ref rng, baseRating, spread),
                    DunkRating = Rate(ref rng, baseRating, spread),
                    VerticalLeap = Rate(ref rng, baseRating, spread),
                    Speed = Rate(ref rng, baseRating, spread),
                    PassingAccuracy = Rate(ref rng, baseRating, spread),
                    PhysicalStrength = Rate(ref rng, baseRating, spread),
                    Hustle = Rate(ref rng, baseRating, spread),
                    PerimeterDefense = Rate(ref rng, baseRating, spread),
                    InteriorDefense = Rate(ref rng, baseRating, spread),
                    DefensiveAwareness = Rate(ref rng, baseRating, spread),
                    HandleControl = Rate(ref rng, baseRating, spread)
                };
            }
            return roster;
        }

        private static byte Rate(ref DeterministicRng rng, int baseRating, int spread)
        {
            int v = baseRating + rng.Range(-spread, spread + 1);
            return (byte)Mathf.Clamp(v, 30, 99);
        }
    }
}
