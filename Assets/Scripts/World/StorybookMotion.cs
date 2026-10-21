using UnityEngine;

namespace VN.World
{
    /// <summary>给童话资产加一点“绘本活着”的轻微运动，不依赖 Animator。</summary>
    public class StorybookMotion : MonoBehaviour
    {
        public enum Mode { Float, Glow, Sway }
        public Mode mode = Mode.Sway;
        public float amount = 0.08f;
        public float speed = 1.4f;

        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Light _light;
        private float _seed;

        private void Awake()
        {
            _startPosition = transform.localPosition;
            _startRotation = transform.localRotation;
            _light = GetComponent<Light>();
            _seed = Mathf.Abs(name.GetHashCode() % 1000) * 0.013f;
        }

        private void Update()
        {
            float wave = Mathf.Sin(Time.time * speed + _seed);
            switch (mode)
            {
                case Mode.Float:
                    transform.localPosition = _startPosition + Vector3.up * (wave * amount);
                    transform.localRotation = _startRotation * Quaternion.Euler(0f, Time.time * 12f, wave * 2f);
                    break;
                case Mode.Glow:
                    if (_light != null) _light.intensity = 1.05f + wave * 0.22f;
                    break;
                case Mode.Sway:
                    transform.localRotation = _startRotation * Quaternion.Euler(0f, 0f, wave * amount * 15f);
                    break;
            }
        }
    }
}
