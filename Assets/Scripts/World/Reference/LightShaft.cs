using UnityEngine;

namespace VN.World
{
    /// <summary>光柱：围绕自身竖轴朝向相机，叠加呼吸，模拟体积光。</summary>
    public class LightShaft : MonoBehaviour
    {
        public float flicker = 0.08f;
        private Renderer _renderer;
        private Material _instance;
        private Color _base;
        private float _seed;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
            {
                _instance = _renderer.material;
                if (_instance.HasProperty("_TintColor")) _base = _instance.GetColor("_TintColor");
                else _base = _instance.color;
            }
            _seed = Random.value * 20f;
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 toCam = cam.transform.position - transform.position;
            Vector3 axis = transform.up;
            Vector3 flat = Vector3.ProjectOnPlane(toCam, axis);
            if (flat.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(-flat.normalized, axis);
            if (_instance != null && flicker > 0f)
            {
                float f = 1f + Mathf.Sin(Time.time * 0.7f + _seed) * flicker;
                if (_instance.HasProperty("_TintColor")) _instance.SetColor("_TintColor", _base * f);
                else _instance.color = _base * f;
            }
        }
    }
}
