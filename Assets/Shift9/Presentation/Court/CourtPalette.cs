using UnityEngine;

namespace Shift9.Presentation.Court
{
    /// <summary>
    /// Court colors, defaulted literally to the references: maple floor, BLACK painted key,
    /// white lines, clear glass backboard with white square, orange rim, white net, and a
    /// State-Farm-red stanchion pad. Paint color is exposed because it is team/arena dependent
    /// (the customization layer can drive it).
    /// </summary>
    [System.Serializable]
    public struct CourtPalette
    {
        public Color Floor;
        public Color Paint;        // the key fill — black per the on-screen reference
        public Color Line;
        public Color BackboardGlass;
        public Color BackboardSquare;
        public Color Rim;
        public Color Net;
        public Color Stanchion;
        public Color StanchionPad;

        public static CourtPalette Default => new CourtPalette
        {
            Floor = new Color(0.82f, 0.66f, 0.42f),       // light maple
            Paint = Color.black,
            Line = Color.white,
            BackboardGlass = new Color(0.85f, 0.9f, 0.95f, 0.25f), // clear, faint blue-white
            BackboardSquare = Color.white,
            Rim = new Color(0.95f, 0.45f, 0.1f),          // orange
            Net = Color.white,
            Stanchion = new Color(0.15f, 0.15f, 0.16f),   // dark padding
            StanchionPad = new Color(0.78f, 0.1f, 0.12f)  // State Farm red
        };
    }
}
