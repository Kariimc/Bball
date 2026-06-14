using Shift9.Sim.Match;
using UnityEngine;

namespace Shift9.Presentation
{
    /// <summary>
    /// Broadcast-style scoreboard bug matching the reference ESPN layout: network block, team
    /// color tabs + abbreviations + scores, a period / clock-pill / shot-clock cluster, and a
    /// series subtitle line. Immediate-mode GUI, so it needs no Canvas, fonts, or UI package.
    ///
    /// Team abbreviations and colors are settable (<see cref="SetTeams"/>) so the customization
    /// layer can drive them from imported team data. Logo IMAGES would need the customization asset
    /// pipeline and are not drawn here — color tabs stand in for now.
    /// </summary>
    public sealed class ScoreboardHud : MonoBehaviour
    {
        [SerializeField] private GameView _gameView;
        [SerializeField] private string _networkTag = "ESPN";
        [SerializeField] private string _homeAbbrev = "HOME";
        [SerializeField] private string _awayAbbrev = "AWAY";
        [SerializeField] private Color _homeColor = new Color(0.10f, 0.22f, 0.70f);
        [SerializeField] private Color _awayColor = new Color(0.55f, 0.57f, 0.60f);
        [SerializeField] private string _subtitle = "";

        // Palette
        private static readonly Color BarDark = new Color(0.07f, 0.08f, 0.10f, 0.96f);
        private static readonly Color BarPanel = new Color(0.12f, 0.13f, 0.16f, 0.96f);
        private static readonly Color NetworkRed = new Color(0.78f, 0.10f, 0.12f, 1f);
        private static readonly Color PillLight = new Color(0.90f, 0.91f, 0.93f, 1f);
        private static readonly Color Amber = new Color(0.98f, 0.74f, 0.16f, 1f);

        private GUIStyle _abbrev, _score, _period, _clock, _shot, _network, _sub;
        private Texture _homeLogo, _awayLogo;

        /// <summary>Configure the bug from team data (customization hook).</summary>
        public void SetTeams(string homeAbbrev, Color homeColor, string awayAbbrev, Color awayColor)
        {
            _homeAbbrev = homeAbbrev;
            _homeColor = homeColor;
            _awayAbbrev = awayAbbrev;
            _awayColor = awayColor;
        }

        /// <summary>Supply loaded team logo textures (null falls back to the color tab).</summary>
        public void SetLogos(Texture homeLogo, Texture awayLogo)
        {
            _homeLogo = homeLogo;
            _awayLogo = awayLogo;
        }

        public void SetSubtitle(string subtitle) => _subtitle = subtitle;

        private void EnsureStyles()
        {
            if (_abbrev != null) return;
            _abbrev = Bold(18, TextAnchor.MiddleLeft);
            _score = Bold(26, TextAnchor.MiddleCenter);
            _period = Bold(15, TextAnchor.MiddleCenter);
            _clock = Bold(20, TextAnchor.MiddleCenter);
            _shot = Bold(18, TextAnchor.MiddleCenter);
            _network = Bold(20, TextAnchor.MiddleCenter);
            _sub = Bold(13, TextAnchor.MiddleLeft);
        }

        private static GUIStyle Bold(int size, TextAnchor anchor) =>
            new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = FontStyle.Bold, alignment = anchor };

        private void OnGUI()
        {
            GameSim g = _gameView != null ? _gameView.Sim : null;
            if (g == null) return;
            EnsureStyles();

            // Segment widths (px), laid out left to right.
            const float net = 58f, tab = 6f, abbr = 52f, sc = 54f, per = 46f, pill = 78f, shotW = 42f;
            const float barH = 44f;
            bool hasSub = !string.IsNullOrEmpty(_subtitle);
            float subH = hasSub ? 20f : 0f;

            // A team logo (when loaded) replaces the thin color tab with a square.
            float homeLead = _homeLogo != null ? barH : tab;
            float awayLead = _awayLogo != null ? barH : tab;

            float w = net + (homeLead + abbr + sc) + (awayLead + abbr + sc) + per + pill + shotW;
            float x = 16f;
            float y = Screen.height - barH - subH - 16f;

            FillRect(new Rect(x, y, w, barH), BarDark);

            float cx = x;
            // Network block
            FillRect(new Rect(cx, y, net, barH), NetworkRed);
            Text(new Rect(cx, y, net, barH), _networkTag, _network, Color.white);
            cx += net;

            // Home / Away: logo (if loaded) or color tab, then abbrev + score
            cx = TeamCell(cx, y, barH, homeLead, abbr, sc, _homeColor, _homeLogo, _homeAbbrev, g.HomeScore);
            cx = TeamCell(cx, y, barH, awayLead, abbr, sc, _awayColor, _awayLogo, _awayAbbrev, g.AwayScore);

            // Period + clock pill + shot clock
            FillRect(new Rect(cx, y, per + pill + shotW, barH), BarPanel);
            Text(new Rect(cx, y, per, barH), ScoreboardFormat.QuarterLabel(g.Quarter), _period, Color.white);
            cx += per;

            var pillRect = new Rect(cx + 4f, y + 8f, pill - 8f, barH - 16f);
            FillRect(pillRect, PillLight);
            Text(pillRect, ScoreboardFormat.FormatClock(g.GameTimeRemaining), _clock, BarDark);
            cx += pill;

            int shotClock = Mathf.CeilToInt(Mathf.Max(0f, g.ShotClockRemaining));
            Text(new Rect(cx, y, shotW, barH), shotClock.ToString(), _shot, Amber);

            // Subtitle (series/context) row
            if (hasSub)
            {
                FillRect(new Rect(x, y + barH, w, subH), BarPanel);
                Text(new Rect(x + 10f, y + barH, w - 20f, subH), _subtitle, _sub, new Color(0.85f, 0.86f, 0.88f));
            }
        }

        private float TeamCell(float cx, float y, float h, float lead, float abbr, float sc,
            Color teamColor, Texture logo, string abbrevText, int scoreValue)
        {
            if (logo != null)
                GUI.DrawTexture(new Rect(cx, y, lead, h), logo, ScaleMode.ScaleToFit);
            else
                FillRect(new Rect(cx, y, lead, h), teamColor);
            cx += lead;
            Text(new Rect(cx + 8f, y, abbr - 8f, h), abbrevText, _abbrev, Color.white);
            cx += abbr;
            Text(new Rect(cx, y, sc, h), scoreValue.ToString(), _score, Color.white);
            return cx + sc;
        }

        private static void FillRect(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private static void Text(Rect r, string s, GUIStyle style, Color color)
        {
            Color prev = GUI.contentColor;
            GUI.contentColor = color;
            GUI.Label(r, s, style);
            GUI.contentColor = prev;
        }
    }
}
