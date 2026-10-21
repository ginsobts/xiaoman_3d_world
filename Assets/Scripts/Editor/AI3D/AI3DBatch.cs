using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VN.World;

namespace VN.EditorTools.AI3D
{
    /// <summary>
    /// 命令行批量生成（不用开 Unity 界面）：
    ///   Unity -batchmode -projectPath . -executeMethod VN.EditorTools.AI3D.AI3DBatch.Generate
    /// 环境变量：
    ///   XIAOMAN_AI3D_IDS      逗号分隔的道具 id（默认全部 AI 候选）
    ///   XIAOMAN_AI3D_PROVIDER hunyuan（默认）| tripo | meshy（后两者读 EditorPrefs 里的 Key）
    ///   XIAOMAN_AI3D_SOURCE   painting（默认）| sheet
    /// 不要加 -quit：任务是异步的，做完后这里自己退出（成功 0，有失败 1）。
    /// </summary>
    public static class AI3DBatch
    {
        static readonly Queue<AI3DImporter.JobState> _pending = new Queue<AI3DImporter.JobState>();
        static readonly List<AI3DImporter.JobState> _all = new List<AI3DImporter.JobState>();
        static AI3DImporter.JobState _current;
        static IImageTo3DProvider _provider;
        static double _started;

        public static void Generate()
        {
            string idsEnv = Environment.GetEnvironmentVariable("XIAOMAN_AI3D_IDS");
            string providerEnv = (Environment.GetEnvironmentVariable("XIAOMAN_AI3D_PROVIDER") ?? "hunyuan").ToLowerInvariant();
            string sourceEnv = (Environment.GetEnvironmentVariable("XIAOMAN_AI3D_SOURCE") ?? "painting").ToLowerInvariant();
            AI3DImporter.PreferPainting = sourceEnv != "sheet";

            switch (providerEnv)
            {
                case "tripo": _provider = new TripoProvider(EditorPrefs.GetString(TripoProvider.Pref, "")); break;
                case "meshy": _provider = new MeshyProvider(EditorPrefs.GetString(MeshyProvider.Pref, "")); break;
                default: _provider = new HunyuanLocalProvider(); break;
            }

            var specs = ReferenceSceneLibrary.Props.Where(p => p.aiCandidate).ToList();
            if (!string.IsNullOrEmpty(idsEnv))
            {
                var ids = new HashSet<string>(idsEnv.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0));
                specs = specs.Where(p => ids.Contains(p.id)).ToList();
                foreach (var missing in ids.Except(specs.Select(p => p.id)))
                    Debug.LogWarning("[AI3D 批量] 未知 id: " + missing);
            }
            if (specs.Count == 0) { Debug.LogError("[AI3D 批量] 没有要生成的道具"); Exit(1); return; }

            string source = AI3DImporter.PreferPainting ? "原画裁剪" : "设定图";
            Debug.Log("[AI3D 批量] 服务商=" + _provider.Name + " 来源=" + source + " 数量=" + specs.Count + ": " + string.Join(", ", specs.Select(s => s.id)));
            foreach (var s in specs)
            {
                var job = new AI3DImporter.JobState { spec = s, stage = "排队中" };
                _pending.Enqueue(job);
                _all.Add(job);
            }
            _started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (_current != null && _current.running) return;
            if (_current != null)
            {
                Debug.Log("[AI3D 批量] " + _current.spec.id + ": " + (_current.done ? "完成 " + _current.prefabPath : "失败 " + _current.error));
                _current = null;
            }
            if (_pending.Count == 0)
            {
                EditorApplication.update -= Tick;
                int failed = _all.Count(j => !j.done);
                Debug.Log("[AI3D 批量] 结束：成功 " + (_all.Count - failed) + " / " + _all.Count + "，耗时 " + (EditorApplication.timeSinceStartup - _started).ToString("F0") + "s");
                foreach (var j in _all)
                    Debug.Log("[AI3D 批量] --- " + j.spec.id + " 日志 ---\n" + string.Join("\n", j.log));
                AssetDatabase.SaveAssets();
                Exit(failed == 0 ? 0 : 1);
                return;
            }
            _current = _pending.Dequeue();
            Debug.Log("[AI3D 批量] 开始 " + _current.spec.id + "（" + AI3DImporter.DescribeSource(_current.spec) + "）");
            _ = AI3DImporter.Run(_current, _provider);
        }

        static void Exit(int code)
        {
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
