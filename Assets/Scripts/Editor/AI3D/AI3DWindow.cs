using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VN.World;

namespace VN.EditorTools.AI3D
{
    /// <summary>
    /// AI 资产控制台：
    ///   页签一「道具与人物」：Meshy 7.1 最高精度图生 3D（人物自动绑骨 + 动画）→ Resources/AI3D prefab
    ///   页签二「Marble 环境」：World Labs Marble 把整幅画作生成可走进去的高斯泼溅世界
    /// API Key 存在 EditorPrefs（本机），不会进入仓库。
    /// </summary>
    public class AI3DWindow : EditorWindow
    {
        private const string ProviderPref = "Xiaoman.AI3D.Provider";
        private const string ConcurrencyPref = "Xiaoman.AI3D.Concurrency";
        private const string TabPref = "Xiaoman.AI3D.Tab";

        private int _tab;
        private int _providerIndex;
        private string _meshyKey = "";
        private string _tripoKey = "";
        private string _marbleKey = "";
        private int _concurrency = 3;
        private float _modelYaw;
        private Vector2 _scroll;
        private string _sceneFilter = "";
        private readonly Dictionary<string, bool> _selected = new Dictionary<string, bool>();
        private readonly Dictionary<string, AI3DImporter.JobState> _jobs = new Dictionary<string, AI3DImporter.JobState>();
        private readonly Queue<AI3DImporter.JobState> _queue = new Queue<AI3DImporter.JobState>();
        private readonly Dictionary<string, MarbleImporter.JobState> _worldJobs = new Dictionary<string, MarbleImporter.JobState>();
        private string _expandedLog;

        [MenuItem("3D世界/AI 资产控制台（图生 3D + Marble 环境）")]
        public static void Open()
        {
            var win = GetWindow<AI3DWindow>("AI 资产");
            win.minSize = new Vector2(600, 520);
        }

        [MenuItem("3D世界/在当前场景生成参考画作场景")]
        public static void BuildGalleriesInCurrentScene()
        {
            var level = UnityEngine.Object.FindObjectOfType<WorldLevel>();
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
            _tab = EditorPrefs.GetInt(TabPref, 0);
            _providerIndex = EditorPrefs.GetInt(ProviderPref, 0);
            _meshyKey = EditorPrefs.GetString(MeshyProvider.Pref, "");
            _tripoKey = EditorPrefs.GetString(TripoProvider.Pref, "");
            _marbleKey = EditorPrefs.GetString(MarbleProvider.Pref, "");
            _concurrency = EditorPrefs.GetInt(ConcurrencyPref, 3);
            _modelYaw = EditorPrefs.GetFloat(AI3DImporter.YawPref, 0f);
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
            int newTab = GUILayout.Toolbar(_tab, new[] { "道具与人物（Meshy）", "环境（World Labs Marble）" }, GUILayout.Height(26));
            if (newTab != _tab) { _tab = newTab; EditorPrefs.SetInt(TabPref, _tab); }
            EditorGUILayout.Space(6);
            if (_tab == 0) DrawPropsTab(); else DrawMarbleTab();
        }

        // ---------------- 道具与人物 ----------------

        private void DrawPropsTab()
        {
            EditorGUILayout.LabelField("图生 3D 服务商", EditorStyles.boldLabel);
            int newProvider = GUILayout.Toolbar(_providerIndex, new[] { "Meshy 7.1（推荐，含绑骨动画）", "Tripo v3.1（备用，仅道具）" });
            if (newProvider != _providerIndex) { _providerIndex = newProvider; EditorPrefs.SetInt(ProviderPref, _providerIndex); }

            EditorGUI.BeginChangeCheck();
            if (_providerIndex == 0)
            {
                _meshyKey = EditorGUILayout.PasswordField("Meshy API Key", _meshyKey);
                var tier = (MeshyProvider.QualityTier)EditorGUILayout.Popup("精度档位", (int)MeshyProvider.Tier,
                    new[] { "标准（5 万面）", "高（15 万面，4K 几何 + 4K 贴图）", "极致（不减面，文件最大）" });
                if (tier != MeshyProvider.Tier) MeshyProvider.Tier = tier;
                EditorGUILayout.HelpBox("Key 在 https://www.meshy.ai/settings/api 创建，只保存在本机。高精度单图约 20–60 Credits/个，人物另加绑骨 5 + 动画 3/段。设定图放在 Assets/Art/AI3D/Refs/<id>.png 或 <id>_front/_back.png，自动多视角重建。", MessageType.Info);
            }
            else
            {
                _tripoKey = EditorGUILayout.PasswordField("Tripo API Key", _tripoKey);
                EditorGUILayout.HelpBox("在 https://platform.tripo3d.ai 创建 Key。Tripo 先生成 GLB 再自动转换为 FBX；不做绑骨。", MessageType.Info);
            }
            _concurrency = EditorGUILayout.IntSlider("并发任务数", _concurrency, 1, 6);
            _modelYaw = EditorGUILayout.Slider(new GUIContent("模型朝向补正(°)", "导入后发现人物/道具背对玩家时改成 180 再重新导入"), _modelYaw, -180f, 180f);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(MeshyProvider.Pref, _meshyKey);
                EditorPrefs.SetString(TripoProvider.Pref, _tripoKey);
                EditorPrefs.SetInt(ConcurrencyPref, _concurrency);
                EditorPrefs.SetFloat(AI3DImporter.YawPref, _modelYaw);
            }

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("清单", EditorStyles.boldLabel, GUILayout.Width(40));
                _sceneFilter = EditorGUILayout.TextField(_sceneFilter, EditorStyles.toolbarSearchField);
                if (GUILayout.Button("全选", GUILayout.Width(50))) SetAll(true);
                if (GUILayout.Button("全不选", GUILayout.Width(60))) SetAll(false);
                if (GUILayout.Button("仅未生成", GUILayout.Width(70)))
                    foreach (var p in ReferenceSceneLibrary.Props.Where(p => p.aiCandidate)) _selected[p.id] = !AIAssetLibrary.HasGenerated(p.id);
                if (GUILayout.Button("仅人物", GUILayout.Width(60)))
                    foreach (var p in ReferenceSceneLibrary.Props.Where(p => p.aiCandidate)) _selected[p.id] = p.isCharacter;
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
                    if (GUILayout.Button($"生成选中的 {selectedCount} 个", GUILayout.Height(30)))
                        Enqueue(ReferenceSceneLibrary.Props.Where(p => p.aiCandidate && _selected.TryGetValue(p.id, out var s) && s));
                }
                if (GUILayout.Button("用 AI 模型替换场景占位", GUILayout.Height(30), GUILayout.Width(170))) ReplacePlaceholders();
            }
            if (running > 0 || _queue.Count > 0)
                EditorGUILayout.HelpBox($"进行中 {running} 个，排队 {_queue.Count} 个。高精度生成约 5–10 分钟/个，人物再加 2–4 分钟。窗口可以最小化但不要关闭 Unity。", MessageType.None);
            if (string.IsNullOrEmpty(CurrentKey))
                EditorGUILayout.HelpBox("请先填写 API Key。", MessageType.Warning);
            EditorGUILayout.Space(4);
        }

        private void DrawRow(ReferencePropSpec spec)
        {
            _jobs.TryGetValue(spec.id, out var job);
            bool generated = AIAssetLibrary.HasGenerated(spec.id);
            var refs = AI3DImporter.ReferenceSheetPaths(spec);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                bool sel = _selected.TryGetValue(spec.id, out var s) && s;
                bool newSel = EditorGUILayout.Toggle(sel, GUILayout.Width(18));
                if (newSel != sel) _selected[spec.id] = newSel;

                string label = (spec.isCharacter ? "[人物] " : "") + spec.displayName + "  (" + spec.id + ")";
                EditorGUILayout.LabelField(label, GUILayout.Width(210));
                string srcInfo = refs.Count > 0 ? $"设定图×{refs.Count}" : "原画裁剪";
                EditorGUILayout.LabelField(srcInfo, EditorStyles.miniLabel, GUILayout.Width(64));
                string status = job != null ? job.stage + (job.running ? $" {job.progress}%" : "") : (generated ? "已生成" : "占位模型");
                var style = new GUIStyle(EditorStyles.label);
                if (job != null && job.error != null) style.normal.textColor = new Color(0.9f, 0.3f, 0.3f);
                else if (generated || (job != null && job.done)) style.normal.textColor = new Color(0.3f, 0.7f, 0.35f);
                EditorGUILayout.LabelField(status, style, GUILayout.Width(120));

                if (job != null && job.log.Count > 0 && GUILayout.Button("日志", GUILayout.Width(40)))
                    _expandedLog = _expandedLog == spec.id ? null : spec.id;
                if (generated && GUILayout.Button("定位", GUILayout.Width(40)))
                    EditorGUIUtility.PingObject(Resources.Load<GameObject>(AIAssetLibrary.ResourcesFolder + "/" + spec.id));
                if (GUILayout.Button("参考图", GUILayout.Width(52)))
                {
                    if (refs.Count > 0) EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(refs[0]));
                    else
                    {
                        AI3DImporter.CropForSpec(spec);
                        AssetDatabase.Refresh();
                        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(AI3DImporter.CropFolder + "/" + spec.id + ".png"));
                    }
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

        // ---------------- Marble 环境 ----------------

        private void DrawMarbleTab()
        {
            EditorGUILayout.LabelField("World Labs Marble（图生 3D 世界）", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _marbleKey = EditorGUILayout.PasswordField("Marble API Key", _marbleKey);
            int model = Array.IndexOf(MarbleProvider.Models, MarbleProvider.Model);
            if (model < 0) model = 0;
            int newModel = EditorGUILayout.Popup("生成模型", model, new[] { "marble-1.1-plus（最精细）", "marble-1.1", "marble-1.0", "marble-1.0-draft（便宜试跑）" });
            if (newModel != model) MarbleProvider.Model = MarbleProvider.Models[newModel];
            int res = Array.IndexOf(MarbleImporter.Resolutions, MarbleImporter.Resolution);
            if (res < 0) res = 0;
            int newRes = EditorGUILayout.Popup("泼溅数量", res, new[] { "full_res（约 200 万，最精细）", "500k", "100k" });
            if (newRes != res) MarbleImporter.Resolution = MarbleImporter.Resolutions[newRes];
            int q = EditorGUILayout.Popup("Unity 内压缩档位", MarbleImporter.Quality, MarbleImporter.QualityNames);
            if (q != MarbleImporter.Quality) MarbleImporter.Quality = q;
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString(MarbleProvider.Pref, _marbleKey);

            bool supported = MarbleWorldMarker.RendererSupported;
            EditorGUILayout.HelpBox(
                "Key 在 https://platform.worldlabs.ai 创建，只保存在本机。每幅画生成一次约 5 分钟。\n" +
                "生成后整幅画作就是可以走进去的环境，程序化墙壁/地面自动隐藏，只保留会动的人物与可弹的钢琴。\n" +
                (supported ? "当前图形 API：" + SystemInfo.graphicsDeviceType + "，可以渲染泼溅。"
                           : "当前图形 API：" + SystemInfo.graphicsDeviceType + " 不支持泼溅渲染。项目已设置 Windows 用 D3D12，重启 Unity 后生效。"),
                supported ? MessageType.Info : MessageType.Warning);

            EditorGUILayout.Space(6);
            foreach (var scene in ReferenceSceneLibrary.Scenes)
            {
                _worldJobs.TryGetValue(scene.id, out var job);
                bool has = MarbleImporter.HasWorld(scene.id);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(scene.title + "  (" + scene.id + ")", GUILayout.Width(200));
                    string status = job != null ? job.stage + (job.running ? $" {job.progress}%" : "") : (has ? "已生成" : "程序化环境");
                    var style = new GUIStyle(EditorStyles.label);
                    if (job != null && job.error != null) style.normal.textColor = new Color(0.9f, 0.3f, 0.3f);
                    else if (has || (job != null && job.done)) style.normal.textColor = new Color(0.3f, 0.7f, 0.35f);
                    EditorGUILayout.LabelField(status, style);
                    if (job != null && job.log.Count > 0 && GUILayout.Button("日志", GUILayout.Width(40)))
                        _expandedLog = _expandedLog == "world:" + scene.id ? null : "world:" + scene.id;
                    if (has && GUILayout.Button("定位", GUILayout.Width(40)))
                        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(MarbleImporter.PrefabPath(scene.id)));
                    if (System.IO.Directory.Exists(MarbleImporter.RawFolder + "/" + scene.id) && GUILayout.Button(new GUIContent("重转换", "用已下载的文件按当前压缩档位重新生成，不花钱"), GUILayout.Width(52)))
                    {
                        var j = new MarbleImporter.JobState { scene = scene };
                        _worldJobs[scene.id] = j;
                        try { j.prefabPath = MarbleImporter.RebuildFromRaw(scene, j.Log); j.stage = "完成"; j.done = true; j.progress = 100; }
                        catch (Exception e) { j.error = e.Message; j.stage = "失败"; j.Log("错误: " + e.Message); }
                    }
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_marbleKey) || (job != null && job.running)))
                    {
                        if (GUILayout.Button(has ? "重新生成" : "生成环境", GUILayout.Width(72)))
                        {
                            var j = new MarbleImporter.JobState { scene = scene, stage = "开始" };
                            _worldJobs[scene.id] = j;
                            _ = MarbleImporter.Run(j, new MarbleProvider(_marbleKey));
                        }
                    }
                }
                if (_expandedLog == "world:" + scene.id && job != null)
                    EditorGUILayout.TextArea(string.Join("\n", job.log.Skip(Mathf.Max(0, job.log.Count - 12))), GUILayout.MinHeight(60));
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("建议顺序：先只生成「图书馆的窗」看效果（约 5 分钟），满意再生成其余四幅。生成后进游戏或用「3D世界/在当前场景生成参考画作场景」重建即可看到。", MessageType.None);
            if (string.IsNullOrEmpty(_marbleKey)) EditorGUILayout.HelpBox("请先填写 Marble API Key。", MessageType.Warning);
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
            if (changed || running > 0 || _worldJobs.Values.Any(j => j.running)) Repaint();
        }
    }
}
