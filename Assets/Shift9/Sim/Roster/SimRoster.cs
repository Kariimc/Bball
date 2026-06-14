using System.Collections.Generic;
using Shift9.Sim.Players;

namespace Shift9.Sim.Roster
{
    /// <summary>
    /// A player as the simulation uses them: identity plus the fixed ratings and the live
    /// in-game state. Visuals (jersey/sneaker images, team colors) live in the presentation
    /// layer and are intentionally absent here.
    /// </summary>
    public sealed class SimPlayer
    {
        public string Id;
        public string Name;
        public int Number;
        public AttributeProfile Attributes;
        public PlayerDynamics Dynamics = PlayerDynamics.Default;
    }

    /// <summary>A team the simulation can play with: identity and its roster of players.</summary>
    public sealed class SimTeam
    {
        public string Id;
        public string Name;
        public List<SimPlayer> Players = new List<SimPlayer>();
    }
}
