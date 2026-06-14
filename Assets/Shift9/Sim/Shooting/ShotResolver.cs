using Shift9.Sim.Core;
using Shift9.Sim.Players;
using UnityEngine;

namespace Shift9.Sim.Shooting
{
    /// <summary>Everything needed to resolve one shot attempt.</summary>
    public struct ShotContext
    {
        public Vector3 Position;          // shooter floor position
        public bool HomeBasket;           // which rim they're attacking
        public AttributeProfile Attributes;
        public PlayerDynamics Dynamics;
        public float ReleaseErrorSeconds; // how far off "perfect" the release timing was (0 = perfect)
        public float Openness;            // 0 = smothered, 1 = wide open
        public bool IsFreeThrow;          // free throws ignore timing/contest
    }

    public readonly struct ShotResult
    {
        public readonly ShotZone Zone;
        public readonly float Probability; // final make chance actually used
        public readonly bool IsGreen;      // perfectly timed release
        public readonly bool Made;
        public ShotResult(ShotZone zone, float probability, bool isGreen, bool made)
        {
            Zone = zone; Probability = probability; IsGreen = isGreen; Made = made;
        }
    }

    /// <summary>Tunable knobs for the make-probability model (the per-zone bases are fixed in code).</summary>
    public struct ShotModelConfig
    {
        public float TimingSigmaMin, TimingSigmaMax; // release forgiveness window (worst..best shooter), seconds
        public float TimingFloor;                    // worst-timing never drops the multiplier below this
        public float GreenEpsilon;                   // |error| under this = perfect "green" release
        public float ContestFloor;                   // smothered shot keeps this fraction of base
        public float FatigueFloor;                   // fully gassed keeps this fraction of base
        public float HotHandMin, HotHandMax;         // clamp on the streak multiplier
        public float MinProbability, MaxProbability; // final clamp

        public static ShotModelConfig Default => new ShotModelConfig
        {
            TimingSigmaMin = 0.05f, TimingSigmaMax = 0.10f, TimingFloor = 0.25f, GreenEpsilon = 0.025f,
            ContestFloor = 0.35f, FatigueFloor = 0.85f, HotHandMin = 0.90f, HotHandMax = 1.15f,
            MinProbability = 0.02f, MaxProbability = 0.99f
        };
    }

    /// <summary>
    /// Resolves a shot into make/miss. The final make chance is:
    ///
    ///   P = base(zone, rating) * timing * contest * fatigue * hotHand   (then clamped)
    ///
    /// * base    : the open, well-timed make rate for the zone, scaled by the shooter's rating.
    /// * timing  : a bell curve on release error — perfect = full, worse = less (down to a floor).
    /// * contest : how open the shot is — wide open = full, smothered = ContestFloor.
    /// * fatigue : tired legs shave a little off.
    /// * hotHand : a small streak nudge.
    ///
    /// The make/miss coin flip uses the shared deterministic dice so the same situation always
    /// resolves the same way — essential for replays and online play.
    /// </summary>
    public static class ShotResolver
    {
        public static ShotResult Resolve(in ShotContext ctx, ref DeterministicRng rng, in ShotModelConfig cfg)
        {
            ShotZone zone = ctx.IsFreeThrow
                ? ShotZone.FreeThrow
                : ShotClassifier.Classify(ctx.Position, ctx.HomeBasket).Zone;

            byte rating = ctx.Attributes.ShootingRating(zone);
            float baseP = ZoneBase(zone, rating);

            // Release timing (free throws are untimed -> full multiplier, never "green").
            bool isGreen = false;
            float timing = 1f;
            if (!ctx.IsFreeThrow)
            {
                float dt = ctx.ReleaseErrorSeconds;
                float sigma = Mathf.Lerp(cfg.TimingSigmaMin, cfg.TimingSigmaMax, rating / 99f);
                timing = Mathf.Max(cfg.TimingFloor, Mathf.Exp(-(dt * dt) / (2f * sigma * sigma)));
                isGreen = Mathf.Abs(dt) <= cfg.GreenEpsilon;
                if (isGreen) timing = 1f;
            }

            float openness = ctx.IsFreeThrow ? 1f : Mathf.Clamp01(ctx.Openness);
            float contest = Mathf.Lerp(cfg.ContestFloor, 1f, openness);
            float fatigue = Mathf.Lerp(cfg.FatigueFloor, 1f, Mathf.Clamp01(ctx.Dynamics.Stamina));
            float hot = Mathf.Clamp(ctx.Dynamics.HotHand, cfg.HotHandMin, cfg.HotHandMax);

            float p = Mathf.Clamp(baseP * timing * contest * fatigue * hot,
                                  cfg.MinProbability, cfg.MaxProbability);

            bool made = rng.Chance(p);
            return new ShotResult(zone, p, isGreen, made);
        }

        // Open, well-timed make rate for a zone, scaled linearly by rating (0..99).
        // Ranges are (rating 0 .. rating 99) and reflect roughly realistic open percentages.
        private static float ZoneBase(ShotZone zone, byte rating)
        {
            float t = rating / 99f;
            switch (zone)
            {
                case ShotZone.AtRim:      return Mathf.Lerp(0.50f, 0.78f, t);
                case ShotZone.Close:      return Mathf.Lerp(0.38f, 0.60f, t);
                case ShotZone.MidRange:   return Mathf.Lerp(0.33f, 0.52f, t);
                case ShotZone.ThreePoint: return Mathf.Lerp(0.28f, 0.46f, t);
                case ShotZone.FreeThrow:  return Mathf.Lerp(0.50f, 0.95f, t);
                default:                  return Mathf.Lerp(0.33f, 0.52f, t);
            }
        }
    }
}
