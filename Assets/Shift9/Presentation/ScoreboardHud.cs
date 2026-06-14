using Shift9.Sim.Match;
using UnityEngine;

namespace Shift9.Presentation
{
    /// <summary>
    /// Draws a broadcast-style scoreboard bug (team scores, quarter, game clock, shot clock) from
    /// the live <see cref="GameSim"/>. Uses immediate-mode GUI so it needs no Canvas, font assets,
    /// or UI package. Assign the GameView; team abbreviations are exposed for the customization layer.
    /// </summary>
    public sealed class ScoreboardHud : MonoBehaviour
    {
        [SerializeField] private GameView _gameView;
        [SerializeField] private string _homeAbbrev = "HOME";
        [SerializeField] private string _awayAbbrev = "AWAY";

        private GUIStyle _team;
        private GUIStyle _score;
        private GUIStyle _meta;

        private void EnsureStyles()
        {
            if (_team != null) return;
            _team = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _score = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            _meta = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        private void OnGUI()
        {
            GameSim g = _gameView != null ? _gameView.Sim : null;
            if (g == null) return;
            EnsureStyles();

            const float w = 580f, h = 48f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - h - 14f;
            GUI.Box(new Rect(x, y, w, h), GUIContent.none);

            // Home | Away | Quarter + clock | shot clock
            float cell = w / 4f;
            Row(x, y, h, cell, _homeAbbrev, g.HomeScore);
            Row(x + cell, y, h, cell, _awayAbbrev, g.AwayScore);

            GUI.Label(new Rect(x + cell * 2f, y + 4f, cell, h * 0.5f),
                ScoreboardFormat.QuarterLabel(g.Quarter), _meta);
            GUI.Label(new Rect(x + cell * 2f, y + h * 0.45f, cell, h * 0.5f),
                ScoreboardFormat.FormatClock(g.GameTimeRemaining), _meta);

            GUI.Label(new Rect(x + cell * 3f, y, cell, h),
                Mathf.CeilToInt(Mathf.Max(0f, g.ShotClockRemaining)).ToString(), _score);
        }

        private void Row(float x, float y, float h, float cell, string abbrev, int score)
        {
            GUI.Label(new Rect(x + 12f, y, cell * 0.6f, h), abbrev, _team);
            GUI.Label(new Rect(x + cell * 0.45f, y, cell * 0.5f, h), score.ToString(), _score);
        }
    }
}
