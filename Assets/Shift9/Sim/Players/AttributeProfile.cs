namespace Shift9.Sim.Players
{
    /// <summary>
    /// A player's fixed shooting/skill ratings on the standard 0..99 scale. A plain value struct
    /// (no references) so rosters can live in tight arrays. Imported rosters arrive here already
    /// clamped to 0..99 by the Customization layer's validator.
    /// </summary>
    public struct AttributeProfile
    {
        public byte FreeThrow;
        public byte ShotClose;      // layups / shots around the basket
        public byte MidRange;
        public byte ThreePoint;
        public byte DunkRating;
        public byte VerticalLeap;
        public byte Speed;
        public byte PassingAccuracy;
        public byte PhysicalStrength;
        public byte Hustle;
        public byte PerimeterDefense;
        public byte InteriorDefense;
        public byte DefensiveAwareness;
        public byte HandleControl;

        /// <summary>Test/util helper: every rating set to the same value.</summary>
        public static AttributeProfile Uniform(byte v) => new AttributeProfile
        {
            FreeThrow = v, ShotClose = v, MidRange = v, ThreePoint = v, DunkRating = v,
            VerticalLeap = v, Speed = v, PassingAccuracy = v, PhysicalStrength = v, Hustle = v,
            PerimeterDefense = v, InteriorDefense = v, DefensiveAwareness = v, HandleControl = v
        };

        /// <summary>Which rating governs a given shot zone.</summary>
        public byte ShootingRating(Shooting.ShotZone zone)
        {
            switch (zone)
            {
                case Shooting.ShotZone.FreeThrow:  return FreeThrow;
                case Shooting.ShotZone.AtRim:      return ShotClose;
                case Shooting.ShotZone.Close:      return ShotClose;
                case Shooting.ShotZone.MidRange:   return MidRange;
                case Shooting.ShotZone.ThreePoint: return ThreePoint;
                default:                           return MidRange;
            }
        }
    }

    /// <summary>
    /// A player's moment-to-moment state that nudges shooting up or down. Separate from the fixed
    /// ratings so it can change every possession without touching the roster data.
    /// </summary>
    public struct PlayerDynamics
    {
        public float Stamina;   // 1 = fresh, 0 = gassed
        public float HotHand;   // streak multiplier, ~0.9..1.15 (1 = neutral)
        public float Fatigue;   // accumulates over a game; feeds stamina drain elsewhere

        public static PlayerDynamics Default => new PlayerDynamics { Stamina = 1f, HotHand = 1f, Fatigue = 0f };
    }
}
