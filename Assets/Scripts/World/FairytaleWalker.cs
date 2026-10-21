using UnityEngine;

namespace VN.World
{
    /// <summary>程序化角色的轻量走路动画：摆臂、摆腿、身体起伏。</summary>
    public class FairytaleWalker : MonoBehaviour
    {
        private PlayerController _player;
        private Transform _legL, _legR, _armL, _armR;
        private Vector3 _basePosition;
        private float _phase;

        private void Awake()
        {
            _player = GetComponentInParent<PlayerController>();
            _legL = transform.Find("LegL");
            _legR = transform.Find("LegR");
            _armL = transform.Find("ArmL");
            _armR = transform.Find("ArmR");
            _basePosition = transform.localPosition;
        }

        private void LateUpdate()
        {
            float speed = _player != null ? _player.CurrentSpeed : 0f;
            float blend = Mathf.Clamp01(speed / 2f);
            _phase += Time.deltaTime * Mathf.Lerp(2f, 10f, Mathf.Clamp01(speed / 7f));
            float swing = Mathf.Sin(_phase) * 26f * blend;

            SetX(_legL, swing);
            SetX(_legR, -swing);
            SetX(_armL, -swing * 0.75f);
            SetX(_armR, swing * 0.75f);
            transform.localPosition = _basePosition + Vector3.up * (Mathf.Abs(Mathf.Sin(_phase)) * 0.045f * blend);
        }

        private static void SetX(Transform target, float angle)
        {
            if (target == null) return;
            Vector3 euler = target.localEulerAngles;
            euler.x = angle;
            target.localEulerAngles = euler;
        }
    }
}
