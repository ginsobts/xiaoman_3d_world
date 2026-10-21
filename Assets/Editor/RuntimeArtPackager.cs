#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 从 Assets/Art/Generated 挑选运行时实际使用的资源，复制到 Resources / StreamingAssets。
/// 保留 Generated 作为完整源资产库，运行包只加载精选副本。
/// </summary>
public static class RuntimeArtPackager
{
    private const string Generated = "Assets/Art/Generated";

    [MenuItem("Tools/Generated Art/Prepare Runtime Pack")]
    public static void Prepare()
    {
        string[] dirs =
        {
            "Assets/Resources/Art/Textures",
            "Assets/Resources/Art/UI",
            "Assets/Resources/Art/VFX",
            "Assets/StreamingAssets/images",
        };
        foreach (string dir in dirs) Directory.CreateDirectory(Absolute(dir));

        string[] textures =
        {
            "Grass", "Dirt", "Cobblestone", "LavenderStone", "WoodPlanks", "RoofTiles",
            "CreamPlaster", "Water", "Bark", "Leaves", "Wildflowers", "Clouds",
            "Sand", "CaveStone", "Crystal", "Parchment",
        };
        foreach (string texture in textures)
            Copy(Generated + "/Environment/" + texture + ".png",
                "Assets/Resources/Art/Textures/" + texture + ".png");

        Copy(Generated + "/UI/DialogueFrame.png", "Assets/Resources/Art/UI/DialoguePanel.png");
        Copy(Generated + "/UI/QuestFrame.png", "Assets/Resources/Art/UI/ParchmentPanel.png");
        Copy(Generated + "/UI/E_Badge.png", "Assets/Resources/Art/UI/E_Badge.png");
        Copy(Generated + "/UI/HeartIcon.png", "Assets/Resources/Art/UI/HeartIcon.png");
        Copy(Generated + "/UI/KeyIcon.png", "Assets/Resources/Art/UI/KeyIcon.png");
        Copy(Generated + "/UI/CrystalIcon.png", "Assets/Resources/Art/UI/CrystalIcon.png");
        Copy(Generated + "/Sky/FairytaleSkyPanorama.jpg", "Assets/Resources/Art/UI/SkyPanorama.jpg");

        // VFX 图集含义：01 金色星芒、06 水晶辉光、07 萤火虫、15 魔法门、16 存档蓝星。
        Copy(Generated + "/VFX/VFX_01.png", "Assets/Resources/Art/VFX/GoldenSparkle.png");
        Copy(Generated + "/VFX/VFX_06.png", "Assets/Resources/Art/VFX/CrystalGlow.png");
        Copy(Generated + "/VFX/VFX_07.png", "Assets/Resources/Art/VFX/Fireflies.png");
        Copy(Generated + "/VFX/VFX_15.png", "Assets/Resources/Art/VFX/DoorSpiral.png");
        Copy(Generated + "/VFX/VFX_16.png", "Assets/Resources/Art/VFX/SaveTwinkle.png");

        // 新角色立绘走原 AVG 的 StreamingAssets 加载链，不改 AssetCache。
        Copy(Generated + "/Characters/Girl_Happy_Portrait.png",
            "Assets/StreamingAssets/images/fairytale_xiaoman_happy.png");
        Copy(Generated + "/Characters/Girl_Worried_Portrait.png",
            "Assets/StreamingAssets/images/fairytale_xiaoman_worried.png");
        Copy(Generated + "/Characters/Girl_Surprised_Portrait.png",
            "Assets/StreamingAssets/images/fairytale_xiaoman_surprised.png");

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        foreach (string texture in textures)
            Configure("Assets/Resources/Art/Textures/" + texture + ".png",
                TextureImporterType.Default, TextureWrapMode.Repeat, false, 512);

        foreach (string file in new[]
                 {
                     "DialoguePanel.png", "ParchmentPanel.png", "E_Badge.png",
                     "HeartIcon.png", "KeyIcon.png", "CrystalIcon.png", "SkyPanorama.jpg"
                 })
            Configure("Assets/Resources/Art/UI/" + file,
                TextureImporterType.Sprite, TextureWrapMode.Clamp, true, 2048);

        foreach (string file in new[]
                 {
                     "GoldenSparkle.png", "CrystalGlow.png", "Fireflies.png",
                     "DoorSpiral.png", "SaveTwinkle.png"
                 })
            Configure("Assets/Resources/Art/VFX/" + file,
                TextureImporterType.Sprite, TextureWrapMode.Clamp, true, 512);

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("RUNTIME_ART_PACK_SUCCESS");
    }

    private static void Copy(string source, string destination)
    {
        string src = Absolute(source);
        if (!File.Exists(src)) throw new FileNotFoundException("Missing generated art", src);
        File.Copy(src, Absolute(destination), true);
    }

    private static void Configure(string path, TextureImporterType type, TextureWrapMode wrap, bool alpha, int maxSize)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = type;
        importer.wrapMode = wrap;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = type == TextureImporterType.Default;
        importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = alpha;
        importer.maxTextureSize = maxSize;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        if (type == TextureImporterType.Sprite)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
        }
        importer.SaveAndReimport();
    }

    private static string Absolute(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
#endif
