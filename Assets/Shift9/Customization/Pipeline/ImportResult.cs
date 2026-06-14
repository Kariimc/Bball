using System.Collections.Generic;

namespace Shift9.Customization.Pipeline
{
    /// <summary>Lifecycle stages an import passes through; surface to UI for progress feedback.</summary>
    public enum ImportState : byte { Idle, Fetching, Validating, Caching, Mapping, Applied, Failed }

    /// <summary>Outcome of an import. <see cref="Value"/> is default on failure.</summary>
    public readonly struct ImportResult<T>
    {
        public readonly bool Success;
        public readonly T Value;
        public readonly string Error;          // null on success
        public readonly List<string> Warnings; // non-fatal items (dropped assets, fallbacks), may be null

        private ImportResult(bool ok, T value, string error, List<string> warnings)
        {
            Success = ok; Value = value; Error = error; Warnings = warnings;
        }

        public static ImportResult<T> Ok(T value, List<string> warnings) =>
            new ImportResult<T>(true, value, null, warnings);

        public static ImportResult<T> Fail(string error) =>
            new ImportResult<T>(false, default, error, null);
    }
}
