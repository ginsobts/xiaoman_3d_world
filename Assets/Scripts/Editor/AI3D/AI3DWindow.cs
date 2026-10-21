using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VN.World;

namespace VN.EditorTools.AI3D
{
    /// <summary>
    /// 图生 3D 控制台：选服务商、填 API Key、勾选道具、一键生成并导入为 Resources/AI3D prefab。
    /// API Key 存在 EditorPrefs（本机），不会进入仓库。
    /// </summary>
    public class AI3DWindow : EditorWindow
    {
        private const string ProviderPref = "Xiaoman.AI3D.Provider";
        private const string ConcurrencyPref = "Xiaoman.AI3D.Concurrency";

        private int _providerIndex;
        private string _meshyKey = "";
        private string _tripoKey = "";
        private int _concurrency = 3;
        private Vector2 _scroll;
        private string _sceneFilter = "";
        private readonly Dictionary<string, bool> _selected = new Dictionary<string, bool>();
        private readonly Dictionary<string, AI3DImporter.JobState> _jobs = new Dictionary<string, AI3DImporter.JobState>();
        private readonly Queue<AI3DImporter.JobState> _queue = new Queue<AI3DImporter.JobState>();
        private string _expandedLog;

        [MenuItem("3D世界/AI 图生 3D 控制台")]
        public static void Open()
        {
            var win = GetWindow<AI3DWindow>("AI 图生 3D");
            win.minSize = new Vector2(560, 480);
        }

        [MenuItem("3D世界/在当前场景生成参考画作场景")]
        public static void BuildGalleriesInCurrentScene()
        {
            var level = Object.FindObjectOfType<WorldLevel>();
            if (level == null)
            {
                EditorUtility.DisplayDialog("没有 WorldLevel", "当前场景里没有 WorldLevel，请先用「3D世界/在当前场景生成原型关卡」。", "好");
                return;
            }
            var existing = level.transform.Find(ReferenceSceneLibrary.GalleryRootName);
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("已有画廊", "场景里已经有参考画作场景，要删除并重新生成吗？", "重新生成", "取消")) return;
                Undo.DestroyObjectImmediate(existing.gameObject);
                var portals = level.transform.Find(ReferenceSceneLibrary.PortalRowName);
                if (portals != null) Undo.DestroyObjectImmediate(portals.gameObject);
            }
            var root = ReferenceSceneBuilder.Build(level);
            Undo.RegisterCreatedObjectUndo(root, "Build reference galleries");
            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            Selection.activeGameObject = root;
        }

        [MenuItem("3D世界/用 AI 模型替换场景中的占位道具")]
        public static void ReplacePlaceholders()
        {
            int n = AI3DImporter.ReplacePlaceholdersInOpenScene();
            EditorUtility.DisplayDialog("替换完成", n > 0 ? $"已替换 {n} 个占位道具。" : "没有可替换的占位道具（请先生成 prefab）。", "好");
            if (n > 0) EditorSceneManager.MarkAllScenesDirty();
        }

        private void OnEnable()
        {
            _providerIndex = EditorPrefs.GetInt(ProviderPref, 0);
            _meshyKey = EditorPrefs.GetString(MeshyProvider.Pref, "");
            _tripoKey = EditorPrefs.GetString(TripoProvider.Pref, "");
            _concurrency = EditorPrefs.GetInt(ConcurrencyPref, 3);
            foreach (var p in ReferenceSceneLibrary.Props)
                if (p.aiCandidate && !_selected.ContainsKey(p.id)) _selected[p.id] = !AIAssetLibrary.HasGenerated(p.id);
            EditorApplication.update += Pump;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Pump;
        }

        private IImageTo3DProvider MakeProvider()
        {
            return _providerIndex == 1 ? (IImageTo3DProvider)new TripoProvider(_tripoKey) : new MeshyProvider(_meshyKey);
        }

        private string CurrentKey => _providerIndex == 1 ? _tripoKey : _meshyKey;

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("图生 3D 服务商", EditorStyles.boldLabel);
            int newProvider = GUILayout.Toolbar(_providerIndex, new[] { "Meshy 7.1（推荐）", "Tripo v3.1（备用）" });
            if (newProvider != _providerIndex) { _providerIndex = newProvider; EditorPrefs.SetInt(ProviderPref, _providerIndex); }

            EditorGUI.BeginChangeCheck();
            if (_providerIndex == 0)
            {
                _meshyKey = EditorGUILayout.PasswordField("Meshy API Key", _meshyKey);
                EditorGUILayout.HelpBox("在 https://www.meshy.ai/settings/api 创建 Key。图生 3D 每个模型约 20 Credits；Key 只保存在本机 EditorPrefs。", MessageType.Info);
            }
            else
            {
                _tripoKey = EditorGUILayout.PasswordField("Tripo API Key", _tripoKey);
                EditorGUILayout.HelpBox("在 https://platform.tripo3d.ai 创建 Key。Tripo 先生成 GLB 再自动转换为 FBX。", MessageType.Info);
            }
            _concurrency = EditorGUILayout.IntSlider("并发任务数", _concurrency, 1, 6);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(MeshyProvider.Pref, _meshyKey);
                EditorPrefs.SetString(TripoProvider.Pref, _tripoKey);
                EditorPrefs.SetInt(ConcurrencyPref, _concurrency);
            }

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("道具清单", EditorStyles.boldLabel, GUILayout.Width(80));
                _sceneFilter = EditorGUILayout.TextField(_sceneFilter, EditorStyles.toolbarSearchField);
                if (GUILayout.Button("全选", GUILayout.Width(50))) SetAll(true);
                if (GUILayout.Button("全不选", GUILayout.Width(60))) SetAll(false);
                if (GUILayout.Button("仅未生成", GUILayout.Width(70)))
                    foreach (var p in ReferenceSceneLibrary.Props.Where(p => p.aiCandidate)) _selected[p.id] = !AIAssetLibrary.HasGenerated(p.id);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            string lastScene = null;
            foreach (var spec in ReferenceSceneLibrary.Props)
            {
                if (!spec.aiCandidate) continue;
                if (!string.IsNullOrEmpty(_sceneFilter) && !spec.sceneId.Contains(_sceneFilter) && !spec.id.Contains(_sceneFilter) && !spec.displayName.Contains(_sceneFilter)) continue;
                if (spec.sceneId != lastScene)
                {
                    lastScene = spec.sceneId;
                    var sceneDef = ReferenceSceneLibrary.Scene(spec.sceneId);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField(sceneDef != null ? sceneDef.title : spec.sceneId, EditorStyles.miniBoldLabel);
                }
                DrawRow(spec);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);
            int selectedCount = _selected.Count(kv => kv.Value);
            int running = _jobs.Values.Count(j => j.running);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(CurrentKey) || selectedCount == 0))
                {
                    if (GUILayout.Button($"生成选中的 {selectedCount} 个道具", GUILayout.Height(30)))
                        Enqueue(ReferenceSceneLibrary.Props.Where(p => p.aiCandidate && _selected.TryGetValue(p.id, out var s) && s));
                }
                if (GUILayout.Button("用 AI 模型替换场景占位", GUILayout.Height(30), GUILayout.Width(170))) ReplacePlaceholders();
            }
            if (running > 0 || _queue.Count > 0)
                EditorGUILayout.HelpBox($"进行中 {running} 个，排队 {_queue.Count} 个。生成通常 2–5 分钟/个，窗口可以最小化但不要关闭 Unity。", MessageType.None);
            if (string.IsNullOrEmpty(CurrentKey))
                EditorGUILayout.HelpBox("请先填写 API Key。", MessageType.Warning);
            EditorGUILayout.Space(4);
        }

        private void DrawRow(ReferencePropSpec spec)
        {
            _jobs.TryGetValue(spec.id, out var job);
            bool generated = AIAssetLibrary.HasGenerated(spec.id);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                bool sel = _selected.TryGetValue(spec.id, out var s) && s;
                bool newSel = EditorGUILayout.Toggle(sel, GUILayout.Width(18));
                if (newSel != sel) _selected[spec.id] = newSel;

                EditorGUILayout.LabelField(spec.displayName + "  (" + spec.id + ")", GUILayout.Width(220));
                string status = job != null ? job.stage + (job.running ? $" {job.progress}%" : "") : (generated ? "已生成" : "占位模型");
                var style = new GUIStyle(EditorStyles.label);
                if (job != null && job.error != null) style.normal.textColor = new Color(0.9f, 0.3f, 0.3f);
                else if (generated || (job != null && job.done)) style.normal.textColor = new Color(0.3f, 0.7f, 0.35f);
                EditorGUILayout.LabelField(status, style, GUILayout.Width(130));

                if (job != null && job.log.Count > 0 && GUILayout.Button("日志", GUILayout.Width(40)))
                    _expandedLog = _expandedLog == spec.id ? null : spec.id;
                if (generated && GUILayout.Button("定位", GUILayout.Width(40)))
                    EditorGUIUtility.PingObject(Resources.Load<GameObject>(AIAssetLibrary.ResourcesFolder + "/" + spec.id));
                if (GUILayout.Button("裁剪预览", GUILayout.Width(64)))
                {
                    AI3DImporter.CropForSpec(spec);
                    AssetDatabase.Refresh();
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(AI3DImporter.CropFolder + "/" + spec.id + ".png"));
                }
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(CurrentKey) || (job != null && job.running)))
                {
                    if (GUILayout.Button(generated ? "重新生成" : "生成", GUILayout.Width(64))) Enqueue(new[] { spec });
                }
            }
            if (_expandedLog == spec.id && job != null)
            {
                EditorGUILayout.TextArea(string.Join("\n", job.log.Skip(Mathf.Max(0, job.log.Count - 12))), GUILayout.MinHeight(60));
            }
        }

        private void SetAll(bool value)
        {
            foreach (var p in ReferenceSceneLibrary.Props) if (p.aiCandidate) _selected[p.id] = value;
        }

        private void Enqueue(IEnumerable<ReferencePropSpec> specs)
        {
            foreach (var spec in specs)
            {
                if (_jobs.TryGetValue(spec.id, out var existing) && existing.running) continue;
                var job = new AI3DImporter.JobState { spec = spec, stage = "排队中" };
                _jobs[spec.id] = job;
                _queue.Enqueue(job);
            }
        }

        private void Pump()
        {
            int running = _jobs.Values.Count(j => j.running);
            bool changed = false;
            while (_queue.Count > 0 && running < _concurrency)
            {
                var job = _queue.Dequeue();
                running++;
                changed = true;
                _ = AI3DImporter.Run(job, MakeProvider());
            }
            if (changed || running > 0) Repaint();
        }
    }
}
