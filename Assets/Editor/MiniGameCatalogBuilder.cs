using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace HuanYouYu.MiniGameHall.EditorTools
{
    public static class MiniGameCatalogBuilder
    {
        private const string GamesRootPath = "Assets/Games";
        private const string CatalogOrderSourcePath = "Assets/Hall/Config/mini_game_catalog_order.json";
        private const string SharedTextSourcePath = "Assets/Common/Resources/Text/ui_texts.shared.zh-CN.json";
        private const string OutputCatalogAssetPath = "Assets/Hall/Resources/MiniGameCatalogConfig.asset";
        private const string OutputRuntimeDispatchPath = "Assets/Hall/Scripts/MiniGameAppController.Runtime.g.cs";

        [Serializable]
        private sealed class ManifestPayload
        {
            public string gameId;
            public string nameKey;
            public string descriptionKey;
            public bool isPlayable;
            public string statusLabelKey;
            public string category;
        }

        [Serializable]
        private sealed class CatalogOrderPayload
        {
            public List<string> gameIds = new List<string>();
        }

        [Serializable]
        private sealed class TextEntryPayload
        {
            public string key;
            public string value;
        }

        [Serializable]
        private sealed class TextPayload
        {
            public List<TextEntryPayload> entries = new List<TextEntryPayload>();
        }

        private sealed class ManifestFile
        {
            public string ManifestPath;
            public string ScriptFolderPath;
            public string GameRootPath;
            public ManifestPayload Payload;
            public int ExplicitOrderIndex = -1;
        }

        private sealed class RuntimeDispatchEntry
        {
            public string GameId;
            public string RuntimeClassName;
        }

        [MenuItem("Tools/小游戏/刷新大厅接入")]
        public static void RefreshAll()
        {
            var manifests = LoadManifestFiles();
            var errors = new List<string>();
            ApplyCatalogOrder(manifests, errors);
            var textByKey = LoadTextEntries(errors, manifests);

            ValidateManifestEntries(manifests, textByKey, errors);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            }

            WriteCatalogAsset(manifests, textByKey);
            WriteRuntimeDispatchFile(manifests);
            TMPStaticSubsetFontBuilder.BuildDefaultSubsetFont();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MiniGame catalog refreshed: " + manifests.Count + " games.");
        }

        public static void RefreshAllFromCommandLine()
        {
            RefreshAll();
        }

        private static string ProjectRootPath
        {
            get { return Directory.GetParent(Application.dataPath).FullName; }
        }

        private static List<ManifestFile> LoadManifestFiles()
        {
            var result = new List<ManifestFile>();
            var fullGamesRootPath = GetProjectFullPath(GamesRootPath);
            if (!Directory.Exists(fullGamesRootPath))
            {
                return result;
            }

            var manifestPaths = Directory.GetFiles(fullGamesRootPath, "game.manifest.json", SearchOption.AllDirectories);
            Array.Sort(manifestPaths, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < manifestPaths.Length; i++)
            {
                var manifestPath = ToAssetRelativePath(manifestPaths[i]);
                var text = File.ReadAllText(manifestPaths[i], Encoding.UTF8);
                var payload = JsonUtility.FromJson<ManifestPayload>(text);
                result.Add(new ManifestFile
                {
                    ManifestPath = manifestPath,
                    ScriptFolderPath = ToAssetRelativePath(Path.GetDirectoryName(manifestPaths[i])),
                    GameRootPath = ToAssetRelativePath(Directory.GetParent(Path.GetDirectoryName(manifestPaths[i])).FullName),
                    Payload = payload
                });
            }

            return result;
        }

        private static void ApplyCatalogOrder(List<ManifestFile> manifests, List<string> errors)
        {
            var fullOrderPath = GetProjectFullPath(CatalogOrderSourcePath);
            var orderFileExists = File.Exists(fullOrderPath);
            var payload = orderFileExists
                ? JsonUtility.FromJson<CatalogOrderPayload>(File.ReadAllText(fullOrderPath, Encoding.UTF8))
                : new CatalogOrderPayload();
            if (payload == null || payload.gameIds == null)
            {
                errors.Add("小游戏排序配置格式无效: " + CatalogOrderSourcePath);
                manifests.Sort(CompareManifestFiles);
                return;
            }

            var manifestById = new Dictionary<string, ManifestFile>(StringComparer.Ordinal);
            for (var i = 0; i < manifests.Count; i++)
            {
                var manifest = manifests[i];
                if (manifest == null || manifest.Payload == null || string.IsNullOrWhiteSpace(manifest.Payload.gameId))
                {
                    continue;
                }

                var gameId = manifest.Payload.gameId.Trim();
                if (!manifestById.ContainsKey(gameId))
                {
                    manifestById.Add(gameId, manifest);
                }
            }

            var seenGameIds = new HashSet<string>(StringComparer.Ordinal);
            var orderedGameIds = new List<string>();
            var explicitIndex = 0;
            for (var i = 0; i < payload.gameIds.Count; i++)
            {
                var gameId = payload.gameIds[i];
                if (string.IsNullOrWhiteSpace(gameId))
                {
                    continue;
                }

                var trimmedGameId = gameId.Trim();
                if (!seenGameIds.Add(trimmedGameId))
                {
                    errors.Add("小游戏排序配置存在重复 gameId: " + trimmedGameId);
                    continue;
                }

                ManifestFile manifest;
                if (!manifestById.TryGetValue(trimmedGameId, out manifest))
                {
                    errors.Add("小游戏排序配置引用了不存在的 gameId: " + trimmedGameId);
                    continue;
                }

                manifest.ExplicitOrderIndex = explicitIndex;
                orderedGameIds.Add(trimmedGameId);
                explicitIndex++;
            }

            var appendManifests = new List<ManifestFile>();
            for (var i = 0; i < manifests.Count; i++)
            {
                var manifest = manifests[i];
                if (manifest == null || manifest.Payload == null || string.IsNullOrWhiteSpace(manifest.Payload.gameId))
                {
                    continue;
                }

                if (!seenGameIds.Contains(manifest.Payload.gameId.Trim()))
                {
                    appendManifests.Add(manifest);
                }
            }

            appendManifests.Sort(CompareManifestFiles);
            for (var i = 0; i < appendManifests.Count; i++)
            {
                var manifest = appendManifests[i];
                var gameId = manifest.Payload.gameId.Trim();
                manifest.ExplicitOrderIndex = explicitIndex;
                orderedGameIds.Add(gameId);
                seenGameIds.Add(gameId);
                explicitIndex++;
            }

            if (errors.Count == 0 && (!orderFileExists || !IsSameCatalogOrder(payload.gameIds, orderedGameIds)))
            {
                WriteCatalogOrderFile(orderedGameIds);
            }

            manifests.Sort(CompareManifestFiles);
        }

        private static bool IsSameCatalogOrder(List<string> sourceGameIds, List<string> normalizedGameIds)
        {
            if (sourceGameIds == null || sourceGameIds.Count != normalizedGameIds.Count)
            {
                return false;
            }

            for (var i = 0; i < sourceGameIds.Count; i++)
            {
                var sourceGameId = sourceGameIds[i];
                var normalizedGameId = normalizedGameIds[i];
                if (string.IsNullOrWhiteSpace(sourceGameId) || sourceGameId.Trim() != normalizedGameId)
                {
                    return false;
                }
            }

            return true;
        }

        private static void WriteCatalogOrderFile(List<string> gameIds)
        {
            EnsureParentDirectory(CatalogOrderSourcePath);

            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"gameIds\": [");
            for (var i = 0; i < gameIds.Count; i++)
            {
                builder.Append("    \"");
                builder.Append(EscapeJsonString(gameIds[i]));
                builder.Append("\"");
                if (i < gameIds.Count - 1)
                {
                    builder.Append(",");
                }

                builder.AppendLine();
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");

            File.WriteAllText(GetProjectFullPath(CatalogOrderSourcePath), builder.ToString(), new UTF8Encoding(true));
        }

        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static int CompareManifestFiles(ManifestFile left, ManifestFile right)
        {
            var leftExplicitOrder = left != null ? left.ExplicitOrderIndex : -1;
            var rightExplicitOrder = right != null ? right.ExplicitOrderIndex : -1;
            if (leftExplicitOrder >= 0 || rightExplicitOrder >= 0)
            {
                if (leftExplicitOrder < 0)
                {
                    return 1;
                }

                if (rightExplicitOrder < 0)
                {
                    return -1;
                }

                var explicitOrderCompare = leftExplicitOrder.CompareTo(rightExplicitOrder);
                if (explicitOrderCompare != 0)
                {
                    return explicitOrderCompare;
                }
            }

            var leftId = left?.Payload != null ? left.Payload.gameId : string.Empty;
            var rightId = right?.Payload != null ? right.Payload.gameId : string.Empty;
            return string.Compare(leftId, rightId, StringComparison.Ordinal);
        }

        private static Dictionary<string, string> LoadTextEntries(List<string> errors, List<ManifestFile> manifests)
        {
            var textByKey = new Dictionary<string, string>(StringComparer.Ordinal);
            LoadTextFile(SharedTextSourcePath, textByKey, errors);

            for (var i = 0; i < manifests.Count; i++)
            {
                var manifest = manifests[i];
                var textPaths = ResolveGameTextSourcePaths(manifest);
                if (textPaths.Count == 0)
                {
                    errors.Add("缺少文案源文件: " + manifest.GameRootPath);
                    continue;
                }

                for (var pathIndex = 0; pathIndex < textPaths.Count; pathIndex++)
                {
                    LoadTextFile(textPaths[pathIndex], textByKey, errors);
                }
            }

            return textByKey;
        }

        private static List<string> ResolveGameTextSourcePaths(ManifestFile manifest)
        {
            var results = new List<string>();
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.GameRootPath))
            {
                return results;
            }

            var fullFolderPath = GetProjectFullPath(manifest.GameRootPath);
            if (!Directory.Exists(fullFolderPath))
            {
                return results;
            }

            var files = Directory.GetFiles(fullFolderPath, "*.ui_texts.zh-CN.json", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < files.Length; i++)
            {
                results.Add(ToAssetRelativePath(files[i]));
            }

            return results;
        }

        private static void LoadTextFile(string assetPath, Dictionary<string, string> textByKey, List<string> errors)
        {
            var fullPath = GetProjectFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                errors.Add("缺少文案源文件: " + assetPath);
                return;
            }

            var payload = JsonUtility.FromJson<TextPayload>(File.ReadAllText(fullPath, Encoding.UTF8));
            if (payload == null || payload.entries == null)
            {
                errors.Add("文案源文件格式无效: " + assetPath);
                return;
            }

            for (var i = 0; i < payload.entries.Count; i++)
            {
                var entry = payload.entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                {
                    continue;
                }

                var key = entry.key.Trim();
                var value = entry.value ?? string.Empty;
                string existingValue;
                if (textByKey.TryGetValue(key, out existingValue))
                {
                    if (!string.Equals(existingValue, value, StringComparison.Ordinal))
                    {
                        errors.Add("文案 key 冲突: " + key + " in " + assetPath);
                    }

                    continue;
                }

                textByKey[key] = value;
            }
        }

        private static void ValidateManifestEntries(List<ManifestFile> manifests, Dictionary<string, string> textByKey, List<string> errors)
        {
            var gameIdSet = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < manifests.Count; i++)
            {
                var manifest = manifests[i];
                if (manifest.Payload == null)
                {
                    errors.Add("manifest 格式无效: " + manifest.ManifestPath);
                    continue;
                }

                var payload = manifest.Payload;
                if (string.IsNullOrWhiteSpace(payload.gameId))
                {
                    errors.Add("manifest 缺少 gameId: " + manifest.ManifestPath);
                }
                else if (!gameIdSet.Add(payload.gameId.Trim()))
                {
                    errors.Add("gameId 重复: " + payload.gameId.Trim());
                }

                ValidateTextKey(payload.nameKey, "nameKey", manifest.ManifestPath, textByKey, errors);
                ValidateTextKey(payload.descriptionKey, "descriptionKey", manifest.ManifestPath, textByKey, errors);
                ValidateTextKey(payload.statusLabelKey, "statusLabelKey", manifest.ManifestPath, textByKey, errors);
                ValidateCategory(payload.category, manifest.ManifestPath, errors);

                if (payload.isPlayable)
                {
                    string runtimeClassName;
                    if (!TryResolveRuntimeClassName(manifest, out runtimeClassName))
                    {
                        errors.Add("未找到对应运行时类: " + manifest.ManifestPath);
                    }
                }

            }
        }

        private static void ValidateTextKey(string key, string fieldName, string manifestPath, Dictionary<string, string> textByKey, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add("manifest 缺少 " + fieldName + ": " + manifestPath);
                return;
            }

            if (!textByKey.ContainsKey(key.Trim()))
            {
                errors.Add("文案 key 未定义: " + key.Trim() + " in " + manifestPath);
            }
        }

        private static void ValidateOptionalTextKey(string key, string fieldName, string manifestPath, Dictionary<string, string> textByKey, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (!textByKey.ContainsKey(key.Trim()))
            {
                errors.Add("文案 key 未定义: " + key.Trim() + " in " + manifestPath + " (" + fieldName + ")");
            }
        }

        private static void ValidateCategory(string category, string manifestPath, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                errors.Add("manifest 缺少 category: " + manifestPath);
                return;
            }

            var trimmedCategory = category.Trim();
            if (trimmedCategory != "eliminate"
                && trimmedCategory != "merge"
                && trimmedCategory != "number"
                && trimmedCategory != "puzzle"
                && trimmedCategory != "action"
                && trimmedCategory != "simulation")
            {
                errors.Add("manifest category 无效: " + trimmedCategory + " in " + manifestPath);
            }
        }

        private static void WriteCatalogAsset(List<ManifestFile> manifests, Dictionary<string, string> textByKey)
        {
            var config = AssetDatabase.LoadAssetAtPath<MiniGameCatalogConfig>(OutputCatalogAssetPath);
            if (config == null)
            {
                EnsureParentDirectory(OutputCatalogAssetPath);
                config = ScriptableObject.CreateInstance<MiniGameCatalogConfig>();
                AssetDatabase.CreateAsset(config, OutputCatalogAssetPath);
            }

            config.Entries.Clear();
            for (var i = 0; i < manifests.Count; i++)
            {
                var payload = manifests[i].Payload;
                config.Entries.Add(new MiniGameCatalogConfig.Entry
                {
                    Id = payload.gameId.Trim(),
                    NameKey = payload.nameKey.Trim(),
                    Name = textByKey[payload.nameKey.Trim()],
                    DescriptionKey = payload.descriptionKey.Trim(),
                    Description = textByKey[payload.descriptionKey.Trim()],
                    IsPlayable = payload.isPlayable,
                    StatusLabelKey = payload.statusLabelKey.Trim(),
                    StatusLabel = textByKey[payload.statusLabelKey.Trim()],
                    Category = payload.category.Trim()
                });
            }

            EditorUtility.SetDirty(config);
        }

        private static void WriteRuntimeDispatchFile(List<ManifestFile> manifests)
        {
            EnsureParentDirectory(OutputRuntimeDispatchPath);

            var entries = new List<RuntimeDispatchEntry>();
            for (var i = 0; i < manifests.Count; i++)
            {
                var manifest = manifests[i];
                if (manifest == null || manifest.Payload == null || !manifest.Payload.isPlayable)
                {
                    continue;
                }

                string runtimeClassName;
                if (!TryResolveRuntimeClassName(manifest, out runtimeClassName))
                {
                    continue;
                }

                entries.Add(new RuntimeDispatchEntry
                {
                    GameId = manifest.Payload.gameId.Trim(),
                    RuntimeClassName = runtimeClassName
                });
            }

            var builder = new StringBuilder();
            builder.AppendLine("using UnityEngine;");
            builder.AppendLine();
            builder.AppendLine("namespace HuanYouYu.MiniGameHall");
            builder.AppendLine("{");
            builder.AppendLine("    public sealed partial class MiniGameAppController");
            builder.AppendLine("    {");
            builder.AppendLine("        private MiniGameBase CreateGameRuntime(string gameId)");
            builder.AppendLine("        {");
            builder.AppendLine("            if (string.IsNullOrWhiteSpace(gameId))");
            builder.AppendLine("            {");
            builder.AppendLine("                return null;");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            switch (gameId)");
            builder.AppendLine("            {");

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                builder.AppendLine("                case " + entry.RuntimeClassName + ".GameIdConstant:");
                builder.AppendLine("                    return new " + entry.RuntimeClassName + "(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);");
            }

            builder.AppendLine();
            builder.AppendLine("                default:");
            builder.AppendLine("                    Debug.LogWarning(\"未注册小游戏运行时: \" + gameId);");
            builder.AppendLine("                    return null;");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");

            File.WriteAllText(GetProjectFullPath(OutputRuntimeDispatchPath), builder.ToString(), new UTF8Encoding(true));
            AssetDatabase.ImportAsset(OutputRuntimeDispatchPath, ImportAssetOptions.ForceUpdate);
        }

        private static bool TryResolveRuntimeClassName(ManifestFile manifest, out string runtimeClassName)
        {
            runtimeClassName = null;

            if (manifest == null || manifest.Payload == null || string.IsNullOrWhiteSpace(manifest.Payload.gameId) || string.IsNullOrWhiteSpace(manifest.ScriptFolderPath))
            {
                return false;
            }

            var fullFolderPath = GetProjectFullPath(manifest.ScriptFolderPath);
            if (!Directory.Exists(fullFolderPath))
            {
                return false;
            }

            var gameIdPattern = Regex.Escape(manifest.Payload.gameId.Trim());
            var csFiles = Directory.GetFiles(fullFolderPath, "*.cs", SearchOption.AllDirectories);
            for (var i = 0; i < csFiles.Length; i++)
            {
                var file = csFiles[i];
                var text = File.ReadAllText(file, Encoding.UTF8);
                if (!Regex.IsMatch(text, "GameIdConstant\\s*=\\s*\"" + gameIdPattern + "\""))
                {
                    continue;
                }

                var match = Regex.Match(text, @"public\s+(?:(?:sealed|partial)\s+)*class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*MiniGameBase");
                if (!match.Success)
                {
                    return false;
                }

                runtimeClassName = match.Groups["name"].Value;
                return true;
            }

            return false;
        }

        private static void EnsureParentDirectory(string assetPath)
        {
            var fullPath = GetProjectFullPath(assetPath);
            var parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }
        }

        private static string GetProjectFullPath(string assetOrProjectRelativePath)
        {
            return Path.GetFullPath(Path.Combine(ProjectRootPath, assetOrProjectRelativePath));
        }

        private static string ToAssetRelativePath(string fullPath)
        {
            var normalizedFullPath = Path.GetFullPath(fullPath).Replace("\\", "/");
            var normalizedProjectRoot = Path.GetFullPath(ProjectRootPath).Replace("\\", "/").TrimEnd('/');
            if (normalizedFullPath.StartsWith(normalizedProjectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedFullPath.Substring(normalizedProjectRoot.Length + 1);
            }

            return normalizedFullPath;
        }
    }
}
