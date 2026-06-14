using System;
using System.Text;
using Shift9.Customization.Mapping;
using Shift9.Presentation;
using UnityEngine;

namespace Shift9.Integration
{
    /// <summary>
    /// Pushes imported team identity (name → abbreviation, primary color) into the scoreboard bug.
    /// The seam where the customization data meets the presentation HUD.
    /// </summary>
    public static class ScoreboardBinder
    {
        public static void Apply(ScoreboardHud hud, RuntimeTeam home, RuntimeTeam away)
        {
            if (hud == null) return;
            hud.SetTeams(
                Abbreviate(home?.Name), home != null ? (Color)home.Primary : Color.gray,
                Abbreviate(away?.Name), away != null ? (Color)away.Primary : Color.gray);
        }

        /// <summary>
        /// A broadcast-style 2-3 letter abbreviation: initials of a multi-word name
        /// ("New York" → "NY", "San Antonio" → "SA"), or the first three letters of a single word
        /// ("Lakers" → "LAK").
        /// </summary>
        public static string Abbreviate(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";

            string[] words = name.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 1)
            {
                string w = Letters(words[0]);
                return (w.Length <= 3 ? w : w.Substring(0, 3)).ToUpperInvariant();
            }

            var sb = new StringBuilder();
            for (int i = 0; i < words.Length && sb.Length < 3; i++)
            {
                string w = Letters(words[i]);
                if (w.Length > 0) sb.Append(w[0]);
            }
            return sb.ToString().ToUpperInvariant();
        }

        private static string Letters(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
                if (char.IsLetter(c)) sb.Append(c);
            return sb.ToString();
        }
    }
}
