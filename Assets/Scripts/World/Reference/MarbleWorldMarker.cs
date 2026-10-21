using UnityEngine;
using UnityEngine.Rendering;

namespace VN.World
{
    /// <summary>
    /// 挂在 Resources/AI3D/World_&lt;sceneId&gt;.prefab 根上：这是一个由 World Labs Marble 生成的高斯泼溅环境。
    /// 子物体 Splat 已按 metricScale / groundOffset 换算成米并把地面放在 y=0；子物体 Collider 是同坐标的碰撞网格。
    /// </summary>
    public class MarbleWorldMarker : MonoBehaviour
    {
        public string sceneId;
        public string worldId;
        [TextArea] public string caption;
        public float metricScale = 1f;
        public float groundOffset;
        public int splatCount;
        /// <summary>传送到达点（本地坐标）。Marble 以输入图片的相机位置为原点，所以默认就站在原点看向 +Z。</summary>
        public Vector3 spawnLocal = Vector3.zero;
        public float spawnYaw;

        /// <summary>高斯泼溅渲染需要 compute + D3D12 / Vulkan / Metal；不满足时回退到程序化环境。</summary>
        public static bool RendererSupported
        {
            get
            {
                if (!SystemInfo.supportsComputeShaders) return false;
                switch (SystemInfo.graphicsDeviceType)
                {
                    case GraphicsDeviceType.Direct3D12:
                    case GraphicsDeviceType.Vulkan:
                    case GraphicsDeviceType.Metal:
                        return true;
                    default:
                        return false;
                }
            }
        }
    }
}
