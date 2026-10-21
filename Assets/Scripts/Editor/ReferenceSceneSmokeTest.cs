#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VN.EditorTools.AI3D;
using VN.Multiplayer;
using VN.World;

namespace VN.Editor
{
    /// <summary>可由 Unity -executeMethod 调用：搭建五个参考场景并检查关键对象、后期 shader 与裁剪流程。</summary>
    public static class ReferenceSceneSmokeTest
    {
        [MenuItem("3D世界/测试参考画作场景")]
        public static void Run()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var levelGO = new GameObject("WorldLevel");
            var level = levelGO.AddComponent<WorldLevel>();
            LevelBuilder.BuildPrototype(level);
            var galleries = ReferenceSceneBuilder.Ensure(level);
            if (galleries == null) throw new Exception("galleries not built");
            if (ReferenceSceneBuilder.Ensure(level) != galleries) throw new Exception("Ensure is not idempotent");

            int portals = level.GetComponentsInChildren<PaintingPortal>().Length;
            int zones = level.GetComponentsInChildren<LightingZone>().Length;
            int pianos = level.GetComponentsInChildren<PlayableInstrument>().Count(p => p.timbre == InstrumentAudio.Timbre.Piano);
            int markers = level.GetComponentsInChildren<GeneratedPropMarker>().Length;
            int gulls = level.GetComponentsInChildren<CircleFlight>().Length;
            int shafts = level.GetComponentsInChildren<LightShaft>().Length;
            if (portals != ReferenceSceneLibrary.Scenes.Count * 2) throw new Exception("portal count " + portals);
            if (zones != ReferenceSceneLibrary.Scenes.Count) throw new Exception("zone count " + zones);
            if (pianos != 1) throw new Exception("piano count " + pianos);
            if (markers < 60) throw new Exception("prop count " + markers);
            if (gulls < 10 || shafts < 20) throw new Exception("ambient count gulls=" + gulls + " shafts=" + shafts);

            foreach (var sceneDef in ReferenceSceneLibrary.Scenes)
            {
                if (Resources.Load<Texture2D>("Art/References/" + sceneDef.imageName) == null)
                    throw new Exception("reference image missing " + sceneDef.imageName);
                foreach (var placement in sceneDef.props)
                    if (ReferenceSceneLibrary.Prop(placement.propId) == null)
                        throw new Exception("unknown prop " + placement.propId);
                if (sceneDef.Lighting == null || sceneDef.Lighting.id != sceneDef.lightingId)
                    throw new Exception("lighting preset mismatch " + sceneDef.id);
            }

            var post = Shader.Find("Hidden/Xiaoman/Fairytale PostFX");
            if (post == null || !post.isSupported) throw new Exception("PostFX shader missing or unsupported");
            var pencil = Shader.Find("Xiaoman/Fairytale Pencil");
            if (pencil == null) throw new Exception("Pencil shader missing");

            // 光照导演：Init / 立即切换 / 按位置套用
            var director = levelGO.AddComponent<LightingDirector>();
            director.Init(Camera.main);
            director.SetImmediate(LightingPreset.Library());
            if (director.Current.id != "library") throw new Exception("SetImmediate failed");
            var greenhouse = ReferenceSceneLibrary.Scene("greenhouse");
            director.ApplyForPosition(greenhouse.origin + greenhouse.spawnLocal);
            if (director.Current.id != "greenhouse") throw new Exception("ApplyForPosition failed: " + director.Current.id);

            // 参考图：两种来源都要能取到 —— 原画裁剪（每个候选都有 crop 框，裁出非空 PNG）与设定图（人物要正反两面）
            int sheets = 0, characters = 0;
            bool savedSource = AI3DImporter.PreferPainting;
            try
            {
                AI3DImporter.PreferPainting = true;
                foreach (var spec in ReferenceSceneLibrary.Props.Where(p => p.aiCandidate))
                {
                    if (!AI3DImporter.HasCrop(spec)) throw new Exception("no painting crop for " + spec.id);
                    var crops = AI3DImporter.LoadReferenceImages(spec, null);
                    if (crops.Count != 1 || crops[0] == null || crops[0].Length < 1000) throw new Exception("painting crop empty " + spec.id);
                }
                AI3DImporter.PreferPainting = false;
                foreach (var spec in ReferenceSceneLibrary.Props.Where(p => p.aiCandidate))
                {
                    var images = AI3DImporter.LoadReferenceImages(spec, null);
                    if (images.Count == 0 || images.Any(i => i == null || i.Length < 1000)) throw new Exception("reference image empty " + spec.id);
                    if (spec.isCharacter)
                    {
                        characters++;
                        if (images.Count < 2) throw new Exception("character needs front+back sheets " + spec.id);
                        if (spec.animationActionIds == null || spec.animationActionIds.Length == 0) throw new Exception("character has no animation " + spec.id);
                        if (TripoProvider.PresetFor(spec.animationActionIds[0]).Length == 0) throw new Exception("no tripo preset " + spec.id);
                    }
                    if (AI3DImporter.HasReferenceSheet(spec)) sheets++;
                }
            }
            finally { AI3DImporter.PreferPainting = savedSource; }
            if (characters != 6) throw new Exception("character count " + characters);

            // 泼溅插件、GLB 读取、Marble 分支
            CheckMarbleStack(level);

            Debug.Log($"[Xiaoman Reference] smoke test passed: portals={portals} zones={zones} props={markers} gulls={gulls} shafts={shafts} sheets={sheets} characters={characters} gfx={SystemInfo.graphicsDeviceType} splats={MarbleWorldMarker.RendererSupported}");
            if (Environment.GetEnvironmentVariable("XIAOMAN_REF_SHOTS") is string shotDir && !string.IsNullOrEmpty(shotDir))
                CaptureAll(director, shotDir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        /// <summary>检查高斯泼溅插件与 Marble 导入链路（不联网）：包内 shader、GLB 解析、有世界 prefab 时的场景搭建分支。</summary>
        private static void CheckMarbleStack(WorldLevel level)
        {
            const string pkg = "Packages/org.nesnausk.gaussian-splatting/Shaders/";
            if (AssetDatabase.LoadAssetAtPath<Shader>(pkg + "RenderGaussianSplats.shader") == null) throw new Exception("gaussian splatting shader missing");
            if (AssetDatabase.LoadAssetAtPath<ComputeShader>(pkg + "SplatUtilities.compute") == null) throw new Exception("gaussian splatting compute missing");
            if (typeof(GaussianSplatting.Runtime.GaussianSplatRenderer) == null) throw new Exception("renderer type missing");

            // GLB 往返：一个立方体写成 GLB 再读回
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            byte[] glb = MiniGlb.Write(cubeMesh);
            var back = MiniGlb.LoadMesh(glb, "roundtrip");
            if (back.vertexCount != cubeMesh.vertexCount || back.triangles.Length != cubeMesh.triangles.Length)
                throw new Exception($"glb roundtrip mismatch v={back.vertexCount} t={back.triangles.Length}");
            // 完整模型读取（AI 服务输出走这条路）：镜像到左手系后尺寸不变、法线朝外
            var full = MiniGlb.LoadModel(glb, "roundtrip_full");
            if (full.mesh.vertexCount != cubeMesh.vertexCount || full.materials.Count != 1 || (full.mesh.bounds.size - cubeMesh.bounds.size).magnitude > 1e-4f)
                throw new Exception($"glb full load mismatch v={full.mesh.vertexCount} mats={full.materials.Count} size={full.mesh.bounds.size}");
            var fn = full.mesh.normals; var fv = full.mesh.vertices;
            for (int i = 0; i < fv.Length; i += 7)
                if (Vector3.Dot(fn[i], fv[i]) < 0f) throw new Exception("glb full load: normals point inward (winding not flipped)");
            UnityEngine.Object.DestroyImmediate(full.mesh);
            UnityEngine.Object.DestroyImmediate(cube);
            UnityEngine.Object.DestroyImmediate(back);

            // Marble 分支：临时放一个 World_library prefab，重建画廊后图书馆应改用它并只保留 keep 列表里的道具
            if (!MarbleWorldMarker.RendererSupported)
            {
                Debug.LogWarning("[Xiaoman Reference] splat renderer not supported on " + SystemInfo.graphicsDeviceType + ", Marble branch not exercised");
                return;
            }
            string prefabPath = MarbleImporter.PrefabPath("library");
            if (System.IO.File.Exists(prefabPath)) { Debug.Log("[Xiaoman Reference] real Marble world present for library, skipping fake"); return; }
            var fake = new GameObject("World_library");
            var marker = fake.AddComponent<MarbleWorldMarker>();
            marker.sceneId = "library";
            marker.spawnLocal = new Vector3(0f, 0f, 0.5f);
            System.IO.Directory.CreateDirectory(AI3DImporter.PrefabFolder);
            PrefabUtility.SaveAsPrefabAsset(fake, prefabPath);
            UnityEngine.Object.DestroyImmediate(fake);
            AssetDatabase.Refresh();
            try
            {
                if (!AIAssetLibrary.HasWorld("library")) throw new Exception("HasWorld(library) false after creating prefab");
                var old = level.transform.Find(ReferenceSceneLibrary.GalleryRootName);
                UnityEngine.Object.DestroyImmediate(old.gameObject);
                UnityEngine.Object.DestroyImmediate(level.transform.Find(ReferenceSceneLibrary.PortalRowName).gameObject);
                var galleries = ReferenceSceneBuilder.Build(level);
                var lib = galleries.transform.Find("Gallery_library");
                if (lib.Find("MarbleWorld_library") == null) throw new Exception("Marble world not instantiated");
                int libProps = lib.GetComponentsInChildren<GeneratedPropMarker>().Length;
                if (libProps != 0) throw new Exception("library should keep no props in Marble mode, got " + libProps);
                var greenhouse = galleries.transform.Find("Gallery_greenhouse");
                if (greenhouse.GetComponentsInChildren<GeneratedPropMarker>().Length < 10) throw new Exception("non-Marble scene lost its props");
                var spawn = lib.Find("Spawn");
                if (spawn == null || (spawn.localPosition - new Vector3(0f, 0f, 0.5f)).sqrMagnitude > 1e-4f) throw new Exception("Marble spawn not applied");
                Debug.Log("[Xiaoman Reference] Marble branch ok (fake world)");
            }
            finally
            {
                AssetDatabase.DeleteAsset(prefabPath);
                AssetDatabase.Refresh();
                // 恢复成普通画廊，后面的截图用
                var g = level.transform.Find(ReferenceSceneLibrary.GalleryRootName);
                if (g != null) UnityEngine.Object.DestroyImmediate(g.gameObject);
                var p = level.transform.Find(ReferenceSceneLibrary.PortalRowName);
                if (p != null) UnityEngine.Object.DestroyImmediate(p.gameObject);
                ReferenceSceneBuilder.Build(level);
            }
        }

        /// <summary>把每个参考场景从出生点视角渲染成 PNG（含后期），用于离线检查效果。</summary>
        [MenuItem("3D世界/截图参考画作场景到 Temp")]
        public static void CaptureFromMenu()
        {
            var level = UnityEngine.Object.FindObjectOfType<WorldLevel>();
            if (level == null) { Debug.LogWarning("没有 WorldLevel"); return; }
            ReferenceSceneBuilder.Ensure(level);
            var director = level.GetComponent<LightingDirector>() ?? level.gameObject.AddComponent<LightingDirector>();
            director.Init(Camera.main);
            CaptureAll(director, System.IO.Path.Combine(Application.dataPath, "../Temp/RefShots"));
        }

        public static void CaptureAll(LightingDirector director, string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            var camGO = new GameObject("RefShotCamera");
            var cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1200f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            var post = camGO.AddComponent<FairytalePostProcess>();
            director.Init(cam);
            Physics.SyncTransforms();

            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var views = new (string id, Vector3 offset, float yaw, float pitch)[]
            {
                ("village", new Vector3(-4f, 1.2f, -16f), 200f, 14f),
                ("greenhouse", Vector3.zero, 0f, 8f),
                ("seaside", Vector3.zero, 0f, 6f),
                ("street", Vector3.zero, 0f, 6f),
                ("library", Vector3.zero, 0f, 6f),
                ("orange", Vector3.zero, 0f, 10f),
            };
            foreach (var view in views)
            {
                Vector3 pos; float yaw = view.yaw;
                if (view.id == "village")
                {
                    pos = view.offset;
                    director.SetImmediate(LightingPreset.Village());
                }
                else
                {
                    var def = ReferenceSceneLibrary.Scene(view.id);
                    pos = def.origin + def.spawnLocal + view.offset;
                    yaw = def.spawnYaw + view.yaw;
                    director.SetImmediate(def.Lighting);
                }
                post.Current = director.Current;
                cam.transform.position = pos + new Vector3(0f, 1.7f, 0f) - Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 1.5f;
                cam.transform.rotation = Quaternion.Euler(view.pitch, yaw, 0f);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, view.id + ".png"), tex.EncodeToPNG());
            }
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(camGO);
            Debug.Log("[Xiaoman Reference] screenshots written to " + dir);
        }
    }
}
#endif
