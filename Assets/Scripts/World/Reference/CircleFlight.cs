using UnityEngine;

namespace VN.World
{
    /// <summary>围绕中心点缓慢盘旋（海鸥群）。</summary>
    public class CircleFlight : MonoBehaviour
    {
        public Vector3 center;
        public float radius = 12f;
        public float height = 8f;
        public float speed = 0.4f;
        public float phase;
        public float bob = 1.2f;

        private void Update()
        {
            float t = Time.time * speed + phase;
            Vector3 p = center + new Vector3(Mathf.Cos(t) * radius, height + Mathf.Sin(t * 2.3f) * bob, Mathf.Sin(t) * radius);
            Vector3 forward = new Vector3(-Mathf.Sin(t), 0f, Mathf.Cos(t));
            transform.position = p;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 0f, -12f);
        }
    }
}
