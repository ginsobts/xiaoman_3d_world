using UnityEngine;

namespace VN.World
{
    /// <summary>标记场景里的参考道具实例，编辑器可据此把占位模型一键替换成 AI 生成模型。</summary>
    public class GeneratedPropMarker : MonoBehaviour
    {
        public string propId;
        public bool isPlaceholder;
    }
}
