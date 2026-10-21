using UnityEngine;

namespace VN.World
{
    /// <summary>海鸥扇翅。</summary>
    public class WingFlap : MonoBehaviour
    {
        public Transform left;
        public Transform right;
        private float _seed;

        private void Start() { _seed = Random.value * 10f; }

        private void Update()
        {
            float a = Mathf.Sin(Time.time * 7f + _seed) * 28f;
            if (left != null) left.localRotation = Quaternion.Euler(0f, 0f, -a);
            if (right != null) right.localRotation = Quaternion.Euler(0f, 0f, a);
        }
    }
}
