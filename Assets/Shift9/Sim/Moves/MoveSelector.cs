using Shift9.Sim.Core;
using Shift9.Sim.Players;
using UnityEngine;

namespace Shift9.Sim.Moves
{
    /// <summary>
    /// Deterministically picks which dribble move, finish, post move, or defensive play a player
    /// uses, gating the signature/special variants behind <see cref="SignatureThresholds"/>. A
    /// 60-handle guard never breaks ankles; a 95 might. Pure given the shared dice.
    /// </summary>
    public static class MoveSelector
    {
        private static readonly DribbleMove[] BasicDribbles =
            { DribbleMove.Crossover, DribbleMove.Hesitation, DribbleMove.BehindBack, DribbleMove.BetweenLegs };

        public static DribbleMove SelectDribble(in AttributeProfile a, bool pressured, ref DeterministicRng rng)
        {
            // Elite handles can break a defender down — only available above the gate.
            if (a.HandleControl >= SignatureThresholds.EliteHandle)
            {
                float over = (a.HandleControl - 85) / 99f; // ~0.05..0.14
                if (rng.Chance(over * 1.3f)) return DribbleMove.SignatureCrossover;
            }
            if (!pressured && rng.NextFloat() > 0.5f) return DribbleMove.None;
            return BasicDribbles[rng.Range(0, BasicDribbles.Length)];
        }

        public static FinishMove SelectFinish(in AttributeProfile a, bool atRim, bool contested, ref DeterministicRng rng)
        {
            if (!atRim)
                return rng.Chance(a.ShotClose / 99f * 0.5f) ? FinishMove.Floater : FinishMove.Layup;

            float dunk = (a.DunkRating + a.VerticalLeap) / 2f / 99f;
            if (!contested && a.DunkRating >= SignatureThresholds.EliteFinisher &&
                a.VerticalLeap >= SignatureThresholds.EliteLeap && rng.Chance(0.5f))
                return FinishMove.SignatureDunk;
            if (rng.Chance(dunk * (contested ? 0.4f : 1f))) return FinishMove.Dunk;
            return FinishMove.Layup;
        }

        public static PostMove SelectPost(in AttributeProfile a, ref DeterministicRng rng)
        {
            if (a.ShotClose < SignatureThresholds.PostScorer || a.PhysicalStrength < SignatureThresholds.PostStrength)
                return PostMove.None;
            switch (rng.Range(0, 3))
            {
                case 0: return PostMove.DropStep;
                case 1: return PostMove.SpinMove;
                default: return PostMove.Fadeaway;
            }
        }

        public static DefenseMove SelectBlock(in AttributeProfile defender, ref DeterministicRng rng)
        {
            if (defender.InteriorDefense >= SignatureThresholds.EliteRimProtect &&
                defender.VerticalLeap >= SignatureThresholds.EliteLeap)
            {
                float skill = (defender.InteriorDefense + defender.VerticalLeap) / 2f / 99f;
                if (rng.Chance((skill - 0.8f) * 0.7f)) return DefenseMove.SignatureBlock;
            }
            return DefenseMove.Contest;
        }
    }
}
