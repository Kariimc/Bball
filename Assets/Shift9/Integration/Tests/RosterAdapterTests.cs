using System.Collections.Generic;
using NUnit.Framework;
using Shift9.Customization.Mapping;
using Shift9.Sim.Core;
using Shift9.Sim.Roster;
using Shift9.Sim.Shooting;
using UnityEngine;

namespace Shift9.Integration.Tests
{
    public sealed class RosterAdapterTests
    {
        [Test]
        public void ToAttributeProfile_CopiesAllRatings()
        {
            var a = new RuntimeAttributes
            {
                FreeThrow = 80, ShotClose = 70, MidRange = 60, ThreePoint = 90, DunkRating = 85,
                VerticalLeap = 75, Speed = 88, PassingAccuracy = 72, PhysicalStrength = 65,
                Hustle = 68, PerimeterDefense = 77, InteriorDefense = 55, DefensiveAwareness = 81,
                HandleControl = 84
            };

            var profile = RosterAdapter.ToAttributeProfile(a);

            Assert.AreEqual(90, profile.ThreePoint);
            Assert.AreEqual(88, profile.Speed);
            Assert.AreEqual(80, profile.FreeThrow);
            Assert.AreEqual(84, profile.HandleControl);
            Assert.AreEqual(55, profile.InteriorDefense);
        }

        [Test]
        public void ToSimTeam_PreservesIdentityAndDefaultsDynamics()
        {
            var team = new RuntimeTeam
            {
                Id = "t1", Name = "Liberty",
                Players = new List<RuntimePlayer>
                {
                    new RuntimePlayer { Id = "p1", Name = "Star", Number = 7,
                        Attributes = new RuntimeAttributes { ThreePoint = 91 } }
                }
            };

            SimTeam sim = RosterAdapter.ToSimTeam(team);

            Assert.AreEqual("t1", sim.Id);
            Assert.AreEqual("Liberty", sim.Name);
            Assert.AreEqual(1, sim.Players.Count);
            Assert.AreEqual("Star", sim.Players[0].Name);
            Assert.AreEqual(7, sim.Players[0].Number);
            Assert.AreEqual(91, sim.Players[0].Attributes.ThreePoint);
            Assert.AreEqual(1f, sim.Players[0].Dynamics.Stamina); // fresh by default
        }

        [Test]
        public void ToSimTeams_MapsEveryTeam()
        {
            var league = new RuntimeLeague();
            league.Teams.Add(new RuntimeTeam { Id = "a", Name = "A" });
            league.Teams.Add(new RuntimeTeam { Id = "b", Name = "B" });

            List<SimTeam> teams = RosterAdapter.ToSimTeams(league);
            Assert.AreEqual(2, teams.Count);
            Assert.AreEqual("a", teams[0].Id);
            Assert.AreEqual("b", teams[1].Id);
        }

        [Test]
        public void ImportedRatings_ActuallyDriveTheShotModel()
        {
            // The whole point: an imported 90 three-point shooter must out-shoot an imported 30.
            var elitePlayer = new RuntimePlayer { Id = "e", Attributes = new RuntimeAttributes { ThreePoint = 90 } };
            var weakPlayer = new RuntimePlayer { Id = "w", Attributes = new RuntimeAttributes { ThreePoint = 30 } };

            var elite = RosterAdapter.ToSimPlayer(elitePlayer);
            var weak = RosterAdapter.ToSimPlayer(weakPlayer);

            var threeSpot = new Vector3(0, 0, -17f); // beyond the home arc
            var rng = new DeterministicRng(1);

            float eliteP = Resolve(elite, threeSpot, ref rng);
            float weakP = Resolve(weak, threeSpot, ref rng);
            Assert.Greater(eliteP, weakP);
        }

        private static float Resolve(SimPlayer player, Vector3 spot, ref DeterministicRng rng)
        {
            var ctx = new ShotContext
            {
                Position = spot, HomeBasket = true,
                Attributes = player.Attributes, Dynamics = player.Dynamics,
                Openness = 1f, ReleaseErrorSeconds = 0f
            };
            return ShotResolver.Resolve(ctx, ref rng, ShotModelConfig.Default).Probability;
        }
    }
}
