namespace Shift9.Sim.Rules
{
    public enum Team : byte { Home, Away }

    /// <summary>Tracks which team has the ball and flips it on makes, turnovers, and violations.</summary>
    public sealed class Possession
    {
        public Team Offense { get; private set; }
        public bool HomeOnOffense => Offense == Team.Home;

        public Possession(Team start = Team.Home) => Offense = start;

        public void Set(Team team) => Offense = team;

        public Team Flip()
        {
            Offense = Offense == Team.Home ? Team.Away : Team.Home;
            return Offense;
        }
    }
}
