namespace Shift9.Presentation
{
    /// <summary>Pure formatting for the scoreboard bug — game clock and quarter labels.</summary>
    public static class ScoreboardFormat
    {
        /// <summary>Game clock: "M:SS" at a minute or more, tenths of a second under a minute.</summary>
        public static string FormatClock(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            if (seconds >= 60f)
            {
                int m = (int)(seconds / 60f);
                int s = (int)(seconds % 60f);
                return m + ":" + s.ToString("00");
            }
            return seconds.ToString("0.0");
        }

        public static string QuarterLabel(int quarter)
        {
            switch (quarter)
            {
                case 1: return "1ST";
                case 2: return "2ND";
                case 3: return "3RD";
                case 4: return "4TH";
                default: return quarter > 4 ? "OT" + (quarter - 4) : "1ST";
            }
        }
    }
}
