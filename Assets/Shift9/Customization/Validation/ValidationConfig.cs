namespace Shift9.Customization.Validation
{
    /// <summary>
    /// Hard limits for the import security gauntlet. Values are tuned conservatively for the
    /// tightest target (Android); Steam can raise them. Every limit exists to bound memory or
    /// close an attack vector — see field comments. Immutable after construction.
    /// </summary>
    public readonly struct ValidationConfig
    {
        public readonly int  SupportedSchema;     // reject unknown schema versions outright
        public readonly long MaxManifestBytes;    // cap JSON payload (decompression/OOM guard)
        public readonly long MaxImageBytes;       // cap each downloaded image (raw bytes)
        public readonly int  MaxImageDimension;   // cap decoded W/H (defeats decompression bombs)
        public readonly int  MaxArenas;
        public readonly int  MaxTeams;
        public readonly int  MaxPlayersPerTeam;
        public readonly int  MaxUniformsPerTeam;
        public readonly int  MaxSneakers;
        public readonly int  MaxNameLength;       // bound text fields before they reach UI
        public readonly int  AttributeMin;        // clamp gameplay ratings into legal range
        public readonly int  AttributeMax;
        public readonly bool RequireHttps;        // reject http/file/etc.
        public readonly bool BlockPrivateHosts;   // reject localhost / RFC1918 / link-local (SSRF)

        public ValidationConfig(int supportedSchema, long maxManifestBytes, long maxImageBytes,
            int maxImageDimension, int maxArenas, int maxTeams, int maxPlayersPerTeam,
            int maxUniformsPerTeam, int maxSneakers, int maxNameLength, int attributeMin,
            int attributeMax, bool requireHttps, bool blockPrivateHosts)
        {
            SupportedSchema = supportedSchema;
            MaxManifestBytes = maxManifestBytes;
            MaxImageBytes = maxImageBytes;
            MaxImageDimension = maxImageDimension;
            MaxArenas = maxArenas;
            MaxTeams = maxTeams;
            MaxPlayersPerTeam = maxPlayersPerTeam;
            MaxUniformsPerTeam = maxUniformsPerTeam;
            MaxSneakers = maxSneakers;
            MaxNameLength = maxNameLength;
            AttributeMin = attributeMin;
            AttributeMax = attributeMax;
            RequireHttps = requireHttps;
            BlockPrivateHosts = blockPrivateHosts;
        }

        /// <summary>Production defaults. Safe for Android and Steam.</summary>
        public static ValidationConfig Default => new ValidationConfig(
            supportedSchema:   1,
            maxManifestBytes:  2L * 1024 * 1024,   // 2 MB JSON
            maxImageBytes:     4L * 1024 * 1024,   // 4 MB per image
            maxImageDimension: 2048,               // 2K max per side
            maxArenas:         64,
            maxTeams:          64,
            maxPlayersPerTeam: 20,
            maxUniformsPerTeam: 8,
            maxSneakers:       256,
            maxNameLength:     48,
            attributeMin:      0,
            attributeMax:      99,                 // NBA-style 0..99 rating scale
            requireHttps:      true,
            blockPrivateHosts: true);
    }
}
