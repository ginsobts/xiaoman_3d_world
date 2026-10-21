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

        public static string WorldPrefabName(string sceneId) => "World_" + sceneId;

        /// <summary>这个场景是否有 Marble 泼溅环境（且当前图形 API 能渲染它）。</summary>
        public static bool HasWorld(string sceneId)
        {
            if (!MarbleWorldMarker.RendererSupported) return false;
            return Resources.Load<GameObject>(ResourcesFolder + "/" + WorldPrefabName(sceneId)) != null;
        }

        public static bool HasWorldPrefab(string sceneId)
        {
            return Resources.Load<GameObject>(ResourcesFolder + "/" + WorldPrefabName(sceneId)) != null;
        }

        public static MarbleWorldMarker WorldMarker(string sceneId)
        {
            var prefab = Resources.Load<GameObject>(ResourcesFolder + "/" + WorldPrefabName(sceneId));
            return prefab != null ? prefab.GetComponent<MarbleWorldMarker>() : null;
        }

        public static GameObject InstantiateWorld(string sceneId, Transform parent)
        {
            var prefab = Resources.Load<GameObject>(ResourcesFolder + "/" + WorldPrefabName(sceneId));
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent);
            go.name = "MarbleWorld_" + sceneId;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
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
