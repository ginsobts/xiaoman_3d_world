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

            // 裁剪：每个 AI 候选道具都能从参考图裁出非空 PNG
            int crops = 0;
            foreach (var spec in ReferenceSceneLibrary.Props.Where(p => p.aiCandidate))
            {
                byte[] png = AI3DImporter.CropForSpec(spec);
                if (png == null || png.Length < 1000) throw new Exception("crop too small " + spec.id);
                crops++;
            }

            Debug.Log($"[Xiaoman Reference] smoke test passed: portals={portals} zones={zones} props={markers} gulls={gulls} shafts={shafts} crops={crops}");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
#endif
