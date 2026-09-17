using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// 将生成源图的独立格子导入为可重复编辑的游戏资源；源图不放进 Resources。
public static class ForestArtImporter
{
    [Serializable] class Footprint { public string name; public float width, height, canvasWidth, canvasHeight; }
    [Serializable] class Footprints { public Footprint[] items; }
    static Footprint[] footprints;
    const string Source = "Assets/Games/StardewAI/ArtSource/";
    const string Output = "Assets/Games/StardewAI/Resources/Art/";
    static readonly string[] Parts = { "Head", "Body", "UpperArm", "Forearm", "UnusedLeg", "Calf", "Hair0", "Top0", "Sleeve0", "Hips0", "Leg0", "Shoe0", "Hair1", "Top1", "Sleeve1", "Hips1", "Leg1", "Shoe1", "Scarf0", "Scarf1", "Hat", "Pouch0", "Pouch1" };

    [MenuItem("Tools/StardewAI/Import B Character Sample")]
    public static void BuildB()
    {
        Texture2D source = Read("BParts");
        string[] names = { "Head", "Hair", "Top", "Hips", "NearUpperArm", "NearForearm", "FarUpperArm", "FarForearm",
            "NearThigh", "NearCalf", "NearShoe", "Scarf", "FarThigh", "FarCalf", "FarShoe" };
        int[] rows = { 0, 300, 550, 770, 1024 };
        for (int i = 0; i < names.Length; i++)
        {
            int row = i / 4;
            Texture2D part = Cut(source, i % 4 * 384, rows[row], 384, rows[row + 1] - rows[row], true, true);
            if (names[i] == "Hips")
            {
                Texture2D band = Cut(part, 0, 0, part.width, Mathf.RoundToInt(part.height * .6f), false);
                UnityEngine.Object.DestroyImmediate(part); part = band;
            }
            if (names[i].EndsWith("UpperArm", StringComparison.Ordinal))
            {
                // 肘部由前臂覆盖；切片不保留上臂末端封口，避免叠成横向接缝。
                Texture2D overlap = Cut(part, 0, 0, part.width, part.height - 12, false);
                UnityEngine.Object.DestroyImmediate(part); part = overlap;
            }
            Save(part, Output + "ForestB/" + names[i] + ".png", 64, Vector2.one * .5f);
        }
        UnityEngine.Object.DestroyImmediate(source);
        var sample = new GameObject("ForestBCharacter");
        sample.AddComponent<FarmPrototype.ForestBCharacterRig>();
        PrefabUtility.SaveAsPrefabAsset(sample, Output + "ForestB/Character.prefab");
        UnityEngine.Object.DestroyImmediate(sample);
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/StardewAI/Import Dream Forest Art")]
    public static void Build()
    {
        footprints = JsonUtility.FromJson<Footprints>(File.ReadAllText(Source + "原占位尺寸.json")).items;
        foreach (string view in new[] { "Front", "Side", "Back" })
        {
            Texture2D source = Read(view);
            int[] xs = { 0, 320, 600, 800, 1040, 1240, 1536 };
            int[] ys = { 0, 280, 530, 760, 1024 };
            for (int i = 0; i < Parts.Length; i++)
            {
                if (Parts[i] == "UnusedLeg" || Parts[i].StartsWith("Pouch")) continue;
                int c = i % 6, r = i / 6;
                Texture2D part = Cut(source, xs[c], ys[r], xs[c + 1] - xs[c], ys[r + 1] - ys[r], true);
                // 袖子单独随上臂转动，躯干只保留中间衣身，防止重复的固定袖子。
                if (Parts[i].StartsWith("Top") && view != "Side")
                {
                    Texture2D trimmed = Cut(part, Mathf.RoundToInt(part.width * .18f), 0,
                        Mathf.RoundToInt(part.width * .64f), part.height, false);
                    UnityEngine.Object.DestroyImmediate(part);
                    part = trimmed;
                }
                Save(part, Output + "ForestCharacter/" + view + "/" + Parts[i] + ".png", 100, new Vector2(.5f, .5f));
            }
            UnityEngine.Object.DestroyImmediate(source);
        }
        ImportSheet("Props", 3, 2, new[] { "PropSprites/Cabin", "PropSprites/TreeTall", "PropSprites/TreeRound", "PropSprites/Bush", "PropSprites/ShippingBin", "PropSprites/SeedChest" }, true);
        ImportSheet("Ground", 4, 2, new[] { "TerrainTiles/GrassA", "TerrainTiles/GrassB", "TerrainTiles/GrassFlowers", "TerrainTiles/Path", "TerrainTiles/Water", "TerrainTiles/FieldBase", "TerrainTiles/SoilDry", "TerrainTiles/SoilWet" }, false);
        ImportSheet("Items", 4, 5, new[] { "TerrainTiles/Fence", "TerrainTiles/FlowerOrange", "TerrainTiles/FlowerYellow", "TerrainTiles/CropSeed", "TerrainTiles/CropSprout", "TerrainTiles/CropLeafy", "TerrainTiles/CropRipe", "ToolSprites/ToolHoe", "ToolSprites/ToolWateringCan", "ToolSprites/ToolSickle", "ToolSprites/ToolSeedBag", "ItemIcons/CropSeed", "ItemIcons/CropRipe", "EffectSprites/EffectHoeHit", "EffectSprites/EffectWaterHit", "EffectSprites/EffectHarvestHit", "EffectSprites/TargetOutline", "EffectSprites/Shadow", "EffectSprites/EffectSeedHit" }, true);
        ImportSheet("Npcs", 5, 1, new[] { "CharacterSprites/NpcLumi", "CharacterSprites/NpcXiaoTuanzi", "CharacterSprites/NpcQianran", "CharacterSprites/NpcHaiyinAwa", "CharacterSprites/NpcAzhai" }, true);
        AssetDatabase.Refresh();
        Debug.Log("Dream Forest art import complete.");
    }

    static Texture2D Read(string name)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(File.ReadAllBytes(Source + name + ".png"))) throw new IOException("Invalid source: " + name);
        return texture;
    }

    static void ImportSheet(string name, int columns, int rows, string[] names, bool removeBackground)
    {
        if (!File.Exists(Source + name + ".png")) return;
        Texture2D source = Read(name);
        for (int i = 0; i < names.Length; i++)
        {
            string path = Output + "Generated/" + names[i] + ".png";
            Footprint footprint = Array.Find(footprints, item => item.name == names[i]);
            if (footprint == null) throw new InvalidDataException("Missing art footprint: " + names[i]);
            Vector2 worldSize = new Vector2(footprint.canvasWidth, footprint.canvasHeight);
            Texture2D tile = Cut(source, i % columns * source.width / columns, i / columns * source.height / rows,
                source.width / columns, source.height / rows, removeBackground);
            // 地块为正方形；装饰和角色保留旧世界占位大小，以免改变碰撞地图。
            int width = removeBackground ? Mathf.Max(16, Mathf.RoundToInt(worldSize.x * 96)) : 64;
            int height = removeBackground ? Mathf.Max(16, Mathf.RoundToInt(worldSize.y * 96)) : 64;
            var normalized = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color32[] original = tile.GetPixels32(), pixels = new Color32[width * height];
            float scale = Mathf.Min(footprint.width * 96 / tile.width, footprint.height * 96 / tile.height);
            int drawWidth = removeBackground ? Mathf.RoundToInt(tile.width * scale) : width;
            int drawHeight = removeBackground ? Mathf.RoundToInt(tile.height * scale) : height;
            int offsetX = (width - drawWidth) / 2, offsetY = (height - drawHeight) / 2;
            for (int y = 0; y < drawHeight; y++) for (int x = 0; x < drawWidth; x++)
                pixels[(y + offsetY) * width + x + offsetX] = original[Mathf.Min(tile.height - 1, y * tile.height / drawHeight) * tile.width + Mathf.Min(tile.width - 1, x * tile.width / drawWidth)];
            normalized.SetPixels32(pixels); normalized.Apply();
            Save(normalized, path, removeBackground ? 96 : 64, new Vector2(.5f, name == "Npcs" ? .3f : .5f));
            UnityEngine.Object.DestroyImmediate(tile);
        }
        UnityEngine.Object.DestroyImmediate(source);
    }

    static Texture2D Cut(Texture2D source, int x, int top, int width, int height, bool clear, bool magenta = false)
    {
        Color[] pixels = source.GetPixels(x, source.height - top - height, width, height);
        int left = width, right = -1, bottom = height, upper = -1;
        for (int py = 0; py < height; py++) for (int px = 0; px < width; px++)
        {
            int index = py * width + px;
            Color c = pixels[index];
            float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b)), max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            bool background = magenta ? c.r > .6f && c.b > .6f && c.g < .35f : min > .82f && max - min < .085f;
            if (clear && background) pixels[index] = Color.clear;
            else if (c.a > .1f) { left = Mathf.Min(left, px); right = Mathf.Max(right, px); bottom = Mathf.Min(bottom, py); upper = Mathf.Max(upper, py); }
        }
        if (!clear)
        {
            var opaque = new Texture2D(width, height, TextureFormat.RGBA32, false);
            opaque.SetPixels(pixels); opaque.Apply(); return opaque;
        }
        if (right < left) throw new InvalidDataException("Empty atlas cell");
        var result = new Texture2D(right - left + 3, upper - bottom + 3, TextureFormat.RGBA32, false);
        var cropped = new Color[result.width * result.height];
        for (int py = bottom; py <= upper; py++) for (int px = left; px <= right; px++)
            cropped[(py - bottom + 1) * result.width + px - left + 1] = pixels[py * width + px];
        result.SetPixels(cropped); result.Apply();
        return result;
    }

    static void Save(Texture2D texture, string path, float ppu, Vector2 pivot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot; importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }
}
