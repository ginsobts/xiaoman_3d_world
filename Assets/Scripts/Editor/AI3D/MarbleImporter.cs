using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GaussianSplatting.Editor;
using GaussianSplatting.Runtime;
using UnityEditor;
using UnityEngine;
using VN.World;
using Object = UnityEngine.Object;

namespace VN.EditorTools.AI3D
{
    /// <summary>
    /// 一幅参考画 → World Labs Marble 世界 → 下载 SPZ 泼溅 + 碰撞网格 → 转成 GaussianSplatAsset
    /// → Resources/AI3D/World_&lt;sceneId&gt;.prefab。运行时 ReferenceSceneBuilder 发现有这个 prefab 就用它替换程序化环境。
    /// </summary>
    public static class MarbleImporter
    {
        public const string RawFolder = "Assets/Art/AI3D/Marble";            // 原始 SPZ / GLB / JSON（已 gitignore）
        public const string AssetFolder = "Assets/Resources/AI3D/Marble";    // 转换后的泼溅数据与碰撞网格
        public const string QualityPref = "Xiaoman.AI3D.MarbleQuality";      // 0 VeryHigh … 4 VeryLow
        public const string ResolutionPref = "Xiaoman.AI3D.MarbleResolution"; // full_res / 500k / 100k
        public static readonly string[] Resolutions = { "full_res", "500k", "100k" };
        public static readonly string[] QualityNames = { "极高（无损，文件很大）", "高", "中", "低（推荐，约 25 MB/场景）", "极低" };

        public static int Quality { get => EditorPrefs.GetInt(QualityPref, 3); set => EditorPrefs.SetInt(QualityPref, value); }
        public static string Resolution { get => EditorPrefs.GetString(ResolutionPref, "full_res"); set => EditorPrefs.SetString(ResolutionPref, value); }

        public class JobState
        {
            public ReferenceSceneDef scene;
            public string stage = "待开始";
            public int progress;
            public bool running;
            public bool done;
            public string error;
            public string operationId;
            public string worldId;
            public string prefabPath;
            public readonly List<string> log = new List<string>();

            public void Log(string line)
            {
                log.Add(DateTime.Now.ToString("HH:mm:ss ") + line);
                if (log.Count > 200) log.RemoveAt(0);
                Debug.Log("[Marble] " + scene.id + ": " + line);
            }
        }

        public static string PrefabPath(string sceneId) => AI3DImporter.PrefabFolder + "/" + AIAssetLibrary.WorldPrefabName(sceneId) + ".prefab";
        public static bool HasWorld(string sceneId) => File.Exists(PrefabPath(sceneId));

        /// <summary>参考画（按 imageUv 子区域裁剪）编码成 JPG 作为 Marble 输入。</summary>
        public static byte[] SceneImage(ReferenceSceneDef scene, out string fileName)
        {
            string path = AI3DImporter.ReferenceImagePath(scene.id);
            if (path == null) throw new Exception("找不到参考图: " + scene.id);
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!src.LoadImage(File.ReadAllBytes(path))) throw new Exception("参考图解码失败: " + path);
            Rect uv = scene.imageUv;
            Texture2D outTex = src;
            if (uv.width < 0.999f || uv.height < 0.999f)
            {
                int x0 = Mathf.Clamp(Mathf.RoundToInt(uv.x * src.width), 0, src.width - 1);
                int y0 = Mathf.Clamp(Mathf.RoundToInt(uv.y * src.height), 0, src.height - 1);
                int w = Mathf.Clamp(Mathf.RoundToInt(uv.width * src.width), 1, src.width - x0);
                int h = Mathf.Clamp(Mathf.RoundToInt(uv.height * src.height), 1, src.height - y0);
                outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                outTex.SetPixels(src.GetPixels(x0, y0, w, h));
                outTex.Apply();
            }
            byte[] jpg = outTex.EncodeToJPG(95);
            if (outTex != src) Object.DestroyImmediate(outTex);
            Object.DestroyImmediate(src);
            fileName = scene.id + ".jpg";
            return jpg;
        }

        public static async Task Run(JobState job, MarbleProvider provider)
        {
            job.running = true; job.done = false; job.error = null;
            try
            {
                job.stage = "上传参考画";
                byte[] image = SceneImage(job.scene, out string fileName);
                string mediaId = await provider.UploadImage(image, fileName, job.Log);
                job.progress = 5;

                job.stage = "提交生成";
                job.operationId = await provider.Generate(mediaId, job.scene.marblePrompt, "Xiaoman " + job.scene.title, job.Log);
                job.progress = 10;

                job.stage = "Marble 生成中";
                var started = DateTime.UtcNow;
                MarbleWorld world = null;
                while (true)
                {
                    await Task.Delay(10000);
                    var op = await provider.PollOperation(job.operationId);
                    if (!string.IsNullOrEmpty(op.description)) job.stage = "Marble: " + op.description;
                    double mins = (DateTime.UtcNow - started).TotalMinutes;
                    job.progress = 10 + Mathf.RoundToInt(Mathf.Clamp01((float)(mins / 6.0)) * 50f);
                    if (op.done)
                    {
                        if (!string.IsNullOrEmpty(op.error)) throw new Exception("Marble 生成失败: " + op.error);
                        world = op.world;
                        if (world == null) throw new Exception("Marble 完成但没有返回世界数据: " + op.raw);
                        break;
                    }
                    if (mins > 40) throw new Exception("Marble 超过 40 分钟仍未完成");
                }
                job.worldId = world.worldId;
                job.Log($"世界已生成 {world.worldId}  scale={world.metricScale:0.###} ground={world.groundOffset:0.###}");
                if (!string.IsNullOrEmpty(world.marbleUrl)) job.Log("网页预览: " + world.marbleUrl);

                job.stage = "下载泼溅";
                string rawDir = RawFolder + "/" + job.scene.id;
                Directory.CreateDirectory(rawDir);
                string resolution = Resolution;
                string spzUrl = world.SpzFor(resolution);
                string splatPath;
                if (!string.IsNullOrEmpty(spzUrl))
                {
                    splatPath = rawDir + "/" + job.scene.id + ".spz";
                    File.WriteAllBytes(splatPath, await HttpProviderBase.Download(spzUrl));
                }
                else
                {
                    string ply = await provider.ExportPly(world.worldId, resolution, job.Log);
                    splatPath = rawDir + "/" + job.scene.id + ".ply";
                    File.WriteAllBytes(splatPath, await HttpProviderBase.Download(ply));
                }
                job.Log($"泼溅文件 {Path.GetFileName(splatPath)} {new FileInfo(splatPath).Length / 1024 / 1024} MB");
                job.progress = 75;

                job.stage = "下载碰撞网格";
                byte[] glb = null;
                if (!string.IsNullOrEmpty(world.colliderGlbUrl))
                {
                    try
                    {
                        glb = await HttpProviderBase.Download(world.colliderGlbUrl);
                        File.WriteAllBytes(rawDir + "/" + job.scene.id + "_collider.glb", glb);
                    }
                    catch (Exception e) { job.Log("碰撞网格下载失败，只用安全地板: " + e.Message); }
                }
                File.WriteAllText(rawDir + "/" + job.scene.id + "_world.json", world.raw ?? "");
                job.progress = 80;

                job.stage = "转换为 Unity 资源";
                job.prefabPath = BuildWorldPrefab(job.scene, splatPath, glb, world, job.Log);
                job.stage = "完成";
                job.progress = 100;
                job.done = true;
            }
            catch (Exception e)
            {
                job.error = e.Message;
                job.stage = "失败";
                job.Log("错误: " + e.Message);
            }
            finally
            {
                job.running = false;
            }
        }

        /// <summary>把 SPZ/PLY + 碰撞 GLB 变成 World_&lt;sceneId&gt;.prefab。可对已下载的文件单独重跑。</summary>
        public static string BuildWorldPrefab(ReferenceSceneDef scene, string splatPath, byte[] colliderGlb, MarbleWorld world, Action<string> log)
        {
            string assetDir = AssetFolder + "/" + scene.id;
            Directory.CreateDirectory(assetDir);
            string assetPath = GaussianSplatAssetCreator.CreateAssetFromFile(splatPath, assetDir, Quality, false);
            var splatAsset = AssetDatabase.LoadAssetAtPath<GaussianSplatAsset>(assetPath);
            if (splatAsset == null) throw new Exception("泼溅资源创建失败: " + assetPath);
            log?.Invoke($"泼溅资源 {assetPath}（{splatAsset.splatCount} 个高斯）");

            Mesh colliderMesh = null;
            if (colliderGlb != null)
            {
                try
                {
                    colliderMesh = MiniGlb.LoadMesh(colliderGlb, scene.id + "_collider");
                    string meshPath = assetDir + "/" + scene.id + "_collider.asset";
                    AssetDatabase.CreateAsset(colliderMesh, meshPath);
                    colliderMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    log?.Invoke($"碰撞网格 {colliderMesh.vertexCount} 顶点");
                }
                catch (Exception e) { log?.Invoke("碰撞网格解析失败，只用安全地板: " + e.Message); colliderMesh = null; }
            }

            // Marble 原始坐标：+x 右、+y 下、+z 前，单位任意；metricScale 换算成米，groundOffset 把地面放到 y=0。
            // Unity：+x 右、+y 上、+z 前。因此 y 取反（镜像缩放，画面左右与原画一致）。
            float s = world.metricScale;
            var scale = new Vector3(s, -s, s);
            var pos = new Vector3(0f, world.groundOffset, 0f);

            var root = new GameObject(AIAssetLibrary.WorldPrefabName(scene.id));
            var marker = root.AddComponent<MarbleWorldMarker>();
            marker.sceneId = scene.id;
            marker.worldId = world.worldId;
            marker.caption = world.caption;
            marker.metricScale = world.metricScale;
            marker.groundOffset = world.groundOffset;
            marker.splatCount = splatAsset.splatCount;
            marker.spawnLocal = Vector3.zero;
            marker.spawnYaw = 0f;

            var splatGo = new GameObject("Splat");
            splatGo.transform.SetParent(root.transform, false);
            splatGo.transform.localPosition = pos;
            splatGo.transform.localScale = scale;
            var renderer = splatGo.AddComponent<GaussianSplatRenderer>();
            renderer.m_Asset = splatAsset;
            renderer.m_SHOrder = 3;
            renderer.m_SplatScale = 1f;
            renderer.m_OpacityScale = 1f;
            const string pkg = "Packages/org.nesnausk.gaussian-splatting/Shaders/";
            if (renderer.m_ShaderSplats == null) renderer.m_ShaderSplats = AssetDatabase.LoadAssetAtPath<Shader>(pkg + "RenderGaussianSplats.shader");
            if (renderer.m_ShaderComposite == null) renderer.m_ShaderComposite = AssetDatabase.LoadAssetAtPath<Shader>(pkg + "GaussianComposite.shader");
            if (renderer.m_ShaderDebugPoints == null) renderer.m_ShaderDebugPoints = AssetDatabase.LoadAssetAtPath<Shader>(pkg + "GaussianDebugRenderPoints.shader");
            if (renderer.m_ShaderDebugBoxes == null) renderer.m_ShaderDebugBoxes = AssetDatabase.LoadAssetAtPath<Shader>(pkg + "GaussianDebugRenderBoxes.shader");
            if (renderer.m_CSSplatUtilities == null) renderer.m_CSSplatUtilities = AssetDatabase.LoadAssetAtPath<ComputeShader>(pkg + "SplatUtilities.compute");

            if (colliderMesh != null)
            {
                var colGo = new GameObject("Collider");
                colGo.transform.SetParent(root.transform, false);
                colGo.transform.localPosition = pos;
                colGo.transform.localScale = scale;
                var mc = colGo.AddComponent<MeshCollider>();
                mc.sharedMesh = colliderMesh;
            }

            Directory.CreateDirectory(AI3DImporter.PrefabFolder);
            string prefabPath = PrefabPath(scene.id);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            log?.Invoke("世界 Prefab 已生成 " + prefabPath);
            return prefabPath;
        }

        /// <summary>用已下载到 Raw 目录的文件重新转换（调整质量档位后不用再花钱重新生成）。</summary>
        public static string RebuildFromRaw(ReferenceSceneDef scene, Action<string> log)
        {
            string rawDir = RawFolder + "/" + scene.id;
            string splat = File.Exists(rawDir + "/" + scene.id + ".spz") ? rawDir + "/" + scene.id + ".spz" : rawDir + "/" + scene.id + ".ply";
            if (!File.Exists(splat)) throw new Exception("没有找到已下载的泼溅文件: " + splat);
            string glbPath = rawDir + "/" + scene.id + "_collider.glb";
            string jsonPath = rawDir + "/" + scene.id + "_world.json";
            var world = new MarbleWorld();
            if (File.Exists(jsonPath))
            {
                var json = MiniJson.Parse(File.ReadAllText(jsonPath));
                world.worldId = MiniJson.GetString(json, "id");
                world.caption = MiniJson.GetString(json, "assets.caption");
                world.metricScale = (float)MiniJson.GetNumber(json, "assets.splats.semantics_metadata.metric_scale_factor", 1.0);
                world.groundOffset = (float)MiniJson.GetNumber(json, "assets.splats.semantics_metadata.ground_plane_offset", 0.0);
                if (world.metricScale <= 0f) world.metricScale = 1f;
            }
            return BuildWorldPrefab(scene, splat, File.Exists(glbPath) ? File.ReadAllBytes(glbPath) : null, world, log);
        }
    }
}
