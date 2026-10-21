#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using VN.World;

/// <summary>批处理和菜单都能运行的美术包自检。</summary>
public static class FairytaleArtValidation
{
    [MenuItem("Tools/Generated Art/Render Preview")]
    public static void RenderPreview()
    {
        var levelGo = new GameObject("__FairytalePreviewLevel");
        var cameraGo = new GameObject("__FairytalePreviewCamera");
        try
        {
            var level = levelGo.AddComponent<WorldLevel>();
            LevelBuilder.BuildPrototype(level);

            var camera = cameraGo.AddComponent<Camera>();
            camera.transform.position = new Vector3(20f, 14f, -24f);
            camera.transform.LookAt(new Vector3(0f, 2.2f, 4f));
            camera.fieldOfView = 52f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.83f, 0.95f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 120f;

            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();

            string previewDir = Path.Combine(Application.dataPath, "Art", "Generated", "Preview");
            Directory.CreateDirectory(previewDir);
            string path = Path.Combine(previewDir, "FairytaleWorldPreview.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
            Debug.Log("FAIRYTALE_PREVIEW_SUCCESS " + path);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cameraGo);
            UnityEngine.Object.DestroyImmediate(levelGo);
        }
    }

    [MenuItem("Tools/Generated Art/Validate Runtime Art")]
    public static void Run()
    {
        string[] textures =
        {
            "Grass", "Dirt", "Cobblestone", "LavenderStone", "WoodPlanks", "RoofTiles",
            "CreamPlaster", "Water", "Bark", "Leaves", "Wildflowers", "Clouds",
            "Sand", "CaveStone", "Crystal", "Parchment",
        };
        foreach (string name in textures)
            Require(Resources.Load<Texture2D>("Art/Textures/" + name) != null, "texture " + name);

        foreach (string name in new[]
                 {
                     "DialoguePanel", "ParchmentPanel", "E_Badge",
                     "HeartIcon", "KeyIcon", "CrystalIcon", "SkyPanorama"
                 })
            Require(Resources.Load<Sprite>("Art/UI/" + name) != null, "UI sprite " + name);

        foreach (string name in new[]
                 {
                     "GoldenSparkle", "CrystalGlow", "Fireflies", "DoorSpiral", "SaveTwinkle"
                 })
            Require(Resources.Load<Sprite>("Art/VFX/" + name) != null, "VFX sprite " + name);

        Require(Shader.Find("Xiaoman/Fairytale Pencil") != null, "fairytale shader");

        var levelGo = new GameObject("__FairytaleValidationLevel");
        try
        {
            var level = levelGo.AddComponent<WorldLevel>();
            Transform spawn = LevelBuilder.BuildPrototype(level);
            Require(spawn != null, "spawn point");
            int renderers = levelGo.GetComponentsInChildren<Renderer>(true).Length;
            int interactions = levelGo.GetComponentsInChildren<Interactable>(true).Length;
            Require(renderers >= 100, "at least 100 rendered model parts (got " + renderers + ")");
            Require(interactions >= 5, "prototype interactions (got " + interactions + ")");

            var player = LevelBuilder.BuildPlayer(Vector3.zero, 0f);
            try
            {
                Require(player.GetComponent<PlayerController>() != null, "player controller");
                Require(player.GetComponentInChildren<FairytaleWalker>() != null, "storybook walk animation");
                Require(player.GetComponentsInChildren<Renderer>(true).Length >= 12, "explorer model parts");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }

            Debug.Log("FAIRYTALE_ART_VALIDATION_SUCCESS renderers=" + renderers
                + " interactables=" + interactions);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(levelGo);
        }
    }

    private static void Require(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("Fairytale art validation failed: " + label);
    }
}
#endif
