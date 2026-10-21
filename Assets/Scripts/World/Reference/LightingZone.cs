using UnityEngine;

namespace VN.World
{
    /// <summary>
    /// 进入体积时切换光照配方。挂在带 Trigger Collider 的物体上。
    /// presetId 用于场景序列化后仍能找回预设（LightingPreset 本身也会序列化，作为自定义覆盖）。
    /// </summary>
    public class LightingZone : MonoBehaviour
    {
        public string presetId = "village";
        public LightingPreset Preset = LightingPreset.Village();
        public float blendSeconds = 1.2f;

        private Collider _collider;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            if (Preset == null || string.IsNullOrEmpty(Preset.id)) Preset = ReferenceSceneLibrary.PresetById(presetId);
        }

        private void Start()
        {
            LightingDirector.Instance?.RegisterZone(this);
        }

        public bool Contains(Vector3 position)
        {
            if (_collider == null) _collider = GetComponent<Collider>();
            return _collider != null && _collider.bounds.Contains(position);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            var avatar = other.GetComponent<VN.Multiplayer.MultiplayerAvatar>();
            if (avatar != null && !avatar.isLocal) return;
            LightingDirector.Instance?.BlendTo(Preset, blendSeconds);
        }
    }
}
