using System;
using System.Collections.Generic;
using Shift9.Customization.Model;
using UnityEngine;

namespace Shift9.Customization.Mapping
{
    /// <summary>
    /// Pure transform from a validated manifest into runtime models. Assumes the manifest has
    /// already passed ContentValidator (text sanitized, ranges clamped, URLs vetted, colors
    /// normalized to #RRGGBB), so it performs no re-validation — only structural mapping.
    /// </summary>
    public static class ManifestMapper
    {
        public static RuntimeLeague MapLeague(LeagueManifest m)
        {
            var league = new RuntimeLeague
            {
                Id = m.League.Id,
                Name = m.League.Name,
                LeagueType = m.League.Type
            };

            if (m.Arenas != null)
                foreach (var a in m.Arenas)
                    league.Arenas.Add(new RuntimeArena
                    {
                        Id = a.Id, Name = a.Name,
                        FloorUrl = a.FloorUrl, JumbotronUrl = a.JumbotronUrl,
                        CrowdDensity = a.CrowdDensity, LightingPreset = a.LightingPreset
                    });

            foreach (var t in m.Teams)
            {
                var team = new RuntimeTeam
                {
                    Id = t.Id, Name = t.Name, ArenaId = t.ArenaId,
                    Primary = ParseHex(t.Primary), Secondary = ParseHex(t.Secondary),
                    LogoUrl = t.LogoUrl
                };

                if (t.Uniforms != null)
                    foreach (var u in t.Uniforms)
                        team.Uniforms.Add(new RuntimeUniform
                        {
                            Slot = ParseSlot(u.Slot), BaseUrl = u.BaseUrl, MaskUrl = u.MaskUrl
                        });

                if (t.Players != null)
                    foreach (var p in t.Players)
                        team.Players.Add(new RuntimePlayer
                        {
                            Id = p.Id, Name = p.Name, Number = p.Number,
                            Attributes = MapAttributes(p.Attributes)
                        });

                league.Teams.Add(team);
            }
            return league;
        }

        public static List<RuntimeSneaker> MapSneakers(SneakerManifest m)
        {
            var list = new List<RuntimeSneaker>(m.Sneakers.Count);
            foreach (var s in m.Sneakers)
                list.Add(new RuntimeSneaker { Id = s.Id, Name = s.Name, ImageUrl = s.ImageUrl });
            return list;
        }

        private static UniformSlot ParseSlot(string slot) =>
            Enum.TryParse<UniformSlot>(slot, ignoreCase: true, out var s) ? s : UniformSlot.Custom;

        // Assumes validator-normalized #RRGGBB; falls back to opaque magenta only if somehow malformed.
        private static Color32 ParseHex(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length != 7 || hex[0] != '#')
                return new Color32(255, 0, 255, 255);
            byte r = (byte)Convert.ToInt32(hex.Substring(1, 2), 16);
            byte g = (byte)Convert.ToInt32(hex.Substring(3, 2), 16);
            byte b = (byte)Convert.ToInt32(hex.Substring(5, 2), 16);
            return new Color32(r, g, b, 255);
        }

        private static RuntimeAttributes MapAttributes(AttributeBlock a)
        {
            if (a == null) return default;
            return new RuntimeAttributes
            {
                FreeThrow = (byte)a.FreeThrow,
                ShotClose = (byte)a.ShotClose,
                MidRange = (byte)a.MidRange,
                ThreePoint = (byte)a.ThreePoint,
                DunkRating = (byte)a.DunkRating,
                VerticalLeap = (byte)a.VerticalLeap,
                Speed = (byte)a.Speed,
                PassingAccuracy = (byte)a.PassingAccuracy,
                PhysicalStrength = (byte)a.PhysicalStrength,
                Hustle = (byte)a.Hustle,
                PerimeterDefense = (byte)a.PerimeterDefense,
                InteriorDefense = (byte)a.InteriorDefense,
                DefensiveAwareness = (byte)a.DefensiveAwareness,
                HandleControl = (byte)a.HandleControl
            };
        }
    }
}
