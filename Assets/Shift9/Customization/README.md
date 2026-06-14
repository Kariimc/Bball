# Shift9 Customization Engine — Core Import Pipeline

Headless, dependency-injected pipeline for importing teams/leagues/arenas and cosmetic
sneakers from a remote manifest URL (Hoop Land style). This is the **core slice**:
data contract, security validation, caching, transport, and mapping. Rendering
(uniform recolor + sneaker slot compositing) and UI are intentionally **not** included yet.

## Assembly layout

```
Model/        ManifestSchema.cs      Versioned JSON DTOs (schema == 1)
Validation/   ValidationConfig.cs    Hard limits (sizes, counts, ranges)
              ContentValidator.cs    The security gauntlet (pure, unit-tested)
              ImageHeaderInspector   PNG/JPEG header sniff (no decode)
              ValidationResult.cs
Caching/      AssetCache.cs          Content-addressed disk + LRU memory cache
              HashUtil.cs            SHA-256 URL keying
Mapping/      RuntimeModels.cs       Game-facing validated models
              ManifestMapper.cs      Pure manifest -> runtime transform
Pipeline/     IContentFetcher.cs     Transport abstraction (DI seam for tests)
              UnityWebFetcher.cs     Production UnityWebRequest transport (byte-capped mid-stream)
              ImportPipeline.cs      fetch -> validate -> cache -> map orchestration
              ImportResult.cs        Result + lifecycle states
Tests/        EditMode NUnit suite (validator, header inspector, cache, mapper, pipeline)
```

## Required Unity packages

Add via Package Manager (or `Packages/manifest.json`):

```json
"com.unity.nuget.newtonsoft-json": "3.2.1",
"com.unity.test-framework": "1.4.5"
```

`Newtonsoft.Json` is referenced for robust JSON (missing-field tolerance, `MaxDepth`
DoS cap) that `JsonUtility` cannot provide.

## Security model (why each limit exists)

All remote content is **untrusted** until it passes `ContentValidator`:

- **HTTPS-only + private-host block** — rejects `http`/`file`, `localhost`, RFC1918,
  loopback, and link-local hosts (SSRF / local-file exfil guard).
- **Byte caps, enforced mid-stream** — `UnityWebFetcher` aborts a transfer the moment
  it crosses the cap; bytes are never fully buffered (DoS/OOM guard).
- **Header-only image inspection** — dimensions are read from ~24 header bytes, so a
  100k×100k "image" is rejected before `LoadImage` allocates VRAM (decompression-bomb guard).
- **Format whitelist by magic bytes** — PNG/JPEG only, never by file extension.
- **Schema gate + quantity caps** — unknown schema rejected; team/player/sneaker counts
  bounded so a hostile manifest can't exhaust memory.
- **Text sanitization** — strips `<...>` markup spans and control chars before names reach
  TMP/uGUI (rich-text injection guard).
- **Stat clamping** — imported ratings clamped to `[0,99]` before they can reach the sim.

Sneakers are **cosmetic only** by current product decision — no stat modifiers are parsed.

## Integration

```csharp
var cfg       = ValidationConfig.Default;
var fetcher   = new UnityWebFetcher();
var validator = new ContentValidator(cfg);
var cache     = new AssetCache(Application.persistentDataPath + "/imgcache",
                               fetcher, validator,
                               memoryBudgetBytes: 64L * 1024 * 1024,
                               maxImageBytes: cfg.MaxImageBytes);
var pipeline  = new ImportPipeline(fetcher, validator, cfg, cache, progress: null);

ImportResult<RuntimeLeague> result = await pipeline.ImportLeagueAsync(userUrl);
if (result.Success) { /* hand result.Value to roster/render systems */ }
else                { /* surface result.Error to UI */ }
```

The Shift9 sim adapter (copying `RuntimeAttributes` into the engine's `AttributeProfile`)
is a trivial member copy and lands when the engine module is committed to the repo.

## Verification status

The EditMode suite under `Tests/` covers the security gauntlet, header parsing, cache
disk/dedup behavior, mapping, and the full pipeline success/failure paths via an injected
fake fetcher (no network, no GPU). **Run it in-editor:** `Window ▸ General ▸ Test Runner ▸
EditMode ▸ Run All`. It was authored and statically traced outside Unity (this repo has no
Unity/.NET toolchain), so execute it once on your machine to confirm green before building on top.
