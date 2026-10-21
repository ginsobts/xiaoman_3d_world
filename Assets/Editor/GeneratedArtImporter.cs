#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class GeneratedArtImporter
{
    private const string Root = "Assets/Art/Generated";
    private const string ExternalRoot = @"C:\Users\xumei\.cursor\projects\c-Users-xumei-cgjpetgame\assets";

    private static readonly string[] SourceFiles =
    {
        "fairytale_environment_atlas.jpg",
        "fairytale_character_sheet.jpg",
        "fairytale_ui_atlas.jpg",
        "fairytale_props_atlas.jpg",
        "fairytale_sky_panorama.jpg",
        "fairytale_vfx_atlas.jpg"
    };

    private static readonly string[] EnvironmentNames =
    {
        "Grass", "Dirt", "Cobblestone", "LavenderStone",
        "WoodPlanks", "RoofTiles", "CreamPlaster", "Water",
        "Bark", "Leaves", "Wildflowers", "Clouds",
        "Sand", "CaveStone", "Crystal", "Parchment"
    };

    private struct Crop
    {
        public readonly string Name;
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;

        public Crop(string name, int x, int y, int width, int height)
        {
            Name = name;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    private static readonly Crop[] CharacterCrops =
    {
        new Crop("Girl_Front_FullBody", 10, 45, 175, 545),
        new Crop("Girl_Side_FullBody", 185, 45, 160, 545),
        new Crop("Girl_Back_FullBody", 335, 45, 175, 545),
        new Crop("Boy_Front_FullBody", 525, 45, 165, 545),
        new Crop("Boy_Side_FullBody", 690, 45, 155, 545),
        new Crop("Boy_Back_FullBody", 835, 45, 180, 545),
        new Crop("Girl_Happy_Portrait", 0, 625, 175, 300),
        new Crop("Girl_Worried_Portrait", 170, 625, 175, 300),
        new Crop("Girl_Surprised_Portrait", 340, 625, 175, 300),
        new Crop("Boy_Happy_Portrait", 510, 625, 175, 300),
        new Crop("Boy_Thinking_Portrait", 680, 625, 175, 300),
        new Crop("Boy_Surprised_Portrait", 850, 625, 174, 300)
    };

    private static readonly Crop[] UiCrops =
    {
        new Crop("DialogueFrame", 35, 12, 465, 172),
        new Crop("QuestFrame", 520, 8, 450, 178),
        new Crop("HeartIcon", 535, 188, 100, 90),
        new Crop("KeyIcon", 635, 185, 110, 100),
        new Crop("CrystalIcon", 745, 180, 95, 110),
        new Crop("SatchelIcon", 830, 185, 145, 100),
        new Crop("MapIcon", 645, 280, 130, 105),
        new Crop("SaveIcon", 775, 285, 90, 100),
        new Crop("SettingsIcon", 865, 280, 110, 105),
        new Crop("E_Badge", 330, 405, 120, 125),
        new Crop("CursorIcon", 235, 425, 75, 100)
    };

    [MenuItem("Tools/Generated Art/Import and Process")]
    public static void ImportAndProcess()
    {
        try
        {
            ValidateSources();
            EnsureCleanOutput();
            CopySources();

            Texture2D environment = LoadExternal("fairytale_environment_atlas.jpg");
            for (int i = 0; i < EnvironmentNames.Length; i++)
            {
                int column = i % 4;
                int rowFromTop = i / 4;
                Texture2D cell = CropTopLeft(environment, column * 256, rowFromTop * 256, 256, 256);
                string path = Root + "/Environment/" + EnvironmentNames[i] + ".png";
                WritePng(cell, path);
                UnityEngine.Object.DestroyImmediate(cell);
            }
            UnityEngine.Object.DestroyImmediate(environment);

            Texture2D vfx = LoadExternal("fairytale_vfx_atlas.jpg");
            for (int i = 0; i < 16; i++)
            {
                Texture2D cell = CropTopLeft(vfx, (i % 4) * 256, (i / 4) * 256, 256, 256);
                ApplyLuminanceAlpha(cell);
                string path = Root + "/VFX/VFX_" + (i + 1).ToString("00") + ".png";
                WritePng(cell, path);
                UnityEngine.Object.DestroyImmediate(cell);
            }
            UnityEngine.Object.DestroyImmediate(vfx);

            Texture2D characters = LoadExternal("fairytale_character_sheet.jpg");
            foreach (Crop crop in CharacterCrops)
            {
                Texture2D sprite = CropTopLeft(characters, crop.X, crop.Y, crop.Width, crop.Height);
                RemoveEdgeConnectedPaper(sprite);
                WritePng(sprite, Root + "/Characters/" + crop.Name + ".png");
                UnityEngine.Object.DestroyImmediate(sprite);
            }
            UnityEngine.Object.DestroyImmediate(characters);

            Texture2D ui = LoadExternal("fairytale_ui_atlas.jpg");
            foreach (Crop crop in UiCrops)
            {
                Texture2D sprite = CropTopLeft(ui, crop.X, crop.Y, crop.Width, crop.Height);
                RemoveGreenScreen(sprite);
                WritePng(sprite, Root + "/UI/" + crop.Name + ".png");
                UnityEngine.Object.DestroyImmediate(sprite);
            }
            UnityEngine.Object.DestroyImmediate(ui);

            File.Copy(Path.Combine(ExternalRoot, "fairytale_props_atlas.jpg"), Absolute(Root + "/Reference/PropsConceptAtlas.jpg"), true);
            File.Copy(Path.Combine(ExternalRoot, "fairytale_sky_panorama.jpg"), Absolute(Root + "/Sky/FairytaleSkyPanorama.jpg"), true);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporters();
            WriteReadme();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("GENERATED_ART_IMPORT_SUCCESS: Created generated art under " + Root);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static void ValidateSources()
    {
        foreach (string fileName in SourceFiles)
        {
            string path = Path.Combine(ExternalRoot, fileName);
            if (!File.Exists(path))
                throw new FileNotFoundException("Generated source image not found.", path);
        }
    }

    private static void EnsureCleanOutput()
    {
        Directory.CreateDirectory(Absolute(Root));
        foreach (string folder in new[] { "Source", "Environment", "VFX", "Characters", "UI", "Reference", "Sky" })
        {
            string path = Absolute(Root + "/" + folder);
            if (Directory.Exists(path))
                Directory.Delete(path, true);
            Directory.CreateDirectory(path);
        }
    }

    private static void CopySources()
    {
        foreach (string fileName in SourceFiles)
            File.Copy(Path.Combine(ExternalRoot, fileName), Absolute(Root + "/Source/" + fileName), true);
    }

    private static Texture2D LoadExternal(string fileName)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(ExternalRoot, fileName));
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, bytes, false))
            throw new InvalidOperationException("Could not decode " + fileName);
        return texture;
    }

    private static Texture2D CropTopLeft(Texture2D source, int x, int yFromTop, int width, int height)
    {
        int clampedWidth = Mathf.Min(width, source.width - x);
        int bottom = source.height - yFromTop - height;
        int clampedBottom = Mathf.Max(0, bottom);
        int clampedHeight = Mathf.Min(height, source.height - clampedBottom);
        Color32[] pixels = source.GetPixels32();
        Color32[] cropped = new Color32[clampedWidth * clampedHeight];

        for (int row = 0; row < clampedHeight; row++)
            Array.Copy(pixels, (clampedBottom + row) * source.width + x, cropped, row * clampedWidth, clampedWidth);

        Texture2D result = new Texture2D(clampedWidth, clampedHeight, TextureFormat.RGBA32, false);
        result.SetPixels32(cropped);
        result.Apply(false, false);
        return result;
    }

    private static void ApplyLuminanceAlpha(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 color = pixels[i];
            float luminance = (0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b) / 255f;
            color.a = (byte)Mathf.RoundToInt(Mathf.Clamp01((luminance - 0.015f) / 0.985f) * 255f);
            pixels[i] = color;
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }

    private static void RemoveGreenScreen(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        Color key = new Color(0f, 1f, 0.04f);
        for (int i = 0; i < pixels.Length; i++)
        {
            Color color = pixels[i];
            float distance = Vector3.Distance(new Vector3(color.r, color.g, color.b), new Vector3(key.r, key.g, key.b));
            if (color.g > 0.55f && color.g > color.r * 1.35f && color.g > color.b * 1.2f)
            {
                float alpha = Mathf.InverseLerp(0.18f, 0.52f, distance);
                color.a *= alpha;
                color.g = Mathf.Min(color.g, Mathf.Max(color.r, color.b) + 0.08f);
            }
            pixels[i] = color;
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }

    private static void RemoveEdgeConnectedPaper(Texture2D texture)
    {
        int width = texture.width;
        int height = texture.height;
        Color32[] pixels = texture.GetPixels32();
        bool[] visited = new bool[pixels.Length];
        Queue<int> queue = new Queue<int>();
        Color32 reference = AverageCorners(pixels, width, height);

        for (int x = 0; x < width; x++)
        {
            Enqueue(x, pixels, visited, queue, reference);
            Enqueue((height - 1) * width + x, pixels, visited, queue, reference);
        }
        for (int y = 0; y < height; y++)
        {
            Enqueue(y * width, pixels, visited, queue, reference);
            Enqueue(y * width + width - 1, pixels, visited, queue, reference);
        }

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            Color32 color = pixels[index];
            color.a = 0;
            pixels[index] = color;
            int x = index % width;
            int y = index / width;
            if (x > 0) Enqueue(index - 1, pixels, visited, queue, reference);
            if (x + 1 < width) Enqueue(index + 1, pixels, visited, queue, reference);
            if (y > 0) Enqueue(index - width, pixels, visited, queue, reference);
            if (y + 1 < height) Enqueue(index + width, pixels, visited, queue, reference);
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }

    private static void Enqueue(int index, Color32[] pixels, bool[] visited, Queue<int> queue, Color32 reference)
    {
        if (visited[index])
            return;
        visited[index] = true;
        Color32 color = pixels[index];
        int spread = Math.Max(color.r, Math.Max(color.g, color.b)) - Math.Min(color.r, Math.Min(color.g, color.b));
        int delta = Math.Abs(color.r - reference.r) + Math.Abs(color.g - reference.g) + Math.Abs(color.b - reference.b);
        if (spread < 48 && delta < 150 && color.r > 185 && color.g > 175 && color.b > 155)
            queue.Enqueue(index);
    }

    private static Color32 AverageCorners(Color32[] pixels, int width, int height)
    {
        Color32[] corners = { pixels[0], pixels[width - 1], pixels[(height - 1) * width], pixels[pixels.Length - 1] };
        int r = 0, g = 0, b = 0;
        foreach (Color32 corner in corners)
        {
            r += corner.r;
            g += corner.g;
            b += corner.b;
        }
        return new Color32((byte)(r / 4), (byte)(g / 4), (byte)(b / 4), 255);
    }

    private static void WritePng(Texture2D texture, string assetPath)
    {
        File.WriteAllBytes(Absolute(assetPath), texture.EncodeToPNG());
    }

    private static void ConfigureImporters()
    {
        foreach (string name in EnvironmentNames)
            Configure(Root + "/Environment/" + name + ".png", TextureImporterType.Default, TextureWrapMode.Repeat, false, 256);

        for (int i = 1; i <= 16; i++)
            Configure(Root + "/VFX/VFX_" + i.ToString("00") + ".png", TextureImporterType.Sprite, TextureWrapMode.Clamp, true, 256);

        foreach (Crop crop in CharacterCrops)
            Configure(Root + "/Characters/" + crop.Name + ".png", TextureImporterType.Sprite, TextureWrapMode.Clamp, true, 1024);

        foreach (Crop crop in UiCrops)
            Configure(Root + "/UI/" + crop.Name + ".png", TextureImporterType.Sprite, TextureWrapMode.Clamp, true, 512);

        Configure(Root + "/Sky/FairytaleSkyPanorama.jpg", TextureImporterType.Default, TextureWrapMode.Clamp, false, 2048);
        Configure(Root + "/Reference/PropsConceptAtlas.jpg", TextureImporterType.Default, TextureWrapMode.Clamp, false, 2048);

        foreach (string fileName in SourceFiles)
            Configure(Root + "/Source/" + fileName, TextureImporterType.Default, TextureWrapMode.Clamp, false, 2048);
    }

    private static void Configure(string path, TextureImporterType type, TextureWrapMode wrap, bool alpha, int maxSize)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("TextureImporter missing for " + path);

        importer.textureType = type;
        importer.wrapMode = wrap;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = type == TextureImporterType.Default;
        importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = alpha;
        importer.maxTextureSize = maxSize;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        if (type == TextureImporterType.Sprite)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
        }
        importer.SaveAndReimport();
    }

    private static void WriteReadme()
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("# Generated Fairytale Art");
        text.AppendLine();
        text.AppendLine("Created by `Assets/Editor/GeneratedArtImporter.cs` from the six generated JPG sheets. Re-run with **Tools > Generated Art > Import and Process**.");
        text.AppendLine();
        text.AppendLine("## Contents");
        text.AppendLine("- `Source/`: untouched copies of all six original JPG files.");
        text.AppendLine("- `Environment/`: 16 named 256x256 PNG texture cells: " + string.Join(", ", EnvironmentNames) + ". Imported as Default textures with Repeat wrapping.");
        text.AppendLine("- `VFX/`: `VFX_01.png` through `VFX_16.png`, 256x256 Sprite PNGs. Black is transparent and alpha follows luminance.");
        text.AppendLine("- `Characters/`: 6 full-body and 6 portrait Sprite PNG crops. Edge-connected paper backgrounds were made transparent.");
        text.AppendLine("- `UI/`: dialogue/quest frames, E badge, cursor, and common icon Sprite PNG crops. Bright green chroma was removed.");
        text.AppendLine("- `Sky/FairytaleSkyPanorama.jpg`: panorama copy imported as a clamped Default texture.");
        text.AppendLine("- `Reference/PropsConceptAtlas.jpg`: intact props concept sheet imported as a clamped Default texture.");
        text.AppendLine();
        text.AppendLine("## Notes and limitations");
        text.AppendLine("- Character and UI crops are derived from a single AI-generated concept sheet, so edges can retain slight paper/chroma fringing and are not rigged or animation-ready.");
        text.AppendLine("- The props source has no regular grid and remains an intact reference atlas rather than risking destructive automatic crops.");
        text.AppendLine("- The sky image is 16:9 artwork, not a seamless 360-degree equirectangular panorama.");
        text.AppendLine("- VFX RGB is preserved while alpha uses perceived luminance; additive materials may still give the best result.");
        File.WriteAllText(Absolute(Root + "/README.md"), text.ToString(), new UTF8Encoding(false));
    }

    private static string Absolute(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
#endif
