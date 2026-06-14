using NUnit.Framework;
using Shift9.Presentation.Players;
using UnityEngine;

namespace Shift9.Presentation.Tests
{
    public sealed class PlayerPlacementTests
    {
        [Test]
        public void BothTeams_HaveFivePlayers()
        {
            Assert.AreEqual(PlayerPlacement.TeamSize, PlayerPlacement.Offense().Length);
            Assert.AreEqual(PlayerPlacement.TeamSize, PlayerPlacement.Defense().Length);
        }

        [Test]
        public void AllPlayers_AreInBoundsInTheFrontcourt()
        {
            foreach (var p in PlayerPlacement.Offense())
            {
                Assert.IsTrue(PlayerPlacement.InBounds(p));
                Assert.Greater(p.z, 0f); // attacking the +z basket
                Assert.AreEqual(0f, p.y, 0.0001f);
            }
            foreach (var p in PlayerPlacement.Defense())
            {
                Assert.IsTrue(PlayerPlacement.InBounds(p));
                Assert.Greater(p.z, 0f);
            }
        }

        [Test]
        public void EachDefender_IsGoalsideOfTheirMan()
        {
            var off = PlayerPlacement.Offense();
            var def = PlayerPlacement.Defense();
            for (int i = 0; i < off.Length; i++)
                Assert.Greater(def[i].z, off[i].z); // closer to the +z basket
        }
    }
}
