using System.Collections.Generic;
using NUnit.Framework;
using Shift9.Customization.Mapping;
using Shift9.Customization.Model;

namespace Shift9.Customization.Tests
{
    public sealed class StatsMappingTests
    {
        private static LeagueManifest WithPlayer(PlayerDef player) => new LeagueManifest
        {
            Schema = 1,
            League = new LeagueDef { Id = "l", Name = "League" },
            Teams = new List<TeamDef>
            {
                new TeamDef { Id = "t", Name = "Team", Players = new List<PlayerDef> { player } }
            }
        };

        [Test]
        public void Mapper_CarriesPlayerStats()
        {
            var m = WithPlayer(new PlayerDef { Id = "p", Stats = new StatsBlock { Points = 25f, Assists = 8f, Blocks = 2f } });
            RuntimeLeague league = ManifestMapper.MapLeague(m);

            RuntimeStats stats = league.Teams[0].Players[0].Stats;
            Assert.IsNotNull(stats);
            Assert.AreEqual(25f, stats.Points);
            Assert.AreEqual(8f, stats.Assists);
            Assert.AreEqual(2f, stats.Blocks);
        }

        [Test]
        public void Mapper_NullStatsWhenAbsent()
        {
            var m = WithPlayer(new PlayerDef { Id = "p", Attributes = new AttributeBlock { ThreePoint = 70 } });
            RuntimeLeague league = ManifestMapper.MapLeague(m);

            Assert.IsNull(league.Teams[0].Players[0].Stats);
            Assert.AreEqual(70, league.Teams[0].Players[0].Attributes.ThreePoint);
        }
    }
}
