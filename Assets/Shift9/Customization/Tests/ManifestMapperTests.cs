using System.Collections.Generic;
using NUnit.Framework;
using Shift9.Customization.Mapping;
using Shift9.Customization.Model;

namespace Shift9.Customization.Tests
{
    public sealed class ManifestMapperTests
    {
        [Test]
        public void MapLeague_ParsesColorsSlotsAndAttributes()
        {
            var m = new LeagueManifest
            {
                Schema = 1,
                League = new LeagueDef { Id = "l", Name = "League", Type = "WNBA" },
                Teams = new List<TeamDef>
                {
                    new TeamDef
                    {
                        Id = "t1", Name = "Liberty", ArenaId = "a1",
                        Primary = "#1D428A", Secondary = "#FFFFFF",
                        Uniforms = new List<UniformDef>
                        {
                            new UniformDef { Slot = "Retro", BaseUrl = "https://cdn.example.com/r.png" },
                            new UniformDef { Slot = "Wacky", BaseUrl = "https://cdn.example.com/w.png" }
                        },
                        Players = new List<PlayerDef>
                        {
                            new PlayerDef { Id = "p1", Name = "Star", Number = 7,
                                Attributes = new AttributeBlock { Speed = 88, ThreePoint = 91 } }
                        }
                    }
                }
            };

            RuntimeLeague league = ManifestMapper.MapLeague(m);
            var team = league.Teams[0];

            Assert.AreEqual(0x1D, team.Primary.r);
            Assert.AreEqual(0x42, team.Primary.g);
            Assert.AreEqual(0x8A, team.Primary.b);
            Assert.AreEqual(255, team.Secondary.r);

            Assert.AreEqual(UniformSlot.Retro, team.Uniforms[0].Slot);
            Assert.AreEqual(UniformSlot.Custom, team.Uniforms[1].Slot); // unknown slot → Custom

            Assert.AreEqual((byte)88, team.Players[0].Attributes.Speed);
            Assert.AreEqual((byte)91, team.Players[0].Attributes.ThreePoint);
        }

        [Test]
        public void MapSneakers_CopiesCosmeticFields()
        {
            var m = new SneakerManifest
            {
                Schema = 1,
                Sneakers = new List<SneakerDef>
                {
                    new SneakerDef { Id = "s1", Name = "CW", ImageUrl = "https://cdn.example.com/k.png" }
                }
            };
            List<RuntimeSneaker> result = ManifestMapper.MapSneakers(m);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("s1", result[0].Id);
            Assert.AreEqual("https://cdn.example.com/k.png", result[0].ImageUrl);
        }
    }
}
