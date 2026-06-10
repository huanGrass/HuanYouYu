using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class TMPStaticSubsetFontBuilder
{
    private const string SourceFontPath = "Assets/Editor/FontSources/NotoSansCJKsc-Regular.otf";
    private const string OutputFontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/NotoSansCJKsc-Subset SDF.asset";
    private const string LegacyFontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/NotoSansCJKsc-Regular SDF.asset";
    private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    private const string LiberationFontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string SharedUiTextSourcePath = "Assets/Common/Resources/Text/ui_texts.shared.zh-CN.json";
    private const string HallRootPath = "Assets/Hall";
    private const string GamesRootPath = "Assets/Games";
    private const string CharacterSetHashRecordPath = "Library/TMPSubsetFontBuildState/character-set.sha256";

    private const string AsciiAndPunctuation =
        " 0123456789" +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
        ".,!?;:'\"`~@#$%^&*()-_=+[]{}<>/\\| " +
        "，。！？；：、（）【】《》“”‘’…—";

    private const string CommonChineseCharacters =
        "的一是了不在人有我他这中大来上个国到说们为子和你地出道也时要就下以生会自着去之过家学对可里后小心多天而能好都然没日于起发成只如事把还用第样道想作种开见经法现当点最本间定行所情者新前后同" +
        "请确认返回继续开始暂停退出完成提示图案填黑打叉重置换题当前模式关卡游戏分数长度最高自动重排消除移动相邻选择开发中可游玩数织小船苹果笑脸上下左右";

    private static readonly Regex EscapedUnicodeRegex = new Regex(@"\\u([0-9a-fA-F]{4})", RegexOptions.Compiled);

    [MenuItem("Tools/TMP/构建静态子集字体")]
    public static void BuildDefaultSubsetFont()
    {
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            throw new InvalidOperationException("Source font not found: " + SourceFontPath);
        }

        string characterSet = BuildCharacterSet();
        if (string.IsNullOrEmpty(characterSet))
        {
            throw new InvalidOperationException("Generated character set is empty.");
        }

        string currentCharacterSetHash = ComputeSha256(characterSet);
        if (HasUnchangedCharacterSet(currentCharacterSetHash))
        {
            Debug.Log("TMP subset character set unchanged, skipping rebuild.");
            return;
        }

        Debug.Log("TMP subset character count: " + characterSet.Length);

        TMP_FontAsset oldSubset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputFontAssetPath);
        if (oldSubset != null)
        {
            AssetDatabase.DeleteAsset(OutputFontAssetPath);
        }

        TMP_FontAsset subsetFontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            72,
            8,
            GlyphRenderMode.SDFAA,
            1024,
            1024,
            AtlasPopulationMode.Dynamic,
            true);

        subsetFontAsset.name = "NotoSansCJKsc-Subset SDF";

        if (!subsetFontAsset.TryAddCharacters(characterSet, out string missingCharacters, true) && !string.IsNullOrEmpty(missingCharacters))
        {
            Debug.LogWarning("Missing characters in subset atlas: " + missingCharacters);
        }

        subsetFontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

        AssetDatabase.CreateAsset(subsetFontAsset, OutputFontAssetPath);
        AttachSubAssets(subsetFontAsset);

        TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
        if (settings == null)
        {
            throw new InvalidOperationException("TMP Settings not found: " + TmpSettingsPath);
        }

        TMP_FontAsset liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationFontAssetPath);
        SerializedObject settingsSerialized = new SerializedObject(settings);
        settingsSerialized.FindProperty("m_defaultFontAsset").objectReferenceValue = subsetFontAsset;
        SerializedProperty fallbackProperty = settingsSerialized.FindProperty("m_fallbackFontAssets");
        fallbackProperty.ClearArray();
        if (liberation != null)
        {
            fallbackProperty.InsertArrayElementAtIndex(0);
            fallbackProperty.GetArrayElementAtIndex(0).objectReferenceValue = liberation;
        }
        settingsSerialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(settings);
        EditorUtility.SetDirty(subsetFontAsset);

        TMP_FontAsset legacyFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LegacyFontAssetPath);
        if (legacyFontAsset != null)
        {
            AssetDatabase.DeleteAsset(LegacyFontAssetPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SaveCharacterSetHash(currentCharacterSetHash);

        Debug.Log("TMP static subset font build completed: " + OutputFontAssetPath);
    }

    private static void AttachSubAssets(TMP_FontAsset fontAsset)
    {
        if (fontAsset.atlasTextures != null)
        {
            foreach (Texture2D texture in fontAsset.atlasTextures)
            {
                if (texture == null)
                {
                    continue;
                }

                string texturePath = AssetDatabase.GetAssetPath(texture);
                if (string.IsNullOrEmpty(texturePath))
                {
                    AssetDatabase.AddObjectToAsset(texture, fontAsset);
                }
            }
        }

        if (fontAsset.material != null)
        {
            string materialPath = AssetDatabase.GetAssetPath(fontAsset.material);
            if (string.IsNullOrEmpty(materialPath))
            {
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }
        }
    }

    private static string BuildCharacterSet()
    {
        HashSet<int> codePoints = new HashSet<int>();
        AddStringCodePoints(codePoints, AsciiAndPunctuation);
        AddStringCodePoints(codePoints, CommonChineseCharacters);
        AddStringCodePoints(codePoints, "★☆♥");

        var textSourceFiles = ResolveUiTextSourceFiles();
        if (textSourceFiles.Count == 0)
        {
            throw new FileNotFoundException("UI text source not found.");
        }

        for (var i = 0; i < textSourceFiles.Count; i++)
        {
            string text = File.ReadAllText(textSourceFiles[i]);
            AddTextCodePoints(codePoints, text);
            AddEscapedUnicodeCodePoints(codePoints, text);
        }

        List<int> ordered = codePoints.ToList();
        ordered.Sort();

        return string.Concat(ordered.Select(char.ConvertFromUtf32));
    }

    private static void AddTextCodePoints(HashSet<int> codePoints, string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            int codePoint = char.ConvertToUtf32(text, i);
            if (char.IsSurrogatePair(text, i))
            {
                i++;
            }

            if (!ShouldIncludeCodePoint(codePoint))
            {
                continue;
            }

            codePoints.Add(codePoint);
        }
    }

    private static void AddStringCodePoints(HashSet<int> codePoints, string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            int codePoint = char.ConvertToUtf32(value, i);
            if (char.IsSurrogatePair(value, i))
            {
                i++;
            }

            codePoints.Add(codePoint);
        }
    }

    private static void AddEscapedUnicodeCodePoints(HashSet<int> codePoints, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var matches = EscapedUnicodeRegex.Matches(text);
        for (var i = 0; i < matches.Count; i++)
        {
            var hex = matches[i].Groups[1].Value;
            int codePoint;
            if (!int.TryParse(hex, NumberStyles.HexNumber, null, out codePoint))
            {
                continue;
            }

            if (!ShouldIncludeCodePoint(codePoint))
            {
                continue;
            }

            codePoints.Add(codePoint);
        }
    }

    private static bool ShouldIncludeCodePoint(int codePoint)
    {
        if (codePoint < 0x20)
        {
            return false;
        }

        if (codePoint >= 0x20 && codePoint <= 0x7E)
        {
            return true;
        }

        // CJK radicals, symbols / punctuation, Hiragana / Katakana, CJK Unified Ideographs and extensions.
        if (codePoint >= 0x2E80 && codePoint <= 0xA4CF)
        {
            return true;
        }

        // CJK compatibility ideographs.
        if (codePoint >= 0xF900 && codePoint <= 0xFAFF)
        {
            return true;
        }

        // Full-width forms and Chinese punctuation.
        if (codePoint >= 0xFF00 && codePoint <= 0xFFEF)
        {
            return true;
        }

        // Misc symbols and dingbats.
        if (codePoint >= 0x2600 && codePoint <= 0x27BF)
        {
            return true;
        }

        // Misc symbols and arrows (includes U+2B50 WHITE MEDIUM STAR).
        if (codePoint >= 0x2B00 && codePoint <= 0x2BFF)
        {
            return true;
        }

        return false;
    }

    private static bool HasUnchangedCharacterSet(string currentCharacterSetHash)
    {
        if (string.IsNullOrEmpty(currentCharacterSetHash))
        {
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputFontAssetPath) == null)
        {
            return false;
        }

        string recordPath = GetAbsolutePath(CharacterSetHashRecordPath);
        if (!File.Exists(recordPath))
        {
            return false;
        }

        string previousHash = File.ReadAllText(recordPath).Trim();
        return string.Equals(previousHash, currentCharacterSetHash, StringComparison.OrdinalIgnoreCase);
    }

    private static void SaveCharacterSetHash(string currentCharacterSetHash)
    {
        string recordPath = GetAbsolutePath(CharacterSetHashRecordPath);
        string directory = Path.GetDirectoryName(recordPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(recordPath, currentCharacterSetHash, new UTF8Encoding(false));
    }

    private static string ComputeSha256(string value)
    {
        using (var sha256 = SHA256.Create())
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            byte[] hashBytes = sha256.ComputeHash(inputBytes);
            return string.Concat(hashBytes.Select(b => b.ToString("x2")));
        }
    }

    private static string GetAbsolutePath(string assetRelativePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot))
        {
            throw new InvalidOperationException("Unable to resolve project root.");
        }

        return Path.Combine(projectRoot, assetRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static List<string> ResolveUiTextSourceFiles()
    {
        var files = new List<string>();
        var sharedFullPath = GetAbsolutePath(SharedUiTextSourcePath);
        if (File.Exists(sharedFullPath))
        {
            files.Add(sharedFullPath);
        }

        AddUiTextSourceFiles(files, HallRootPath);
        AddJsonTextSourceFiles(files, "Assets/Hall/Resources/Announcements", "*.zh-CN.json");
        AddUiTextSourceFiles(files, GamesRootPath);
        AddJsonTextSourceFiles(files, GamesRootPath, "*content_graph*.json");

        return files;
    }

    private static void AddUiTextSourceFiles(List<string> files, string assetRelativeRootPath)
    {
        var rootFullPath = GetAbsolutePath(assetRelativeRootPath);
        if (!Directory.Exists(rootFullPath))
        {
            return;
        }

        var uiTextFiles = Directory.GetFiles(rootFullPath, "*.ui_texts.zh-CN.json", SearchOption.AllDirectories);
        Array.Sort(uiTextFiles, StringComparer.OrdinalIgnoreCase);
        files.AddRange(uiTextFiles);
    }

    private static void AddJsonTextSourceFiles(List<string> files, string assetRelativeRootPath, string searchPattern)
    {
        var rootFullPath = GetAbsolutePath(assetRelativeRootPath);
        if (!Directory.Exists(rootFullPath))
        {
            return;
        }

        var jsonFiles = Directory.GetFiles(rootFullPath, searchPattern, SearchOption.AllDirectories);
        Array.Sort(jsonFiles, StringComparer.OrdinalIgnoreCase);
        files.AddRange(jsonFiles);
    }
}

