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
            Attributes = ResolveAttributes(p),
            Dynamics = PlayerDynamics.Default
        };

        /// <summary>
        /// A player's ratings: derived from box-score stats via <see cref="AttributeFormula"/> when
        /// stats are supplied, otherwise the explicit imported attributes.
        /// </summary>
        public static AttributeProfile ResolveAttributes(RuntimePlayer p)
        {
            if (p.Stats != null) return AttributeFormula.FromStats(ToPlayerStats(p.Stats));
            return ToAttributeProfile(p.Attributes);
        }

        public static PlayerStats ToPlayerStats(RuntimeStats s) => new PlayerStats
        {
            Points = s.Points, FieldGoalPct = s.FieldGoalPct, ThreePtPct = s.ThreePtPct,
            ThreePtAtt = s.ThreePtAtt, FreeThrowPct = s.FreeThrowPct, Assists = s.Assists,
            Turnovers = s.Turnovers, Rebounds = s.Rebounds, OffRebounds = s.OffRebounds,
            Blocks = s.Blocks, Steals = s.Steals, HeightInches = s.HeightInches, WeightLbs = s.WeightLbs
        };

        /// <summary>
        /// The five attribute profiles a game uses for a team — its first five players (stats-derived
        /// where stats were supplied). Short rosters are filled with a neutral 70 so a game can start.
        /// </summary>
        public static AttributeProfile[] StartingFive(RuntimeTeam team)
        {
            const int five = 5;
            var roster = new AttributeProfile[five];
            for (int i = 0; i < five; i++)
            {
                bool hasPlayer = team?.Players != null && i < team.Players.Count;
                roster[i] = hasPlayer ? ResolveAttributes(team.Players[i]) : AttributeProfile.Uniform(70);
            }
            return roster;
        }

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
