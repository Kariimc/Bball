using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Shift9.Customization.Model;

namespace Shift9.Customization.Validation
{
    /// <summary>
    /// The import security gauntlet. Every byte of untrusted remote content passes through here
    /// before it can reach the sim or the GPU. Pure (no I/O), so it is fully unit-testable and
    /// allocation-light. Manifest validation mutates in place: it sanitizes text, clamps numeric
    /// ranges, and nulls out individual bad asset URLs (recording a warning) rather than failing
    /// the whole import for one broken image.
    /// </summary>
    public sealed class ContentValidator
    {
        private readonly ValidationConfig _cfg;

        public ContentValidator(ValidationConfig cfg) => _cfg = cfg;

        // ---- Raw payload size ---------------------------------------------------------------

        public ValidationResult ValidateManifestBytes(byte[] raw)
        {
            if (raw == null || raw.Length == 0) return ValidationResult.Fail("Empty manifest payload.");
            if (raw.Length > _cfg.MaxManifestBytes)
                return ValidationResult.Fail($"Manifest exceeds {_cfg.MaxManifestBytes} bytes.");
            return ValidationResult.Success;
        }

        // ---- Image bytes (format + dimensions, header-only, no decode) -----------------------

        public ValidationResult ValidateImageBytes(byte[] data)
        {
            if (data == null || data.Length == 0) return ValidationResult.Fail("Empty image payload.");
            if (data.Length > _cfg.MaxImageBytes)
                return ValidationResult.Fail($"Image exceeds {_cfg.MaxImageBytes} bytes.");
            if (!ImageHeaderInspector.TryInspect(data, out var fmt, out int w, out int h))
                return ValidationResult.Fail("Unrecognized image format (PNG/JPEG only).");
            if (fmt == ImageFormat.Unknown)
                return ValidationResult.Fail("Image format not whitelisted.");
            if (w > _cfg.MaxImageDimension || h > _cfg.MaxImageDimension)
                return ValidationResult.Fail($"Image {w}x{h} exceeds max side {_cfg.MaxImageDimension}.");
            return ValidationResult.Success;
        }

        // ---- URL safety ---------------------------------------------------------------------

        public bool IsUrlSafe(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            if (_cfg.RequireHttps && !string.Equals(uri.Scheme, "https", StringComparison.Ordinal))
                return false;
            if (_cfg.BlockPrivateHosts && IsBlockedHost(uri.DnsSafeHost)) return false;
            return true;
        }

        // Blocks localhost and literal private/loopback/link-local IPs. NOTE: this does not
        // resolve hostnames (no synchronous DNS in the validator); DNS-rebinding to a private
        // IP is a residual risk to be mitigated at the fetch layer if a proxy is ever added.
        private static bool IsBlockedHost(string host)
        {
            if (string.IsNullOrEmpty(host)) return true;
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;

            if (IPAddress.TryParse(host, out var ip))
            {
                if (IPAddress.IsLoopback(ip)) return true;
                byte[] b = ip.GetAddressBytes();
                if (b.Length == 4)
                {
                    if (b[0] == 10) return true;                              // 10.0.0.0/8
                    if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true; // 172.16.0.0/12
                    if (b[0] == 192 && b[1] == 168) return true;             // 192.168.0.0/16
                    if (b[0] == 169 && b[1] == 254) return true;             // 169.254.0.0/16
                    if (b[0] == 0) return true;                              // 0.0.0.0/8
                }
                else if (b.Length == 16)
                {
                    if ((b[0] & 0xFE) == 0xFC) return true;                  // fc00::/7 unique-local
                    if (b[0] == 0xFE && (b[1] & 0xC0) == 0x80) return true;  // fe80::/10 link-local
                }
            }
            return false;
        }

        // ---- League manifest ----------------------------------------------------------------

        public ValidationResult ValidateLeague(LeagueManifest m)
        {
            if (m == null) return ValidationResult.Fail("Manifest did not parse.");
            if (m.Schema != _cfg.SupportedSchema)
                return ValidationResult.Fail($"Unsupported schema {m.Schema} (expected {_cfg.SupportedSchema}).");
            if (m.League == null || string.IsNullOrWhiteSpace(m.League.Id))
                return ValidationResult.Fail("Manifest missing required league.id.");
            if (m.Teams == null || m.Teams.Count == 0)
                return ValidationResult.Fail("Manifest contains no teams.");
            if (m.Teams.Count > _cfg.MaxTeams)
                return ValidationResult.Fail($"Too many teams ({m.Teams.Count} > {_cfg.MaxTeams}).");
            if (m.Arenas != null && m.Arenas.Count > _cfg.MaxArenas)
                return ValidationResult.Fail($"Too many arenas ({m.Arenas.Count} > {_cfg.MaxArenas}).");

            var warnings = new List<string>();

            m.League.Name = Sanitize(m.League.Name);
            m.League.Type = Sanitize(m.League.Type);

            if (m.Arenas != null)
            {
                foreach (var a in m.Arenas)
                {
                    if (a == null || string.IsNullOrWhiteSpace(a.Id))
                        return ValidationResult.Fail("An arena is missing its required id.");
                    a.Name = Sanitize(a.Name);
                    a.LightingPreset = Sanitize(a.LightingPreset);
                    a.CrowdDensity = Clamp01(a.CrowdDensity);
                    a.FloorUrl = KeepUrlOrWarn(a.FloorUrl, $"arena '{a.Id}' floor", warnings);
                    a.JumbotronUrl = KeepUrlOrWarn(a.JumbotronUrl, $"arena '{a.Id}' jumbotron", warnings);
                }
            }

            foreach (var t in m.Teams)
            {
                if (t == null || string.IsNullOrWhiteSpace(t.Id))
                    return ValidationResult.Fail("A team is missing its required id.");
                t.Name = Sanitize(t.Name);
                t.Primary = NormalizeHexOrWarn(t.Primary, "#1E1E1E", $"team '{t.Id}' primary", warnings);
                t.Secondary = NormalizeHexOrWarn(t.Secondary, "#FFFFFF", $"team '{t.Id}' secondary", warnings);

                if (t.Uniforms != null)
                {
                    if (t.Uniforms.Count > _cfg.MaxUniformsPerTeam)
                        return ValidationResult.Fail($"Team '{t.Id}' has too many uniforms.");
                    for (int i = t.Uniforms.Count - 1; i >= 0; i--)
                    {
                        var u = t.Uniforms[i];
                        if (u == null || string.IsNullOrWhiteSpace(u.Slot) || !IsUrlSafe(u.BaseUrl))
                        {
                            warnings.Add($"Dropped invalid uniform on team '{t.Id}'.");
                            t.Uniforms.RemoveAt(i);
                            continue;
                        }
                        u.Slot = Sanitize(u.Slot);
                        u.MaskUrl = KeepUrlOrWarn(u.MaskUrl, $"team '{t.Id}' uniform mask", warnings);
                    }
                }

                if (t.Players != null)
                {
                    if (t.Players.Count > _cfg.MaxPlayersPerTeam)
                        return ValidationResult.Fail($"Team '{t.Id}' has too many players.");
                    foreach (var p in t.Players)
                    {
                        if (p == null || string.IsNullOrWhiteSpace(p.Id))
                            return ValidationResult.Fail($"A player on team '{t.Id}' is missing its id.");
                        p.Name = Sanitize(p.Name);
                        p.Number = Math.Clamp(p.Number, 0, 99);
                        ClampAttributes(p.Attributes);
                    }
                }
            }

            return ValidationResult.SuccessWith(warnings);
        }

        // ---- Sneaker manifest (cosmetic only) -----------------------------------------------

        public ValidationResult ValidateSneakers(SneakerManifest m)
        {
            if (m == null) return ValidationResult.Fail("Sneaker manifest did not parse.");
            if (m.Schema != _cfg.SupportedSchema)
                return ValidationResult.Fail($"Unsupported schema {m.Schema} (expected {_cfg.SupportedSchema}).");
            if (m.Sneakers == null || m.Sneakers.Count == 0)
                return ValidationResult.Fail("Sneaker manifest contains no sneakers.");
            if (m.Sneakers.Count > _cfg.MaxSneakers)
                return ValidationResult.Fail($"Too many sneakers ({m.Sneakers.Count} > {_cfg.MaxSneakers}).");

            var warnings = new List<string>();
            for (int i = m.Sneakers.Count - 1; i >= 0; i--)
            {
                var s = m.Sneakers[i];
                if (s == null || string.IsNullOrWhiteSpace(s.Id) || !IsUrlSafe(s.ImageUrl))
                {
                    warnings.Add("Dropped invalid sneaker entry.");
                    m.Sneakers.RemoveAt(i);
                    continue;
                }
                s.Name = Sanitize(s.Name);
            }
            if (m.Sneakers.Count == 0) return ValidationResult.Fail("No valid sneakers remained after validation.");
            return ValidationResult.SuccessWith(warnings);
        }

        // ---- Helpers ------------------------------------------------------------------------

        private void ClampAttributes(AttributeBlock a)
        {
            if (a == null) return;
            int lo = _cfg.AttributeMin, hi = _cfg.AttributeMax;
            a.FreeThrow = Math.Clamp(a.FreeThrow, lo, hi);
            a.ShotClose = Math.Clamp(a.ShotClose, lo, hi);
            a.MidRange = Math.Clamp(a.MidRange, lo, hi);
            a.ThreePoint = Math.Clamp(a.ThreePoint, lo, hi);
            a.DunkRating = Math.Clamp(a.DunkRating, lo, hi);
            a.VerticalLeap = Math.Clamp(a.VerticalLeap, lo, hi);
            a.Speed = Math.Clamp(a.Speed, lo, hi);
            a.PassingAccuracy = Math.Clamp(a.PassingAccuracy, lo, hi);
            a.PhysicalStrength = Math.Clamp(a.PhysicalStrength, lo, hi);
            a.Hustle = Math.Clamp(a.Hustle, lo, hi);
            a.PerimeterDefense = Math.Clamp(a.PerimeterDefense, lo, hi);
            a.InteriorDefense = Math.Clamp(a.InteriorDefense, lo, hi);
            a.DefensiveAwareness = Math.Clamp(a.DefensiveAwareness, lo, hi);
            a.HandleControl = Math.Clamp(a.HandleControl, lo, hi);
        }

        private string KeepUrlOrWarn(string url, string label, List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(url)) return null; // optional asset, simply absent
            if (IsUrlSafe(url)) return url;
            warnings.Add($"Dropped unsafe URL for {label}.");
            return null;
        }

        private static string NormalizeHexOrWarn(string hex, string fallback, string label, List<string> warnings)
        {
            if (IsHexColor(hex)) return hex.ToUpperInvariant();
            warnings.Add($"Invalid color for {label}; using fallback.");
            return fallback;
        }

        private static bool IsHexColor(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length != 7 || s[0] != '#') return false;
            for (int i = 1; i < 7; i++)
            {
                char c = s[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }
            return true;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        // Strips entire rich-text/markup tag spans (TMP/uGUI interpret '<...>') and control
        // characters, then trims and caps length, so imported names can't inject UI tags,
        // newlines, or bloat layout. An unclosed '<' drops the remainder defensively.
        private string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var sb = new StringBuilder(raw.Length);
            bool inTag = false;
            foreach (char c in raw)
            {
                if (c == '<') { inTag = true; continue; }   // open tag span
                if (c == '>') { inTag = false; continue; }  // close tag span
                if (inTag) continue;                         // drop everything inside <...>
                if (char.IsControl(c)) continue;             // drop control chars / newlines
                sb.Append(c);
                if (sb.Length >= _cfg.MaxNameLength) break;  // hard cap
            }
            return sb.ToString().Trim();
        }
    }
}
