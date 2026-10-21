using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VN.World;

namespace VN.EditorTools
{
    /// <summary>
    /// 3D 世界的编辑器辅助菜单：
    ///  - 在当前场景生成原型关卡（白盒），之后可以直接在 Hierarchy 里改位置、换模型、调 Interactable 参数；
    ///  - 新建一个独立的 World3D 场景并加入 Build Settings。
    /// 运行时若场景里没有 WorldLevel，WorldController 也会自动搭同样的原型，所以这两步都不是必须的。
    /// </summary>
    public static class WorldSceneTools
    {
        private const string ScenePath = "Assets/Scenes/World3D.unity";

        // 第一次升级到 3D 框架时自动生成一个真正可编辑的场景资产。
        // 用 Additive 临时场景生成，不会关闭或覆盖用户当前正在编辑的场景。
        [InitializeOnLoadMethod]
        private static void EnsureEditableWorldScene()
        {
            if (WorldSceneExists()) return;
            EditorApplication.delayCall += TryCreateEditableWorldScene;
        }

        private static void TryCreateEditableWorldScene()
        {
            if (WorldSceneExists()) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged += CreateAfterPlayMode;
                return;
            }
            CreateWorldSceneAsset();
        }

        private static void CreateAfterPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= CreateAfterPlayMode;
            EditorApplication.delayCall += TryCreateEditableWorldScene;
        }

        [MenuItem("3D世界/在当前场景生成原型关卡")]
        public static void BuildPrototypeInCurrentScene()
        {
            var level = Object.FindObjectOfType<WorldLevel>();
            if (level != null && level.transform.childCount > 0)
            {
                if (!EditorUtility.DisplayDialog("已有关卡",
                        "当前场景里已经有 WorldLevel 且不为空。\n要清空并重新生成原型关卡吗？", "重新生成", "取消"))
                    return;
                for (int i = level.transform.childCount - 1; i >= 0; i--)
                    Undo.DestroyObjectImmediate(level.transform.GetChild(i).gameObject);
            }

            if (level == null)
            {
                var go = new GameObject("WorldLevel");
                Undo.RegisterCreatedObjectUndo(go, "Create WorldLevel");
                level = go.AddComponent<WorldLevel>();
            }

            LevelBuilder.BuildPrototype(level);
            level.autoBuildPrototype = false;   // 已经有内容了，运行时不再自动搭
            EditorUtility.SetDirty(level);
            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            Selection.activeGameObject = level.gameObject;
            Debug.Log("[3D世界] 原型关卡已生成，可直接在场景里编辑。记得 Ctrl+S 保存场景。");
        }

        [MenuItem("3D世界/新建 World3D 场景")]
        public static void CreateWorldScene()
        {
            CreateWorldSceneInternal(false);
        }

        /// <summary>CI / 命令行入口：无对话框地重建可编辑场景。</summary>
        public static void CreateWorldSceneBatch()
        {
            CreateWorldSceneAsset();
        }

        private static void CreateWorldSceneInternal(bool force)
        {
            if (!force && WorldSceneExists() &&
                !EditorUtility.DisplayDialog("场景已存在", ScenePath + " 已存在，要覆盖吗？", "覆盖", "取消"))
                return;

            if (!force && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.nearClipPlane = 0.1f;
            }

            var levelGO = new GameObject("WorldLevel");
            var level = levelGO.AddComponent<WorldLevel>();
            LevelBuilder.BuildPrototype(level);
            level.autoBuildPrototype = false;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            // 加入 Build Settings（放到最前，作为启动场景）
            AddWorldToBuildSettings();

            Debug.Log("[3D世界] 已创建 " + ScenePath + " 并设为启动场景。");
        }

        private static void CreateWorldSceneAsset()
        {
            Scene previous = SceneManager.GetActiveScene();
            // Unity 不允许在“尚未保存的 Untitled 场景”旁边新建 Additive 场景。
            // 批处理没有用户场景需要保护，直接用 Single；交互编辑器则等待用户先保存当前场景。
            if (!Application.isBatchMode && string.IsNullOrEmpty(previous.path))
            {
                Debug.Log("[3D世界] 当前场景尚未保存；保存后可自动生成 World3D，或使用菜单手动创建。");
                return;
            }
            bool additive = !Application.isBatchMode;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,
                additive ? NewSceneMode.Additive : NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            try
            {
                var cam = scene.GetRootGameObjects()
                    .Select(go => go.GetComponentInChildren<Camera>())
                    .FirstOrDefault(found => found != null);
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.Skybox;
                    cam.nearClipPlane = 0.1f;
                }

                var levelGO = new GameObject("WorldLevel");
                var level = levelGO.AddComponent<WorldLevel>();
                LevelBuilder.BuildPrototype(level);
                level.autoBuildPrototype = false;

                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                EditorSceneManager.SaveScene(scene, ScenePath);
                AddWorldToBuildSettings();
                AssetDatabase.Refresh();
                Debug.Log("[3D世界] 已自动生成可自由编辑的场景：" + ScenePath);
            }
            finally
            {
                if (additive)
                {
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void AddWorldToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        [MenuItem("3D世界/打开可编辑 World3D 场景")]
        public static void OpenWorldScene()
        {
            if (!WorldSceneExists())
            {
                CreateWorldScene();
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static bool WorldSceneExists()
        {
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
        }

        [MenuItem("3D世界/删除世界存档")]
        public static void DeleteWorldSave()
        {
            WorldState.DeleteSave();
            Debug.Log("[3D世界] 已删除世界存档（" + Path.Combine(Application.persistentDataPath, "world_save.json") + "）。");
        }

        [MenuItem("3D世界/打开剧本 world.json")]
        public static void OpenWorldStory()
        {
            string path = Path.Combine(Application.streamingAssetsPath, GameConfig.WorldStoryFile);
            if (File.Exists(path)) EditorUtility.OpenWithDefaultApp(path);
            else Debug.LogWarning("[3D世界] 找不到 " + path);
        }
    }
}
