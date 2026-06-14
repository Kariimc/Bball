using System.Collections.Generic;
using UnityEngine;

namespace Shift9.Customization.Mapping
{
    public enum UniformSlot : byte { Home, Away, Alternate, Retro, Custom }

    /// <summary>
    /// Validated, game-facing models produced from a manifest. These are pure data (no GPU
    /// resources): image URLs are carried through so the presentation layer (RenderComposer,
    /// deferred) can resolve Sprites via AssetCache on demand. This keeps the core headless
    /// and unit-testable without a GPU.
    /// </summary>
    public sealed class RuntimeLeague
    {
        public string Id;
        public string Name;
        public string LeagueType;
        public List<RuntimeArena> Arenas = new();
        public List<RuntimeTeam> Teams = new();
    }

    public sealed class RuntimeArena
    {
        public string Id;
        public string Name;
        public string FloorUrl;       // may be null (absent/dropped)
        public string JumbotronUrl;   // may be null
        public float CrowdDensity;    // 0..1
        public string LightingPreset;
    }

    public sealed class RuntimeTeam
    {
        public string Id;
        public string Name;
        public string ArenaId;
        public Color32 Primary;
        public Color32 Secondary;
        public List<RuntimeUniform> Uniforms = new();
        public List<RuntimePlayer> Players = new();
    }

    public sealed class RuntimeUniform
    {
        public UniformSlot Slot;
        public string BaseUrl;
        public string MaskUrl; // may be null
    }

    public sealed class RuntimePlayer
    {
        public string Id;
        public string Name;
        public int Number;
        public RuntimeAttributes Attributes;
    }

    /// <summary>
    /// Field-for-field mirror of Shift9.NBAEngine.Entities.AttributeProfile, stored as bytes
    /// (values already clamped to 0..99 by ContentValidator). Kept local so this assembly does
    /// not hard-depend on the sim engine; the adapter into the engine is a trivial member copy
    /// once that module lands in the repo.
    /// </summary>
    public struct RuntimeAttributes
    {
        public byte FreeThrow, ShotClose, MidRange, ThreePoint, DunkRating, VerticalLeap, Speed;
        public byte PassingAccuracy, PhysicalStrength, Hustle, PerimeterDefense, InteriorDefense;
        public byte DefensiveAwareness, HandleControl;
    }

    /// <summary>Validated cosmetic sneaker entry (no gameplay effect).</summary>
    public sealed class RuntimeSneaker
    {
        public string Id;
        public string Name;
        public string ImageUrl;
    }
}
