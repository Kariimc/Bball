namespace Shift9.Sim.Stats
{
    /// <summary>One player's accumulated game stats.</summary>
    public sealed class StatLine
    {
        public int Points;
        public int FieldGoalsMade, FieldGoalsAttempted;
        public int ThreesMade, ThreesAttempted;
        public int FreeThrowsMade, FreeThrowsAttempted;
        public int Rebounds, Assists, Steals, Blocks, Turnovers, Fouls;
    }

    /// <summary>
    /// Per-player box score for both teams (5 a side). Indexed by team + roster slot 0..4, matching
    /// the rosters a <c>GameSim</c> runs with. Mutated live as the game records events.
    /// </summary>
    public sealed class BoxScore
    {
        public const int TeamSize = 5;

        private readonly StatLine[] _home = New();
        private readonly StatLine[] _away = New();

        public StatLine Line(bool home, int playerIndex) => (home ? _home : _away)[playerIndex];

        public int TeamPoints(bool home)
        {
            StatLine[] team = home ? _home : _away;
            int total = 0;
            for (int i = 0; i < team.Length; i++) total += team[i].Points;
            return total;
        }

        private static StatLine[] New()
        {
            var lines = new StatLine[TeamSize];
            for (int i = 0; i < lines.Length; i++) lines[i] = new StatLine();
            return lines;
        }
    }
}
