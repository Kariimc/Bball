using UnityEngine;

namespace Shift9.Presentation.Players
{
    /// <summary>
    /// Spawns ten capsule figures (5 v 5) in team colors at the showcase formation, so the court
    /// reads as a live game from the broadcast camera. A blockout — capsules now, rigged player
    /// meshes later. Colors default to the references (royal-blue home, light-grey away) and are
    /// exposed for the customization layer to drive.
    /// </summary>
    public sealed class PlayerBuilder : MonoBehaviour
    {
        [SerializeField] private Color _homeColor = new Color(0.10f, 0.22f, 0.70f); // royal blue
        [SerializeField] private Color _awayColor = new Color(0.90f, 0.90f, 0.92f); // light grey/white
        [SerializeField] private float _playerHeight = 6.5f;
        [SerializeField] private float _playerWidth = 2.0f;
        [SerializeField] private bool _buildOnAwake = true;

        private void Awake()
        {
            if (_buildOnAwake) Build();
        }

        [ContextMenu("Build Players")]
        public void Build()
        {
            Clear();
            Material home = MaterialUtil.Solid(_homeColor);
            Material away = MaterialUtil.Solid(_awayColor);

            var offense = PlayerPlacement.Offense();
            var defense = PlayerPlacement.Defense();
            for (int i = 0; i < offense.Length; i++) Spawn($"Home_{i}", offense[i], home);
            for (int i = 0; i < defense.Length; i++) Spawn($"Away_{i}", defense[i], away);
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        private void Spawn(string name, Vector3 floorPos, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(_playerWidth, _playerHeight * 0.5f, _playerWidth);
            go.transform.localPosition = new Vector3(floorPos.x, _playerHeight * 0.5f, floorPos.z);

            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = material;
        }
    }
}
