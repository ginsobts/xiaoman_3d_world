using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VN.World;
using Object = UnityEngine.Object;

namespace VN.EditorTools.AI3D
{
    /// <summary>
    /// 一个道具 / 人物的完整流水线：
    ///   参考图（Assets/Art/AI3D/Refs 里的干净设定图，没有时回退到裁剪原画）
    ///   → 图生 3D（人物 A-pose）→ [人物] 自动绑骨 → 预设动画
    ///   → 下载 FBX / 4K 贴图 → 导入 → 规范尺寸与原点 → [人物] Animator 循环播放
    ///   → 存为 Resources/AI3D/&lt;propId&gt;.prefab。运行时 AIAssetLibrary 会自动优先加载这些 prefab。
    /// </summary>
    public static class AI3DImporter
    {
        public const string RawFolder = "Assets/Art/AI3D/Raw";
        public const string PrefabFolder = "Assets/Resources/AI3D";
        public const string CropFolder = "Assets/Art/AI3D/Crops";
        public const string RefsFolder = "Assets/Art/AI3D/Refs";
        public const string MeshFolder = "Assets/Resources/AI3D/Meshes";
        /// <summary>导入后绕 Y 轴补正的角度（有的服务商模型正面朝 -Z；在控制台里改一次即可）。</summary>
        public const string YawPref = "Xiaoman.AI3D.ModelYaw";

        private static readonly string[] RefSuffixes = { "_front", "", "_back", "_left", "_right", "_side" };
        private static readonly string[] RefExts = { ".png", ".jpg", ".jpeg", ".webp" };

        public class JobState
        {
            public ReferencePropSpec spec;
            public string providerName;
            public string taskId;
            public string rigTaskId;
            public string animTaskId;
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

        // ---------- 参考图 ----------

        public static string ReferenceImagePath(string sceneId)
        {
            var scene = ReferenceSceneLibrary.Scene(sceneId);
            if (scene == null) return null;
            string png = "Assets/Resources/Art/References/" + scene.imageName + ".png";
            return File.Exists(png) ? png : null;
        }

        /// <summary>Refs 目录里属于这个道具的设定图路径，正面在前。</summary>
        public static List<string> ReferenceSheetPaths(ReferencePropSpec spec)
        {
            var list = new List<string>();
            foreach (var suffix in RefSuffixes)
            foreach (var ext in RefExts)
            {
                string p = RefsFolder + "/" + spec.id + suffix + ext;
                if (File.Exists(p) && !list.Contains(p)) { list.Add(p); break; }
            }
            return list;
        }

        public static bool HasReferenceSheet(ReferencePropSpec spec) => ReferenceSheetPaths(spec).Count > 0;

        /// <summary>参考来源：0 = 直接裁剪原画（默认，贴图最贴近画作）；1 = 用 Refs 里的干净设定图（多视角，几何更稳）。</summary>
        public const string SourcePref = "Xiaoman.AI3D.Source";
        public static bool PreferPainting
        {
            get => EditorPrefs.GetInt(SourcePref, 0) == 0;
            set => EditorPrefs.SetInt(SourcePref, value ? 0 : 1);
        }

        public static bool HasCrop(ReferencePropSpec spec) => spec.crop.width > 0f && spec.crop.height > 0f;

        /// <summary>这个道具当前实际会用哪种参考（给界面显示）。</summary>
        public static string DescribeSource(ReferencePropSpec spec)
        {
            int sheets = ReferenceSheetPaths(spec).Count;
            bool crop = HasCrop(spec);
            if (PreferPainting) return crop ? "原画裁剪" : sheets > 0 ? $"设定图×{sheets}（无裁剪框）" : "缺少参考";
            return sheets > 0 ? $"设定图×{sheets}" : crop ? "原画裁剪（无设定图）" : "缺少参考";
        }

        /// <summary>按「参考来源」设置取图：原画裁剪优先或设定图优先，另一种作为回退。</summary>
        public static List<byte[]> LoadReferenceImages(ReferencePropSpec spec, Action<string> log)
        {
            var images = new List<byte[]>();
            var paths = ReferenceSheetPaths(spec);
            bool crop = HasCrop(spec);
            bool usePainting = PreferPainting ? crop : (paths.Count == 0 && crop);
            if (usePainting)
            {
                images.Add(CropForSpec(spec));
                log?.Invoke("参考：直接从原画裁剪（" + spec.sceneId + "）");
                return images;
            }
            foreach (var p in paths) images.Add(File.ReadAllBytes(p));
            if (images.Count > 0)
            {
                log?.Invoke($"参考：设定图 {images.Count} 张: {string.Join(", ", paths.Select(Path.GetFileName))}");
                return images;
            }
            throw new Exception($"没有设定图也没有裁剪框。请把正面图放到 {RefsFolder}/{spec.id}_front.png，或在 ReferenceSceneLibrary 里给它一个 crop 框");
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
                job.stage = "准备参考图";
                var images = LoadReferenceImages(job.spec, job.Log);

                job.stage = "提交任务";
                job.taskId = await provider.CreateTask(images, job.spec, job.Log);

                job.stage = "生成模型";
                int timeout = provider is HunyuanLocalProvider ? 25 : 40;
                var model = await WaitFor(job, provider.Poll, job.taskId, 0, 60, timeout);
                if (!model.HasModel) throw new Exception("服务没有返回模型地址: " + model.raw);

                ProviderTaskResult final = model;
                bool animated = false;
                if (job.spec.isCharacter)
                {
                    if (!provider.SupportsRigging)
                        job.Log("当前服务商不支持绑骨，人物将作为静态模型导入");
                    else
                    {
                        job.stage = "自动绑骨";
                        job.rigTaskId = await provider.CreateRigTask(job.taskId, job.spec, job.Log);
                        var rig = await WaitFor(job, provider.PollRig, job.rigTaskId, 60, 80, 15);
                        if (string.IsNullOrEmpty(rig.fbxUrl)) throw new Exception("绑骨没有返回 FBX: " + rig.raw);
                        final = rig;

                        job.stage = "套用动画";
                        try
                        {
                            job.animTaskId = await provider.CreateAnimationTask(job.rigTaskId, job.spec.animationActionIds, job.Log);
                            var anim = await WaitFor(job, provider.PollAnimation, job.animTaskId, 80, 90, 15);
                            if (!string.IsNullOrEmpty(anim.fbxUrl)) { final = anim; animated = true; }
                            else job.Log("动画没有返回 FBX，改用绑骨静态模型: " + anim.raw);
                        }
                        catch (Exception e)
                        {
                            job.Log("动画阶段失败，改用绑骨静态模型: " + e.Message);
                        }
                    }
                }

                job.stage = "下载模型";
                string rawDir = Path.Combine(RawFolder, job.spec.id).Replace('\\', '/');
                Directory.CreateDirectory(rawDir);
                string modelPath;
                if (final.glbBytes != null)
                {
                    modelPath = Path.Combine(rawDir, job.spec.id + ".glb").Replace('\\', '/');
                    File.WriteAllBytes(modelPath, final.glbBytes);
                    job.Log($"模型已保存 {modelPath} ({final.glbBytes.Length / 1024} KB)");
                }
                else
                {
                    string modelUrl = !string.IsNullOrEmpty(final.fbxUrl) ? final.fbxUrl : final.glbUrl;
                    byte[] modelBytes = await HttpProviderBase.Download(modelUrl);
                    modelPath = SaveModel(rawDir, job.spec.id, modelUrl, modelBytes, job.Log);
                }
                string texPath = null;
                if (!string.IsNullOrEmpty(model.baseColorUrl))
                {
                    try
                    {
                        byte[] texBytes = await HttpProviderBase.Download(model.baseColorUrl);
                        texPath = Path.Combine(rawDir, job.spec.id + "_basecolor" + GuessExt(model.baseColorUrl, ".png")).Replace('\\', '/');
                        File.WriteAllBytes(texPath, texBytes);
                    }
                    catch (Exception e) { job.Log("贴图下载失败（将使用 FBX 内嵌贴图）: " + e.Message); }
                }
                if (final != model && (!string.IsNullOrEmpty(model.glbUrl) || model.glbBytes != null))
                {
                    // 绑骨 / 动画走的是 FBX；基础色从未绑骨的 GLB 里取出来，顺便留一份静态模型
                    try
                    {
                        byte[] staticGlb = model.glbBytes ?? await HttpProviderBase.Download(model.glbUrl);
                        File.WriteAllBytes(Path.Combine(rawDir, job.spec.id + "_static.glb"), staticGlb);
                        if (texPath == null)
                        {
                            var parsed = MiniGlb.LoadModel(staticGlb, job.spec.id + "_static");
                            var m0 = parsed.materials.Count > 0 ? parsed.materials[0] : null;
                            if (m0 != null && m0.baseColor != null)
                            {
                                texPath = Path.Combine(rawDir, job.spec.id + "_basecolor" + (IsJpeg(m0.baseColor) ? ".jpg" : ".png")).Replace('\\', '/');
                                File.WriteAllBytes(texPath, m0.baseColor);
                            }
                            Object.DestroyImmediate(parsed.mesh);
                        }
                    }
                    catch (Exception e) { job.Log("静态 GLB 处理失败（将使用 FBX 内嵌贴图）: " + e.Message); }
                }
                else if (final != model && !string.IsNullOrEmpty(model.fbxUrl))
                {
                    try { File.WriteAllBytes(Path.Combine(rawDir, job.spec.id + "_static.fbx"), await HttpProviderBase.Download(model.fbxUrl)); }
                    catch { /* 可选 */ }
                }

                job.stage = "导入";
                job.prefabPath = ImportAndBuildPrefab(job.spec, modelPath, texPath, job.Log, animated);
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

        private static async Task<ProviderTaskResult> WaitFor(JobState job, Func<string, Action<string>, Task<ProviderTaskResult>> poll,
            string taskId, int progressFrom, int progressTo, int timeoutMinutes)
        {
            var started = DateTime.UtcNow;
            string id = taskId;
            while (true)
            {
                await Task.Delay(6000);
                var result = await poll(id, job.Log);
                job.progress = progressFrom + Mathf.RoundToInt(Mathf.Clamp01(result.progress / 100f) * (progressTo - progressFrom));
                if (!string.IsNullOrEmpty(result.nextTaskId)) { id = result.nextTaskId; job.taskId = id; }
                if (result.status == ProviderTaskStatus.Succeeded) { job.progress = progressTo; return result; }
                if (result.status == ProviderTaskStatus.Failed) throw new Exception(result.error ?? "生成失败");
                if ((DateTime.UtcNow - started).TotalMinutes > timeoutMinutes) throw new Exception($"超过 {timeoutMinutes} 分钟仍未完成");
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

        public static string ImportAndBuildPrefab(ReferencePropSpec spec, string modelPath, string texPath, Action<string> log, bool animated = false)
        {
            bool isGlb = Path.GetExtension(modelPath).ToLowerInvariant() == ".glb";
            if (isGlb)
            {
                // GLB：自己解析成 Mesh 资产 + 贴图文件，不依赖 Unity 的模型导入器
                var glb = MiniGlb.LoadModel(File.ReadAllBytes(modelPath), spec.id);
                Directory.CreateDirectory(MeshFolder);
                string meshPath = MeshFolder + "/" + spec.id + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing != null) { existing.Clear(); EditorUtility.CopySerialized(glb.mesh, existing); Object.DestroyImmediate(glb.mesh); glb.mesh = existing; }
                else AssetDatabase.CreateAsset(glb.mesh, meshPath);
                string dir = Path.GetDirectoryName(modelPath)?.Replace('\\', '/') ?? RawFolder;
                string normalPath = null;
                for (int i = 0; i < glb.materials.Count; i++)
                {
                    var m = glb.materials[i];
                    if (texPath == null && m.baseColor != null)
                    {
                        texPath = dir + "/" + spec.id + "_basecolor" + (IsJpeg(m.baseColor) ? ".jpg" : ".png");
                        File.WriteAllBytes(texPath, m.baseColor);
                    }
                    if (normalPath == null && m.normal != null)
                    {
                        normalPath = dir + "/" + spec.id + "_normal" + (IsJpeg(m.normal) ? ".jpg" : ".png");
                        File.WriteAllBytes(normalPath, m.normal);
                    }
                }
                AssetDatabase.SaveAssets();
                if (!string.IsNullOrEmpty(texPath)) AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
                if (!string.IsNullOrEmpty(normalPath))
                {
                    AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
                    if (AssetImporter.GetAtPath(normalPath) is TextureImporter ni)
                    {
                        ni.textureType = TextureImporterType.NormalMap;
                        ni.maxTextureSize = 4096;
                        ni.SaveAndReimport();
                    }
                }
                log?.Invoke($"GLB 解析完成：{glb.mesh.vertexCount} 顶点 / {glb.mesh.triangles.Length / 3} 三角形 / {glb.materials.Count} 材质" + (normalPath != null ? "（含法线贴图）" : ""));
                var glbGo = new GameObject(spec.id + "_mesh");
                glbGo.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                var mr = glbGo.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new Material[glb.materials.Count];
                return BuildPrefabFromInstance(spec, glbGo, texPath, normalPath, log, false, modelPath);
            }

            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            if (!string.IsNullOrEmpty(texPath)) AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);

            bool wantsAnimation = animated && spec.isCharacter;
            if (AssetImporter.GetAtPath(modelPath) is ModelImporter mi)
            {
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.importAnimation = wantsAnimation;
                mi.animationType = wantsAnimation ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
                mi.importCameras = false;
                mi.importLights = false;
                mi.generateSecondaryUV = false;
                mi.isReadable = true;
                mi.meshCompression = ModelImporterMeshCompression.Off;
                mi.importBlendShapes = false;
                mi.SaveAndReimport();
                if (wantsAnimation)
                {
                    // 把每个 take 变成循环片段
                    var clips = mi.defaultClipAnimations;
                    foreach (var c in clips) { c.loopTime = true; c.loopPose = false; c.lockRootRotation = true; c.lockRootPositionXZ = true; c.keepOriginalPositionY = true; }
                    if (clips.Length > 0)
                    {
                        mi.clipAnimations = clips;
                        mi.SaveAndReimport();
                    }
                }
            }
            if (!string.IsNullOrEmpty(texPath) && AssetImporter.GetAtPath(texPath) is TextureImporter ti)
            {
                ti.sRGBTexture = true;
                ti.maxTextureSize = 4096;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) throw new Exception("模型导入失败: " + modelPath);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            return BuildPrefabFromInstance(spec, instance, texPath, null, log, wantsAnimation, modelPath);
        }

        private static bool IsJpeg(byte[] bytes) => bytes != null && bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8;

        /// <summary>规范尺寸（高度 = targetHeight，原点在脚底中心）、统一材质、碰撞体与标记，保存成 prefab。</summary>
        private static string BuildPrefabFromInstance(ReferencePropSpec spec, GameObject instance, string texPath, string normalPath, Action<string> log, bool wantsAnimation, string modelPath)
        {
            var root = new GameObject(spec.id);
            instance.transform.SetParent(root.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(0f, EditorPrefs.GetFloat(YawPref, 0f), 0f);

            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new Exception("模型里没有 Renderer");
            var bounds = WorldBounds(renderers);
            float height = Mathf.Max(0.001f, bounds.size.y);
            float scale = spec.targetHeight / height;
            instance.transform.localScale = Vector3.one * scale;
            bounds = WorldBounds(renderers);
            instance.transform.localPosition = -new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            // 统一用绘本铅笔材质 + AI 贴图
            Texture2D tex = string.IsNullOrEmpty(texPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) tex = FindEmbeddedTexture(modelPath);
            Texture2D normalTex = string.IsNullOrEmpty(normalPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            var shader = Shader.Find("Xiaoman/Fairytale Pencil") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = spec.id + "_Pencil", color = Color.white };
            if (tex != null) mat.mainTexture = tex;
            if (normalTex != null && mat.HasProperty("_BumpMap")) { mat.SetTexture("_BumpMap", normalTex); mat.EnableKeyword("_NORMALMAP"); }
            if (mat.HasProperty("_Roughness")) mat.SetFloat("_Roughness", 0.8f);
            if (mat.HasProperty("_HatchStrength")) mat.SetFloat("_HatchStrength", spec.isCharacter ? 0.02f : 0.04f);
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", FairytaleArt.Ink);
            if (tex == null) log?.Invoke("注意：没有找到基础色贴图，模型将显示为白模");
            Directory.CreateDirectory(PrefabFolder);
            string matPath = PrefabFolder + "/" + spec.id + "_Pencil.mat";
            AssetDatabase.CreateAsset(mat, matPath);
            foreach (var r in renderers)
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }

            // 碰撞体与标记
            bounds = WorldBounds(renderers);
            if (spec.isCharacter)
            {
                var cap = root.AddComponent<CapsuleCollider>();
                cap.radius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * 0.3f, 0.2f, 0.45f);
                cap.height = spec.targetHeight;
                cap.center = new Vector3(0f, spec.targetHeight * 0.5f, 0f);
            }
            else
            {
                var col = root.AddComponent<BoxCollider>();
                col.center = root.transform.InverseTransformPoint(bounds.center);
                col.size = bounds.size;
            }
            var marker = root.AddComponent<GeneratedPropMarker>();
            marker.propId = spec.id;

            // 人物：Animator 循环播放第一段动画（Idle），其余片段作为同一控制器的状态备用
            if (wantsAnimation) SetupAnimator(spec, modelPath, instance, log);

            string prefabPath = PrefabFolder + "/" + spec.id + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            log?.Invoke("Prefab 已生成 " + prefabPath);
            return prefabPath;
        }

        private static Bounds WorldBounds(Renderer[] renderers)
        {
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private static void SetupAnimator(ReferencePropSpec spec, string modelPath, GameObject instance, Action<string> log)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToList();
            if (clips.Count == 0) { log?.Invoke("FBX 里没有动画片段，人物保持静态"); return; }

            string controllerPath = PrefabFolder + "/" + spec.id + "_Anim.controller";
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var layer = controller.layers[0];
            AnimatorState first = null;
            foreach (var clip in clips)
            {
                var state = layer.stateMachine.AddState(clip.name);
                state.motion = clip;
                if (first == null) first = state;
            }
            layer.stateMachine.defaultState = first;

            var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            EditorUtility.SetDirty(controller);
            log?.Invoke($"动画片段 {clips.Count} 段：{string.Join(", ", clips.Select(c => c.name))}，默认播放 {first.name}");
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
