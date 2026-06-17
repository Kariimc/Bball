using Shift9.Presentation.Court;
using UnityEngine;

namespace Shift9.Presentation
{
    /// <summary>
    /// One-click scene setup: add this to a single empty GameObject, press Play, and it spawns the
    /// whole game — a directional light, the court, the live <see cref="GameView"/> (players + ball
    /// + sim), a broadcast camera, and the scoreboard bug. Everything self-wires (the camera finds
    /// the ball, the HUD finds the game), so no manual scene assembly is needed.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private ulong _seed = 12345UL;
        [SerializeField] private bool _buildOnAwake = true;

        private void Awake()
        {
            if (_buildOnAwake) Build();
        }

        public void Build()
        {
            // Sunlight so the blockout is visible.
            var sun = Child("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Court (builds on its own Awake).
            Child("Court").AddComponent<CourtBuilder>();

            // Live game: players, ball, deterministic sim.
            var game = Child("Game").AddComponent<GameView>();
            game.Seed = _seed;

            // Broadcast camera (self-wires its follow target to the ball).
            var cam = Child("BroadcastCamera");
            cam.AddComponent<Camera>();
            cam.AddComponent<BroadcastCameraRig>();
            cam.tag = "MainCamera";

            // Scoreboard bug (self-wires to the GameView).
            Child("Scoreboard").AddComponent<ScoreboardHud>();
        }

        private GameObject Child(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false); // parent before AddComponent so child Awakes see the hierarchy
            return go;
        }
    }
}
