using System.Collections.Generic;
using NUnit.Framework;
using Shift9.Customization.Mapping;
using Shift9.Customization.Model;
using Shift9.Customization.Validation;

namespace Shift9.Customization.Tests
{
    public sealed class TeamLogoTests
    {
        private static LeagueManifest ManifestWithLogos(string homeLogo, string awayLogo) =>
            new LeagueManifest
            {
                Schema = 1,
                League = new LeagueDef { Id = "l", Name = "League", Type = "NBA" },
                Teams = new List<TeamDef>
                {
                    new TeamDef { Id = "t1", Name = "Liberty", Primary = "#1d428a", Secondary = "#ffffff", LogoUrl = homeLogo },
                    new TeamDef { Id = "t2", Name = "Storm",   Primary = "#1d428a", Secondary = "#ffffff", LogoUrl = awayLogo }
                }
            };

        [Test]
        public void Validator_KeepsSafeLogo_DropsUnsafe()
        {
            var m = ManifestWithLogos("https://cdn.example.com/logo.png", "http://insecure/logo.png");
            var validator = new ContentValidator(ValidationConfig.Default);

            ValidationResult r = validator.ValidateLeague(m);

            Assert.IsTrue(r.Ok);
            Assert.AreEqual("https://cdn.example.com/logo.png", m.Teams[0].LogoUrl);
            Assert.IsNull(m.Teams[1].LogoUrl); // http logo rejected -> nulled to fall back to color tab
        }

        [Test]
        public void Mapper_CarriesLogoUrl()
        {
            var m = ManifestWithLogos("https://cdn.example.com/logo.png", null);
            RuntimeLeague league = ManifestMapper.MapLeague(m);

            Assert.AreEqual("https://cdn.example.com/logo.png", league.Teams[0].LogoUrl);
            Assert.IsNull(league.Teams[1].LogoUrl);
        }
    }
}
