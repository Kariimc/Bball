namespace Shift9.Sim.Rules
{
    /// <summary>Something the clock reported this tick.</summary>
    public enum ClockEvent : byte { None, ShotClockViolation, QuarterEnded, GameEnded }

    /// <summary>
    /// Deterministic game and shot clocks. Advanced by the fixed simulation step; reports the
    /// significant event (if any) each tick. Defaults to NBA timing (four 12-minute quarters,
    /// 24-second shot clock, 14-second reset on an offensive rebound).
    /// </summary>
    public sealed class GameClock
    {
        public float QuarterLength { get; }
        public int NumQuarters { get; }
        public float ShotClockFull { get; }
        public float ShotClockResetShort { get; }

        public int Quarter { get; private set; }
        public float GameTimeRemaining { get; private set; }
        public float ShotClockRemaining { get; private set; }
        public bool GameOver { get; private set; }

        public GameClock(float quarterLength = 720f, int numQuarters = 4,
            float shotClockFull = 24f, float shotClockResetShort = 14f)
        {
            QuarterLength = quarterLength;
            NumQuarters = numQuarters;
            ShotClockFull = shotClockFull;
            ShotClockResetShort = shotClockResetShort;

            Quarter = 1;
            GameTimeRemaining = quarterLength;
            ShotClockRemaining = shotClockFull;
            GameOver = false;
        }

        /// <summary>
        /// Advances both clocks by <paramref name="dt"/>. End-of-period takes precedence over a
        /// shot-clock violation when both would trip on the same tick. No-op once the game is over.
        /// </summary>
        public ClockEvent Tick(float dt)
        {
            if (GameOver) return ClockEvent.None;

            GameTimeRemaining -= dt;
            ShotClockRemaining -= dt;

            if (GameTimeRemaining <= 0f)
            {
                GameTimeRemaining = 0f;
                if (Quarter >= NumQuarters) { GameOver = true; return ClockEvent.GameEnded; }
                return ClockEvent.QuarterEnded;
            }

            if (ShotClockRemaining <= 0f)
            {
                ShotClockRemaining = 0f;
                return ClockEvent.ShotClockViolation;
            }

            return ClockEvent.None;
        }

        /// <summary>
        /// Resets the shot clock — full 24 after a change of possession, or the short reset after
        /// an offensive rebound. Never set higher than the time left in the period.
        /// </summary>
        public void ResetShotClock(bool full)
        {
            float reset = full ? ShotClockFull : ShotClockResetShort;
            ShotClockRemaining = reset < GameTimeRemaining ? reset : GameTimeRemaining;
        }

        /// <summary>Starts the next period. Returns false if the game is already over / on its last period.</summary>
        public bool AdvanceQuarter()
        {
            if (GameOver || Quarter >= NumQuarters) return false;
            Quarter++;
            GameTimeRemaining = QuarterLength;
            ShotClockRemaining = ShotClockFull;
            return true;
        }
    }
}
