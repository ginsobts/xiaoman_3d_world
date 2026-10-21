using UnityEngine;

namespace VN.World
{
    /// <summary>UV 滚动（海面、流云）。</summary>
    public class UVScroll : MonoBehaviour
    {
        public Vector2 speed = new Vector2(0.01f, 0.004f);
        private Renderer _renderer;
        private Material _instance;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            if (_renderer != null) _instance = _renderer.material;
        }

        private void Update()
        {
            if (_instance == null) return;
            _instance.mainTextureOffset += speed * Time.deltaTime;
        }
    }
}
