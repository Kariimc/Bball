using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Shift9.Customization.Model
{
    // ==============================================================================
    // Versioned import manifest schema (schema == 1).
    //
    // Two independent document shapes share this file because they are imported from
    // separate URLs per the product spec:
    //   * LeagueManifest  -> leagues / teams / arenas / players / uniforms
    //   * SneakerManifest -> cosmetic sneaker image slots (no gameplay effect)
    //
    // All types are POCOs deserialized with Newtonsoft. They are UNTRUSTED until they
    // pass ContentValidator; nothing here may be fed to the sim or the GPU directly.
    // ==============================================================================

    /// <summary>Root document for a teams/leagues/arenas import.</summary>
    [Serializable]
    public sealed class LeagueManifest
    {
        [JsonProperty("schema")]  public int Schema;
        [JsonProperty("league")]  public LeagueDef League;
        [JsonProperty("arenas")]  public List<ArenaDef> Arenas;
        [JsonProperty("teams")]   public List<TeamDef> Teams;
    }

    [Serializable]
    public sealed class LeagueDef
    {
        [JsonProperty("id")]   public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("type")] public string Type; // "NBA" | "WNBA" | custom — free text, sanitized
    }

    [Serializable]
    public sealed class ArenaDef
    {
        [JsonProperty("id")]           public string Id;
        [JsonProperty("name")]         public string Name;
        [JsonProperty("floorUrl")]     public string FloorUrl;
        [JsonProperty("jumbotronUrl")] public string JumbotronUrl;
        [JsonProperty("crowdDensity")] public float CrowdDensity;     // clamped 0..1
        [JsonProperty("lightingPreset")] public string LightingPreset;
    }

    [Serializable]
    public sealed class TeamDef
    {
        [JsonProperty("id")]        public string Id;
        [JsonProperty("name")]      public string Name;
        [JsonProperty("arenaId")]   public string ArenaId;
        [JsonProperty("primary")]   public string Primary;   // hex "#RRGGBB"
        [JsonProperty("secondary")] public string Secondary; // hex "#RRGGBB"
        [JsonProperty("logoUrl")]   public string LogoUrl;   // optional team logo image
        [JsonProperty("uniforms")]  public List<UniformDef> Uniforms;
        [JsonProperty("players")]   public List<PlayerDef> Players;
    }

    [Serializable]
    public sealed class UniformDef
    {
        [JsonProperty("slot")]    public string Slot;    // Home | Away | Alternate | Retro
        [JsonProperty("baseUrl")] public string BaseUrl;
        [JsonProperty("maskUrl")] public string MaskUrl; // optional recolor mask
    }

    [Serializable]
    public sealed class PlayerDef
    {
        [JsonProperty("id")]         public string Id;
        [JsonProperty("name")]       public string Name;
        [JsonProperty("number")]     public int Number;
        [JsonProperty("attributes")] public AttributeBlock Attributes;
    }

    /// <summary>
    /// Gameplay-affecting ratings. Mirrors Shift9 AttributeProfile field-for-field but uses
    /// int (Newtonsoft default) so out-of-range/oversized values are detectable BEFORE the
    /// clamp; ContentValidator clamps each to [0,99] prior to mapping into the sim.
    /// </summary>
    [Serializable]
    public sealed class AttributeBlock
    {
        [JsonProperty("freeThrow")]          public int FreeThrow;
        [JsonProperty("shotClose")]          public int ShotClose;
        [JsonProperty("midRange")]           public int MidRange;
        [JsonProperty("threePoint")]         public int ThreePoint;
        [JsonProperty("dunkRating")]         public int DunkRating;
        [JsonProperty("verticalLeap")]       public int VerticalLeap;
        [JsonProperty("speed")]              public int Speed;
        [JsonProperty("passingAccuracy")]    public int PassingAccuracy;
        [JsonProperty("physicalStrength")]   public int PhysicalStrength;
        [JsonProperty("hustle")]             public int Hustle;
        [JsonProperty("perimeterDefense")]   public int PerimeterDefense;
        [JsonProperty("interiorDefense")]    public int InteriorDefense;
        [JsonProperty("defensiveAwareness")] public int DefensiveAwareness;
        [JsonProperty("handleControl")]      public int HandleControl;
    }

    /// <summary>Root document for the separate cosmetic sneaker import.</summary>
    [Serializable]
    public sealed class SneakerManifest
    {
        [JsonProperty("schema")]   public int Schema;
        [JsonProperty("sneakers")] public List<SneakerDef> Sneakers;
    }

    [Serializable]
    public sealed class SneakerDef
    {
        [JsonProperty("id")]       public string Id;
        [JsonProperty("name")]     public string Name;
        [JsonProperty("imageUrl")] public string ImageUrl; // cosmetic only — no stat modifiers
    }
}
