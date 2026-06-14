using Shift9.Sim.Shooting;

namespace Shift9.Sim.Rules
{
    /// <summary>Running score. Points are derived from the shot zone (free throw 1, three 3, else 2).</summary>
    public sealed class Scoreboard
    {
        public int HomeScore { get; private set; }
        public int AwayScore { get; private set; }

        public void AddBasket(bool home, ShotZone zone)
        {
            int points = PointsFor(zone);
            if (home) HomeScore += points;
            else AwayScore += points;
        }

        public static int PointsFor(ShotZone zone)
        {
            switch (zone)
            {
                case ShotZone.FreeThrow:  return 1;
                case ShotZone.ThreePoint: return 3;
                default:                  return 2;
            }
        }
    }
}
