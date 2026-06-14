using Shift9.Sim.Core;
using Shift9.Sim.Match;
using UnityEngine;

namespace Shift9.Presentation
{
    /// <summary>
    /// Plays a continuous game: owns a deterministic <see cref="GameSim"/>, spawns ten capsules and
    /// a ball, and drives their transforms (and team colors) from the sim each frame. The sim is
    /// stepped at the fixed simulation rate via an accumulator, so it stays deterministic regardless
    /// of display frame rate. Drop on an empty GameObject in a scene with the court + broadcast camera.
    /// </summary>
    public sealed class GameView : MonoBehaviour
    {
        [SerializeField] private ulong _seed = 12345UL;
        [SerializeField] private float _quarterLength = 720f;
        [SerializeField] private int _quarters = 4;
        [SerializeField] private Color _homeColor = new Color(0.10f, 0.22f, 0.70f);
        [SerializeField] private Color _awayColor = new Color(0.90f, 0.90f, 0.92f);
        [SerializeField] private Color _ballColor = new Color(0.95f, 0.45f, 0.10f);
        [SerializeField] private float _playerHeight = 6.5f;
        [SerializeField] private float _playerWidth = 2.0f;

        private GameSim _sim;
        private Transform[] _playerViews;
        private Renderer[] _playerRenderers;
        private Transform _ballView;
        private Material _homeMat;
        private Material _awayMat;
        private float _accumulator;

        private void Start()
        {
            _sim = new GameSim(_seed, _quarterLength, _quarters);
            _homeMat = MaterialUtil.Solid(_homeColor);
            _awayMat = MaterialUtil.Solid(_awayColor);
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
            int count = _sim.PlayerCount;
            _playerViews = new Transform[count];
            _playerRenderers = new Renderer[count];
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "Player_" + i;
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(_playerWidth, _playerHeight * 0.5f, _playerWidth);
                _playerViews[i] = go.transform;
                _playerRenderers[i] = go.GetComponent<Renderer>();
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
                SimPlayerState s = _sim.GetPlayer(i);
                _playerViews[i].localPosition = new Vector3(s.Position.x, _playerHeight * 0.5f, s.Position.z);
                if (_playerRenderers[i] != null)
                    _playerRenderers[i].sharedMaterial = s.IsHomeTeam ? _homeMat : _awayMat;
            }
            _ballView.localPosition = _sim.BallPosition;
        }
    }
}
