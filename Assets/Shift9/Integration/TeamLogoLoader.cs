using System.Threading;
using System.Threading.Tasks;
using Shift9.Customization.Caching;
using Shift9.Customization.Mapping;
using Shift9.Presentation;
using UnityEngine;

namespace Shift9.Integration
{
    /// <summary>
    /// Loads imported team logos through the customization image pipeline (fetch → validate →
    /// decode → cache) and hands the textures to the scoreboard bug. Missing/failed logos degrade
    /// silently to the team color tab. This is the seam between the asset cache and the HUD.
    /// </summary>
    public static class TeamLogoLoader
    {
        public static async Task LoadAsync(AssetCache cache, ScoreboardHud hud,
            RuntimeTeam home, RuntimeTeam away, CancellationToken ct = default)
        {
            if (hud == null) return;
            Texture homeLogo = await LoadOne(cache, home?.LogoUrl, ct);
            Texture awayLogo = await LoadOne(cache, away?.LogoUrl, ct);
            hud.SetLogos(homeLogo, awayLogo);
        }

        private static async Task<Texture> LoadOne(AssetCache cache, string url, CancellationToken ct)
        {
            if (cache == null || string.IsNullOrEmpty(url)) return null;
            var box = new ResultBox();
            Sprite sprite = await cache.GetSpriteAsync(url, ct, box);
            return sprite != null ? sprite.texture : null;
        }
    }
}
