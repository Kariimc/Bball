using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shift9.Customization.Caching;
using Shift9.Customization.Mapping;
using Shift9.Customization.Model;
using Shift9.Customization.Validation;

namespace Shift9.Customization.Pipeline
{
    /// <summary>
    /// Orchestrates a single-manifest-URL import: fetch -> validate -> (optional raw cache warm)
    /// -> map. Transport and cache are injected, so the whole flow is unit-testable with a fake
    /// fetcher and no network/GPU. Reports stage transitions through an optional IProgress.
    /// </summary>
    public sealed class ImportPipeline
    {
        // Hard cap on JSON nesting depth — defeats deeply-nested-array parser DoS.
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            MaxDepth = 32,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly IContentFetcher _fetcher;
        private readonly ContentValidator _validator;
        private readonly ValidationConfig _cfg;
        private readonly AssetCache _cache;        // optional; null disables raw cache warming
        private readonly IProgress<ImportState> _progress;

        public ImportPipeline(IContentFetcher fetcher, ContentValidator validator,
            ValidationConfig cfg, AssetCache cache = null, IProgress<ImportState> progress = null)
        {
            _fetcher = fetcher;
            _validator = validator;
            _cfg = cfg;
            _cache = cache;
            _progress = progress;
        }

        public async Task<ImportResult<RuntimeLeague>> ImportLeagueAsync(string url, CancellationToken ct = default)
        {
            Report(ImportState.Fetching);
            if (!_validator.IsUrlSafe(url))
                return Fail<RuntimeLeague>("Manifest URL rejected (must be https, public host).");

            FetchResponse fetch = await _fetcher.FetchAsync(url, _cfg.MaxManifestBytes, ct);
            if (!fetch.Success)
                return Fail<RuntimeLeague>($"Fetch failed ({fetch.StatusCode}): {fetch.Error}");

            ValidationResult bytesOk = _validator.ValidateManifestBytes(fetch.Data);
            if (!bytesOk.Ok) return Fail<RuntimeLeague>(bytesOk.Error);

            Report(ImportState.Validating);
            if (!TryParse<LeagueManifest>(fetch.Data, out var manifest, out string parseErr))
                return Fail<RuntimeLeague>(parseErr);

            ValidationResult valid = _validator.ValidateLeague(manifest);
            if (!valid.Ok) return Fail<RuntimeLeague>(valid.Error);
            var warnings = valid.Warnings ?? new List<string>();

            if (_cache != null)
            {
                Report(ImportState.Caching);
                await WarmRawCache(CollectLeagueImageUrls(manifest), warnings, ct);
            }

            Report(ImportState.Mapping);
            RuntimeLeague league = ManifestMapper.MapLeague(manifest);

            Report(ImportState.Applied);
            return ImportResult<RuntimeLeague>.Ok(league, warnings);
        }

        public async Task<ImportResult<List<RuntimeSneaker>>> ImportSneakersAsync(string url, CancellationToken ct = default)
        {
            Report(ImportState.Fetching);
            if (!_validator.IsUrlSafe(url))
                return Fail<List<RuntimeSneaker>>("Sneaker URL rejected (must be https, public host).");

            FetchResponse fetch = await _fetcher.FetchAsync(url, _cfg.MaxManifestBytes, ct);
            if (!fetch.Success)
                return Fail<List<RuntimeSneaker>>($"Fetch failed ({fetch.StatusCode}): {fetch.Error}");

            ValidationResult bytesOk = _validator.ValidateManifestBytes(fetch.Data);
            if (!bytesOk.Ok) return Fail<List<RuntimeSneaker>>(bytesOk.Error);

            Report(ImportState.Validating);
            if (!TryParse<SneakerManifest>(fetch.Data, out var manifest, out string parseErr))
                return Fail<List<RuntimeSneaker>>(parseErr);

            ValidationResult valid = _validator.ValidateSneakers(manifest);
            if (!valid.Ok) return Fail<List<RuntimeSneaker>>(valid.Error);
            var warnings = valid.Warnings ?? new List<string>();

            if (_cache != null)
            {
                Report(ImportState.Caching);
                var urls = new List<string>(manifest.Sneakers.Count);
                foreach (var s in manifest.Sneakers) urls.Add(s.ImageUrl);
                await WarmRawCache(urls, warnings, ct);
            }

            Report(ImportState.Mapping);
            List<RuntimeSneaker> sneakers = ManifestMapper.MapSneakers(manifest);

            Report(ImportState.Applied);
            return ImportResult<List<RuntimeSneaker>>.Ok(sneakers, warnings);
        }

        // ---- internals ----------------------------------------------------------------------

        private bool TryParse<T>(byte[] data, out T value, out string error) where T : class
        {
            value = null; error = null;
            try
            {
                string json = DecodeUtf8(data);
                value = JsonConvert.DeserializeObject<T>(json, JsonSettings);
                if (value == null) { error = "Manifest deserialized to null."; return false; }
                return true;
            }
            catch (JsonException e) { error = $"Manifest JSON invalid: {e.Message}"; return false; }
            catch (Exception e)     { error = $"Manifest parse error: {e.Message}"; return false; }
        }

        // Strips a UTF-8 BOM if present so the JSON parser does not choke on the leading marker.
        private static string DecodeUtf8(byte[] data)
        {
            int offset = (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) ? 3 : 0;
            return Encoding.UTF8.GetString(data, offset, data.Length - offset);
        }

        private static List<string> CollectLeagueImageUrls(LeagueManifest m)
        {
            var urls = new List<string>();
            if (m.Arenas != null)
                foreach (var a in m.Arenas)
                {
                    if (a.FloorUrl != null) urls.Add(a.FloorUrl);
                    if (a.JumbotronUrl != null) urls.Add(a.JumbotronUrl);
                }
            foreach (var t in m.Teams)
                if (t.Uniforms != null)
                    foreach (var u in t.Uniforms)
                    {
                        if (u.BaseUrl != null) urls.Add(u.BaseUrl);
                        if (u.MaskUrl != null) urls.Add(u.MaskUrl);
                    }
            return urls;
        }

        // Pre-fetches raw image bytes to disk. Failures are non-fatal: a missing image degrades
        // to a fallback at render time, so we record a warning and continue rather than abort.
        private async Task WarmRawCache(List<string> urls, List<string> warnings, CancellationToken ct)
        {
            var seen = new HashSet<string>();
            foreach (string url in urls)
            {
                if (string.IsNullOrEmpty(url) || !seen.Add(url)) continue; // dedup
                ct.ThrowIfCancellationRequested();
                var box = new ResultBox();
                byte[] raw = await _cache.GetRawAsync(url, ct, box);
                if (raw == null) warnings.Add($"Image unavailable, will use fallback: {box.Error}");
            }
        }

        private void Report(ImportState s) => _progress?.Report(s);

        private ImportResult<T> Fail<T>(string reason)
        {
            Report(ImportState.Failed);
            return ImportResult<T>.Fail(reason);
        }
    }
}
