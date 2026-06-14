using System.Collections.Generic;

namespace Shift9.Customization.Validation
{
    /// <summary>
    /// Outcome of a validation pass. Fail-fast friendly: callers check <see cref="Ok"/> and
    /// surface <see cref="Error"/> to the UI. A small struct to avoid per-validation GC; the
    /// optional <see cref="Warnings"/> list is allocated lazily only when something is dropped.
    /// </summary>
    public readonly struct ValidationResult
    {
        public readonly bool Ok;
        public readonly string Error;          // null on success
        public readonly List<string> Warnings; // non-fatal items (e.g., a dropped bad image), may be null

        private ValidationResult(bool ok, string error, List<string> warnings)
        {
            Ok = ok; Error = error; Warnings = warnings;
        }

        public static readonly ValidationResult Success = new ValidationResult(true, null, null);

        public static ValidationResult SuccessWith(List<string> warnings) =>
            new ValidationResult(true, null, warnings);

        public static ValidationResult Fail(string reason) =>
            new ValidationResult(false, reason, null);
    }
}
