using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using VN.World;
using Object = UnityEngine.Object;

namespace VN.EditorTools.AI3D
{
    /// <summary>
    /// 一个道具的完整流水线：裁剪参考图 → 调用图生 3D → 下载 FBX / 贴图 → 导入 → 规范尺寸与原点 → 存为
    /// Resources/AI3D/&lt;propId&gt;.prefab。运行时 AIAssetLibrary 会自动优先加载这些 prefab。
    /// </summary>
    public static class AI3DImporter
    {
        public const string RawFolder = "Assets/Art/AI3D/Raw";
        public const string PrefabFolder = "Assets/Resources/AI3D";
        public const string CropFolder = "Assets/Art/AI3D/Crops";

        public class JobState
        {
            public ReferencePropSpec spec;
            public string providerName;
            public string taskId;
            public string stage = "待开始";
            public int progress;
            public bool running;
            public bool done;
            public string error;
            public string prefabPath;
            public readonly List<string> log = new List<string>();

            public void Log(string line)
            {
                log.Add(DateTime.Now.ToString("HH:mm:ss ") + line);
                if (log.Count > 200) log.RemoveAt(0);
                Debug.Log("[AI3D] " + spec.id + ": " + line);
            }
        }

        // ---------- 裁剪 ----------

        public static string ReferenceImagePath(string sceneId)
        {
            var scene = ReferenceSceneLibrary.Scene(sceneId);
            if (scene == null) return null;
            string png = "Assets/Resources/Art/References/" + scene.imageName + ".png";
            return File.Exists(png) ? png : null;
        }

        /// <summary>按规格裁剪参考图，四周留白并补成正方形，返回 PNG 字节；同时写一份到 Crops 目录方便检查。</summary>
        public static byte[] CropForSpec(ReferencePropSpec spec, float padding = 0.08f)
        {
            string path = ReferenceImagePath(spec.sceneId);
            if (path == null) throw new Exception("找不到参考图: " + spec.sceneId);
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!src.LoadImage(File.ReadAllBytes(path))) throw new Exception("参考图解码失败: " + path);

            Rect r = spec.crop;
            if (r.width <= 0f || r.height <= 0f) r = new Rect(0f, 0f, 1f, 1f);
            // crop 以左上为原点，Texture2D 以左下为原点
            int x0 = Mathf.Clamp(Mathf.RoundToInt((r.x - padding * r.width) * src.width), 0, src.width - 1);
            int x1 = Mathf.Clamp(Mathf.RoundToInt((r.x + r.width * (1f + padding)) * src.width), x0 + 1, src.width);
            float topNorm = r.y - padding * r.height;
            float bottomNorm = r.y + r.height * (1f + padding);
            int y1 = Mathf.Clamp(Mathf.RoundToInt((1f - topNorm) * src.height), 1, src.height);
            int y0 = Mathf.Clamp(Mathf.RoundToInt((1f - bottomNorm) * src.height), 0, y1 - 1);
            int w = x1 - x0, h = y1 - y0;
            var pixels = src.GetPixels(x0, y0, w, h);

            int size = Mathf.Max(w, h);
            var dst = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var fill = new Color[size * size];
            Color bg = AverageEdge(pixels, w, h);
            for (int i = 0; i < fill.Length; i++) fill[i] = bg;
            dst.SetPixels(fill);
            dst.SetPixels((size - w) / 2, (size - h) / 2, w, h, pixels);
            dst.Apply();
            // 小裁剪放大到至少 1024px，服务端重建更稳定
            if (size < 1024)
            {
                int up = 1024;
                var big = new Texture2D(up, up, TextureFormat.RGBA32, false);
                var bigPixels = new Color[up * up];
                for (int y = 0; y < up; y++)
                for (int x = 0; x < up; x++)
                    bigPixels[y * up + x] = dst.GetPixelBilinear((x + 0.5f) / up, (y + 0.5f) / up);
                big.SetPixels(bigPixels);
                big.Apply();
                Object.DestroyImmediate(dst);
                dst = big;
            }
            byte[] png = dst.EncodeToPNG();
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(dst);

            Directory.CreateDirectory(CropFolder);
            File.WriteAllBytes(Path.Combine(CropFolder, spec.id + ".png"), png);
            return png;
        }

        private static Color AverageEdge(Color[] pixels, int w, int h)
        {
            Color sum = Color.clear; int n = 0;
            for (int x = 0; x < w; x += Mathf.Max(1, w / 32)) { sum += pixels[x]; sum += pixels[(h - 1) * w + x]; n += 2; }
            for (int y = 0; y < h; y += Mathf.Max(1, h / 32)) { sum += pixels[y * w]; sum += pixels[y * w + w - 1]; n += 2; }
            var c = sum / Mathf.Max(1, n);
            c.a = 1f;
            return c;
        }

        // ---------- 生成 ----------

        public static async Task Run(JobState job, IImageTo3DProvider provider)
        {
            job.running = true;
            job.done = false;
            job.error = null;
            job.providerName = provider.Name;
            try
            {
                job.stage = "裁剪参考图";
                byte[] png = CropForSpec(job.spec);
                job.Log($"裁剪完成 {png.Length / 1024} KB");

                job.stage = "提交任务";
                job.taskId = await provider.CreateTask(png, job.spec, job.Log);

                job.stage = "生成中";
                ProviderTaskResult result;
                var started = DateTime.UtcNow;
                while (true)
                {
                    await Task.Delay(6000);
                    result = await provider.Poll(job.taskId, job.Log);
                    job.progress = result.progress;
                    if (!string.IsNullOrEmpty(result.nextTaskId)) job.taskId = result.nextTaskId;
                    if (result.status == ProviderTaskStatus.Succeeded || result.status == ProviderTaskStatus.Failed) break;
                    if ((DateTime.UtcNow - started).TotalMinutes > 30) throw new Exception("超过 30 分钟仍未完成");
                }
                if (result.status == ProviderTaskStatus.Failed) throw new Exception(result.error ?? "生成失败");
                if (string.IsNullOrEmpty(result.fbxUrl) && string.IsNullOrEmpty(result.glbUrl)) throw new Exception("服务没有返回模型地址: " + result.raw);

                job.stage = "下载模型";
                string rawDir = Path.Combine(RawFolder, job.spec.id);
                Directory.CreateDirectory(rawDir);
                string modelUrl = !string.IsNullOrEmpty(result.fbxUrl) ? result.fbxUrl : result.glbUrl;
                byte[] modelBytes = await HttpProviderBase.Download(modelUrl);
                string modelPath = SaveModel(rawDir, job.spec.id, modelUrl, modelBytes, job.Log);
                string texPath = null;
                if (!string.IsNullOrEmpty(result.baseColorUrl))
                {
                    try
                    {
                        byte[] texBytes = await HttpProviderBase.Download(result.baseColorUrl);
                        texPath = Path.Combine(rawDir, job.spec.id + "_basecolor" + GuessExt(result.baseColorUrl, ".png")).Replace('\\', '/');
                        File.WriteAllBytes(texPath, texBytes);
                    }
                    catch (Exception e) { job.Log("贴图下载失败（将使用 FBX 内嵌贴图）: " + e.Message); }
                }

                job.stage = "导入";
                job.prefabPath = ImportAndBuildPrefab(job.spec, modelPath, texPath, job.Log);
                job.stage = "完成";
                job.done = true;
                job.progress = 100;
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

        private static string GuessExt(string url, string fallback)
        {
            try
            {
                string path = new Uri(url).AbsolutePath;
                string ext = Path.GetExtension(path);
                return string.IsNullOrEmpty(ext) ? fallback : ext.ToLowerInvariant();
            }
            catch { return fallback; }
        }

        private static string SaveModel(string dir, string id, string url, byte[] bytes, Action<string> log)
        {
            string ext = GuessExt(url, ".fbx");
            if (ext == ".zip")
            {
                string tmp = Path.Combine(Path.GetTempPath(), "xiaoman_ai3d_" + id + ".zip");
                File.WriteAllBytes(tmp, bytes);
                using (var zip = ZipFile.OpenRead(tmp))
                {
                    string found = null;
                    foreach (var entry in zip.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue;
                        string target = Path.Combine(dir, entry.Name);
                        entry.ExtractToFile(target, true);
                        string e = Path.GetExtension(entry.Name).ToLowerInvariant();
                        if (found == null && (e == ".fbx" || e == ".glb" || e == ".obj")) found = target;
                    }
                    File.Delete(tmp);
                    if (found == null) throw new Exception("压缩包里没有模型文件");
                    log?.Invoke("已解压 " + Path.GetFileName(found));
                    return found.Replace('\\', '/');
                }
            }
            string modelPath = Path.Combine(dir, id + ext).Replace('\\', '/');
            File.WriteAllBytes(modelPath, bytes);
            log?.Invoke($"模型已保存 {modelPath} ({bytes.Length / 1024} KB)");
            return modelPath;
        }

        // ---------- 导入与规范化 ----------

        public static string ImportAndBuildPrefab(ReferencePropSpec spec, string modelPath, string texPath, Action<string> log)
        {
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            if (!string.IsNullOrEmpty(texPath)) AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(modelPath) is ModelImporter mi)
            {
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.importAnimation = false;
                mi.importCameras = false;
                mi.importLights = false;
                mi.generateSecondaryUV = false;
                mi.isReadable = true;
                mi.SaveAndReimport();
            }
            if (!string.IsNullOrEmpty(texPath) && AssetImporter.GetAtPath(texPath) is TextureImporter ti)
            {
                ti.sRGBTexture = true;
                ti.maxTextureSize = 2048;
                ti.SaveAndReimport();
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                if (Path.GetExtension(modelPath).ToLowerInvariant() == ".glb")
                    throw new Exception("Unity 内置导入器不支持 GLB。请在 Meshy 中改用 FBX，或安装 glTFast 后重试。");
                throw new Exception("模型导入失败: " + modelPath);
            }

            // 实例化并规范尺寸：高度 = targetHeight，原点放到脚底中心
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var root = new GameObject(spec.id);
            instance.transform.SetParent(root.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new Exception("模型里没有 Renderer");
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float height = Mathf.Max(0.001f, bounds.size.y);
            float scale = spec.targetHeight / height;
            instance.transform.localScale = Vector3.one * scale;
            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            instance.transform.localPosition = -new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            // 统一用绘本铅笔材质 + AI 贴图
            Texture2D tex = string.IsNullOrEmpty(texPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) tex = FindEmbeddedTexture(modelPath);
            var shader = Shader.Find("Xiaoman/Fairytale Pencil") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = spec.id + "_Pencil", color = Color.white };
            if (tex != null) mat.mainTexture = tex;
            if (mat.HasProperty("_Roughness")) mat.SetFloat("_Roughness", 0.8f);
            if (mat.HasProperty("_HatchStrength")) mat.SetFloat("_HatchStrength", 0.06f);
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", FairytaleArt.Ink);
            Directory.CreateDirectory(PrefabFolder);
            string matPath = PrefabFolder + "/" + spec.id + "_Pencil.mat";
            AssetDatabase.CreateAsset(mat, matPath);
            foreach (var r in renderers)
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            // 碰撞体与标记
            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var col = root.AddComponent<BoxCollider>();
            col.center = root.transform.InverseTransformPoint(bounds.center);
            col.size = bounds.size;
            var marker = root.AddComponent<GeneratedPropMarker>();
            marker.propId = spec.id;

            string prefabPath = PrefabFolder + "/" + spec.id + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            log?.Invoke("Prefab 已生成 " + prefabPath);
            return prefabPath;
        }

        private static Texture2D FindEmbeddedTexture(string modelPath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (asset is Texture2D t) return t;
            string dir = Path.GetDirectoryName(modelPath);
            if (dir == null) return null;
            foreach (var file in Directory.GetFiles(dir))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                {
                    string p = file.Replace('\\', '/');
                    AssetDatabase.ImportAsset(p);
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                    if (t != null) return t;
                }
            }
            return null;
        }

        /// <summary>把当前打开场景中的占位道具替换为已生成的 prefab。</summary>
        public static int ReplacePlaceholdersInOpenScene()
        {
            int replaced = 0;
            foreach (var marker in Object.FindObjectsOfType<GeneratedPropMarker>())
            {
                if (!marker.isPlaceholder) continue;
                var prefab = Resources.Load<GameObject>(AIAssetLibrary.ResourcesFolder + "/" + marker.propId);
                if (prefab == null) continue;
                var t = marker.transform;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, t.parent);
                go.transform.localPosition = t.localPosition;
                go.transform.localRotation = t.localRotation;
                go.transform.localScale = t.localScale;
                go.name = "AI_" + marker.propId;
                // 保留钢琴等可交互组件
                var instrument = marker.GetComponent<VN.Multiplayer.PlayableInstrument>();
                if (instrument != null)
                {
                    var copy = go.AddComponent<VN.Multiplayer.PlayableInstrument>();
                    copy.instrumentId = instrument.instrumentId;
                    copy.noteCount = instrument.noteCount;
                    copy.timbre = instrument.timbre;
                    copy.displayName = instrument.displayName;
                    copy.interactRadius = instrument.interactRadius;
                }
                Undo.RegisterCreatedObjectUndo(go, "Replace AI prop");
                Undo.DestroyObjectImmediate(marker.gameObject);
                replaced++;
            }
            return replaced;
        }
    }
}
