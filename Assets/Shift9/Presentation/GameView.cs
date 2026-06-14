using Shift9.Sim.Core;
using Shift9.Sim.Match;
using UnityEngine;

namespace Shift9.Presentation
{
    /// <summary>
    /// Plays a live possession: owns a deterministic <see cref="PossessionSim"/>, spawns ten
    /// team-colored capsules and a ball, and drives their transforms from the sim each frame.
    /// The sim is stepped at the fixed simulation rate via an accumulator (so it stays
    /// deterministic regardless of the display frame rate); the visuals snap to the latest state.
    ///
    /// Drop this on an empty GameObject in a scene that also has the court + broadcast camera.
    /// </summary>
    public sealed class GameView : MonoBehaviour
    {
        [SerializeField] private ulong _seed = 12345UL;
        [SerializeField] private Color _homeColor = new Color(0.10f, 0.22f, 0.70f);
        [SerializeField] private Color _awayColor = new Color(0.90f, 0.90f, 0.92f);
        [SerializeField] private Color _ballColor = new Color(0.95f, 0.45f, 0.10f);
        [SerializeField] private float _playerHeight = 6.5f;
        [SerializeField] private float _playerWidth = 2.0f;

        private PossessionSim _sim;
        private Transform[] _playerViews;
        private Transform _ballView;
        private float _accumulator;

        private void Start()
        {
            _sim = new PossessionSim(_seed);
            BuildVisuals();
            Sync();
        }

        private void Update()
        {
            if (_sim == null) return;

            _accumulator += Time.deltaTime;
            float dt = SimConstants.FixedTimestep;
            int guard = 0;
            while (_accumulator >= dt && guard++ < 8)
            {
                _sim.Tick(dt);
                _accumulator -= dt;
            }
            Sync();
        }

        private void BuildVisuals()
        {
            _playerViews = new Transform[_sim.PlayerCount];
            for (int i = 0; i < _sim.PlayerCount; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = (_sim.GetPlayer(i).IsOffense ? "Home_" : "Away_") + i;
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(_playerWidth, _playerHeight * 0.5f, _playerWidth);

                var r = go.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = MaterialUtil.Solid(_sim.GetPlayer(i).IsOffense ? _homeColor : _awayColor);
                _playerViews[i] = go.transform;
            }

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Ball";
            ball.transform.SetParent(transform, false);
            float d = SimConstants.BallRadius * 2f;
            ball.transform.localScale = new Vector3(d, d, d);
            var br = ball.GetComponent<Renderer>();
            if (br != null) br.sharedMaterial = MaterialUtil.Solid(_ballColor);
            _ballView = ball.transform;
        }

        private void Sync()
        {
            for (int i = 0; i < _playerViews.Length; i++)
            {
                Vector3 p = _sim.GetPlayer(i).Position;
                _playerViews[i].localPosition = new Vector3(p.x, _playerHeight * 0.5f, p.z);
            }
            _ballView.localPosition = _sim.BallPosition;
        }
    }
}
