using System.Collections.Generic;
using UnityEngine;

namespace VN.World
{
    /// <summary>
    /// 把 LightingPreset 应用到场景：太阳、RenderSettings、相机背景与后期。
    /// 支持平滑过渡；LightingZone 进入时调用 BlendTo。
    /// </summary>
    public class LightingDirector : MonoBehaviour
    {
        public static LightingDirector Instance { get; private set; }

        public LightingPreset Current { get; private set; } = LightingPreset.Village();

        private LightingPreset _from;
        private LightingPreset _to;
        private float _blend = 1f;
        private float _duration = 1.2f;
        private Light _sun;
        private Camera _camera;
        private FairytalePostProcess _post;
        private readonly List<LightingZone> _zones = new List<LightingZone>();

        private void Awake()
        {
            Instance = this;
        }

        public void Init(Camera camera)
        {
            _camera = camera;
            _sun = FindSun();
            if (_camera != null)
            {
                _post = _camera.GetComponent<FairytalePostProcess>();
                if (_post == null) _post = _camera.gameObject.AddComponent<FairytalePostProcess>();
            }
            _zones.Clear();
            _zones.AddRange(FindObjectsOfType<LightingZone>());
            RenderQuality.Apply(_camera, _sun);
            Apply(Current);
        }

        public void RegisterZone(LightingZone zone)
        {
            if (zone != null && !_zones.Contains(zone)) _zones.Add(zone);
        }

        /// <summary>按位置立即套用所在区域的光照（出生 / 传送 / 读档后调用）。</summary>
        public void ApplyForPosition(Vector3 position)
        {
            LightingPreset preset = null;
            foreach (var zone in _zones)
                if (zone != null && zone.Contains(position)) { preset = zone.Preset; break; }
            if (preset == null) preset = LightingPreset.Village();
            SetImmediate(preset);
        }

        public void SetImmediate(LightingPreset preset)
        {
            if (preset == null) return;
            _from = _to = preset;
            _blend = 1f;
            Apply(preset);
        }

        public void BlendTo(LightingPreset preset, float duration = 1.2f)
        {
            if (preset == null) return;
            if (_to != null && _to.id == preset.id && _blend >= 1f) return;
            _from = Current.Clone();
            _to = preset;
            _blend = 0f;
            _duration = Mathf.Max(0.05f, duration);
        }

        private void Update()
        {
            if (_blend >= 1f || _to == null) return;
            _blend = Mathf.Min(1f, _blend + Time.unscaledDeltaTime / _duration);
            float eased = Mathf.SmoothStep(0f, 1f, _blend);
            Apply(LightingPreset.Lerp(_from, _to, eased));
        }

        private void Apply(LightingPreset p)
        {
            Current = p;
            if (_sun == null) _sun = FindSun();
            if (_sun != null)
            {
                _sun.transform.rotation = Quaternion.Euler(p.sunEuler);
                _sun.color = p.sunColor;
                _sun.intensity = p.sunIntensity;
                _sun.shadowStrength = p.shadowStrength;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.ambientSky;
            RenderSettings.ambientEquatorColor = p.ambientEquator;
            RenderSettings.ambientGroundColor = p.ambientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = p.fogColor;
            RenderSettings.fogStartDistance = p.fogStart;
            RenderSettings.fogEndDistance = p.fogEnd;
            if (_camera != null) _camera.backgroundColor = p.background;
            if (_post != null) _post.Current = p;
        }

        private static Light FindSun()
        {
            Light best = null;
            foreach (var light in FindObjectsOfType<Light>())
                if (light.type == LightType.Directional && (best == null || light.intensity > best.intensity))
                    best = light;
            return best;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

}
