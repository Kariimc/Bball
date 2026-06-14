using UnityEngine;

namespace Shift9.Sim.Players
{
    /// <summary>
    /// Box-score-style inputs used to derive a player's ratings. Per-game numbers and percentages
    /// (0..1 for the shooting splits), plus physicals. These are the "stats" half of the
    /// stats → attributes equation.
    /// </summary>
    public struct PlayerStats
    {
        public float Points;
        public float FieldGoalPct;   // 0..1
        public float ThreePtPct;     // 0..1
        public float ThreePtAtt;     // per game
        public float FreeThrowPct;   // 0..1
        public float Assists;
        public float Turnovers;
        public float Rebounds;
        public float OffRebounds;
        public float Blocks;
        public float Steals;
        public float HeightInches;
        public float WeightLbs;
    }

    /// <summary>
    /// Converts box-score stats into a 0..99 <see cref="AttributeProfile"/> via weighted equations.
    /// Each rating is a weighted blend of normalized stats (every input mapped to 0..1 over a
    /// sensible NBA range) scaled to 0..99 and clamped. Pure and deterministic.
    /// </summary>
    public static class AttributeFormula
    {
        public static AttributeProfile FromStats(in PlayerStats s)
        {
            float astTo = s.Assists / Mathf.Max(0.5f, s.Turnovers);
            float fg = N(s.FieldGoalPct, 0.40f, 0.55f);
            float pts = N(s.Points, 5f, 30f);
            float height = N(s.HeightInches, 72f, 87f);
            float blocks = N(s.Blocks, 0f, 3f);
            float steals = N(s.Steals, 0f, 2.5f);
            float reb = N(s.Rebounds, 2f, 14f);
            float oreb = N(s.OffRebounds, 0f, 4f);
            float ast = N(s.Assists, 1f, 11f);
            float lowTo = 1f - N(s.Turnovers, 0.5f, 4f);

            return new AttributeProfile
            {
                FreeThrow = R(N(s.FreeThrowPct, 0.40f, 0.95f)),
                ThreePoint = R(0.7f * N(s.ThreePtPct, 0.25f, 0.45f) + 0.3f * N(s.ThreePtAtt, 0f, 9f)),
                MidRange = R(0.6f * N(s.FieldGoalPct, 0.40f, 0.55f) + 0.4f * pts),
                ShotClose = R(0.5f * N(s.FieldGoalPct, 0.45f, 0.65f) + 0.3f * pts + 0.2f * height),
                DunkRating = R(0.5f * height + 0.3f * blocks + 0.2f * pts),
                VerticalLeap = R(0.5f * blocks + 0.3f * oreb + 0.2f * height),
                Speed = R(0.6f * steals + 0.4f * (1f - height)),
                PassingAccuracy = R(0.6f * ast + 0.4f * N(astTo, 0.8f, 3.5f)),
                PhysicalStrength = R(0.5f * N(s.WeightLbs, 170f, 280f) + 0.3f * reb + 0.2f * height),
                Hustle = R(0.4f * oreb + 0.3f * steals + 0.3f * blocks),
                PerimeterDefense = R(0.7f * steals + 0.3f * (1f - height)),
                InteriorDefense = R(0.6f * blocks + 0.2f * reb + 0.2f * height),
                DefensiveAwareness = R(0.5f * steals + 0.5f * blocks),
                HandleControl = R(0.5f * ast + 0.3f * lowTo + 0.2f * pts)
            };
        }

        // Normalize a stat to 0..1 across [lo, hi].
        private static float N(float value, float lo, float hi)
        {
            if (hi <= lo) return 0f;
            return Mathf.Clamp01((value - lo) / (hi - lo));
        }

        // Scale a 0..1 blend to a 0..99 rating.
        private static byte R(float zeroToOne) =>
            (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(zeroToOne) * 99f), 0, 99);
    }
}
