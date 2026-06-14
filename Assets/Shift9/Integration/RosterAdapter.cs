using System.Collections.Generic;
using Shift9.Customization.Mapping;
using Shift9.Sim.Players;
using Shift9.Sim.Roster;

namespace Shift9.Integration
{
    /// <summary>
    /// Converts validated, imported roster data (from the Customization engine) into the
    /// simulation's roster types, so an imported team's ratings actually drive gameplay.
    ///
    /// This is the ONE place the two systems meet: Customization knows nothing about the sim,
    /// the sim knows nothing about imports, and this connector depends on both. Imported ratings
    /// arrive already clamped to 0..99 by the validator, so this is a pure structural copy.
    /// </summary>
    public static class RosterAdapter
    {
        public static AttributeProfile ToAttributeProfile(in RuntimeAttributes a) => new AttributeProfile
        {
            FreeThrow = a.FreeThrow,
            ShotClose = a.ShotClose,
            MidRange = a.MidRange,
            ThreePoint = a.ThreePoint,
            DunkRating = a.DunkRating,
            VerticalLeap = a.VerticalLeap,
            Speed = a.Speed,
            PassingAccuracy = a.PassingAccuracy,
            PhysicalStrength = a.PhysicalStrength,
            Hustle = a.Hustle,
            PerimeterDefense = a.PerimeterDefense,
            InteriorDefense = a.InteriorDefense,
            DefensiveAwareness = a.DefensiveAwareness,
            HandleControl = a.HandleControl
        };

        public static SimPlayer ToSimPlayer(RuntimePlayer p) => new SimPlayer
        {
            Id = p.Id,
            Name = p.Name,
            Number = p.Number,
            Attributes = ToAttributeProfile(p.Attributes),
            Dynamics = PlayerDynamics.Default
        };

        public static SimTeam ToSimTeam(RuntimeTeam t)
        {
            var team = new SimTeam { Id = t.Id, Name = t.Name };
            if (t.Players != null)
                foreach (var p in t.Players)
                    team.Players.Add(ToSimPlayer(p));
            return team;
        }

        /// <summary>Maps every team in an imported league into sim teams, preserving order.</summary>
        public static List<SimTeam> ToSimTeams(RuntimeLeague league)
        {
            var teams = new List<SimTeam>();
            if (league?.Teams != null)
                foreach (var t in league.Teams)
                    teams.Add(ToSimTeam(t));
            return teams;
        }
    }
}
