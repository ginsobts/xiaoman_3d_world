using UnityEngine;

namespace VN.World
{
    /// <summary>把生成的蜡笔特效小图作为面向相机的世界 Sprite 播放。</summary>
    public class FairytaleVFX : MonoBehaviour
    {
        private float _life = 1f;
        private float _age;
        private Vector3 _startScale;
        private SpriteRenderer _renderer;

        public static void Spawn(string spriteName, Vector3 position, float size = 1.8f, float life = 1f)
        {
            var sprite = Resources.Load<Sprite>("Art/VFX/" + spriteName);
            if (sprite == null) return;

            var go = new GameObject("VFX_" + spriteName);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * size;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = 20;
            var fx = go.AddComponent<FairytaleVFX>();
            fx._life = life;
            fx._startScale = go.transform.localScale;
            fx._renderer = renderer;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _life);
            transform.position += Vector3.up * (Time.deltaTime * 0.35f);
            transform.localScale = _startScale * Mathf.Lerp(0.65f, 1.25f, Mathf.Sin(t * Mathf.PI));
            if (_renderer != null)
            {
                Color c = _renderer.color;
                c.a = 1f - t;
                _renderer.color = c;
            }
            if (Camera.main != null)
                transform.rotation = Camera.main.transform.rotation;
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
