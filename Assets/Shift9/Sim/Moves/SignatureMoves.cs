namespace Shift9.Sim.Moves
{
    public enum DribbleMove : byte { None, Crossover, Hesitation, BehindBack, BetweenLegs, SignatureCrossover }
    public enum FinishMove : byte { JumpShot, Layup, Floater, Dunk, SignatureDunk }
    public enum PostMove : byte { None, DropStep, SpinMove, Fadeaway }
    public enum DefenseMove : byte { Contest, SignatureBlock }

    /// <summary>
    /// Rating gates above which a player unlocks special / signature animations — the "outlier"
    /// abilities (an ankle-breaking crossover, an elite rim-protecting block, a poster dunk).
    /// </summary>
    public static class SignatureThresholds
    {
        public const byte EliteHandle = 90;       // signature crossover (defender stumbles)
        public const byte EliteFinisher = 92;     // signature dunk
        public const byte EliteRimProtect = 90;   // signature shot block
        public const byte EliteLeap = 80;         // paired with rim protection / finishing
        public const byte PostScorer = 85;        // post moves
        public const byte PostStrength = 80;
    }
}
