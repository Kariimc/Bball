using Shift9.Sim.Core;
using UnityEngine;

namespace Shift9.Sim.Defense
{
    /// <summary>Tunables for how defenders smother a shot.</summary>
    public struct OpennessConfig
    {
        public float ContestScale; // distance (ft) at which raw proximity contest is 0.5
        public float LaneFloor;    // a defender beside (not between) the shooter still contests this fraction
        public float SkillMin, SkillMax; // contest multiplier across rating 0..99

        public static OpennessConfig Default => new OpennessConfig
        {
            ContestScale = 4f, LaneFloor = 0.5f, SkillMin = 0.7f, SkillMax = 1.15f
        };
    }

    /// <summary>
    /// Computes how open a shooter is, 0 (smothered) .. 1 (wide open) — the value that feeds the
    /// shot model's contest term. Each defender's contest uses an inverse-square closeout curve on
    /// distance, weighted by how directly they sit in the shooter-to-rim lane and by their contest
    /// rating. The single strongest contester sets the openness (a help defender can't be double
    /// counted into an impossible shot).
    /// </summary>
    public static class OpennessCalculator
    {
        public static float Compute(Vector3 shooter, bool homeBasket, DefenderState[] defenders)
            => Compute(shooter, homeBasket, defenders, OpennessConfig.Default);

        public static float Compute(Vector3 shooter, bool homeBasket, DefenderState[] defenders, in OpennessConfig cfg)
        {
            if (defenders == null || defenders.Length == 0) return 1f;

            Vector3 hoop = homeBasket ? SimConstants.HoopHome : SimConstants.HoopAway;
            Vector3 toHoop = hoop - shooter; toHoop.y = 0f;
            Vector3 dirToHoop = toHoop.sqrMagnitude > 1e-6f ? toHoop.normalized : Vector3.forward;

            float strongestContest = 0f;
            for (int i = 0; i < defenders.Length; i++)
            {
                Vector3 toDef = defenders[i].Position - shooter; toDef.y = 0f;
                float dist = toDef.magnitude;
                Vector3 dirToDef = dist > 1e-4f ? toDef / dist : dirToHoop;

                // 1 = defender sits directly between shooter and rim; 0 = off to the side/behind.
                float alignment = Mathf.Clamp01(Vector3.Dot(dirToHoop, dirToDef));

                // Inverse-square closeout: full pressure point-blank, fading with distance.
                float ratio = dist / cfg.ContestScale;
                float proximity = 1f / (1f + ratio * ratio);

                float skill = Mathf.Lerp(cfg.SkillMin, cfg.SkillMax, defenders[i].ContestRating / 99f);
                float contest = Mathf.Clamp01(proximity * Mathf.Lerp(cfg.LaneFloor, 1f, alignment) * skill);

                if (contest > strongestContest) strongestContest = contest;
            }

            return Mathf.Clamp01(1f - strongestContest);
        }
    }
}
