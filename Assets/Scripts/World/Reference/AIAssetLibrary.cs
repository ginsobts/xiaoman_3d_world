using UnityEngine;

namespace VN.World
{
    /// <summary>
    /// 道具实例化入口：优先使用 AI 生成并导入到 Resources/AI3D/&lt;propId&gt;.prefab 的模型，
    /// 没有时退回 ReferenceProps 的程序化版本。两者尺寸均以米为单位、脚底为原点，可以无缝互换。
    /// </summary>
    public static class AIAssetLibrary
    {
        public const string ResourcesFolder = "AI3D";

        public static bool HasGenerated(string propId)
        {
            return Resources.Load<GameObject>(ResourcesFolder + "/" + propId) != null;
        }

        public static GameObject Instantiate(string propId, Transform parent, Vector3 localPosition, float yaw, float scale = 1f)
        {
            var prefab = Resources.Load<GameObject>(ResourcesFolder + "/" + propId);
            if (prefab != null)
            {
                var go = Object.Instantiate(prefab, parent);
                go.name = "AI_" + propId;
                go.transform.localPosition = localPosition;
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                go.transform.localScale = Vector3.one * scale;
                var marker = go.GetComponent<GeneratedPropMarker>();
                if (marker == null) marker = go.AddComponent<GeneratedPropMarker>();
                marker.propId = propId;
                return go;
            }
            var fallback = ReferenceProps.Build(propId, parent, localPosition, yaw, scale);
            var fm = fallback.AddComponent<GeneratedPropMarker>();
            fm.propId = propId;
            fm.isPlaceholder = true;
            return fallback;
        }
    }
}
