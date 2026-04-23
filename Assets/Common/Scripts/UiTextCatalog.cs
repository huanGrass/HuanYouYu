using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    public static class UiTextCatalog
    {
        private const string ResourceFolderPath = "Text";
        private const string SharedCatalogName = "ui_texts.shared.zh-CN";
        private const string MissingText = "?";
        private static readonly Dictionary<string, string> TextByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly HashSet<string> ReportedMissingKeys = new HashSet<string>(StringComparer.Ordinal);
        private static bool initialized;

        [Serializable]
        private sealed class UiTextEntry
        {
            public string key;
            public string value;
        }

        [Serializable]
        private sealed class UiTextPayload
        {
            public List<UiTextEntry> entries = new List<UiTextEntry>();
        }

        /// <summary>
        /// 按 key 获取文案，不存在时返回 "?"。
        /// </summary>
        public static string Get(string key)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(key))
            {
                ReportMissingKey("<empty>");
                return MissingText;
            }

            string value;
            if (TextByKey.TryGetValue(key.Trim(), out value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }

            ReportMissingKey(key.Trim());
            return MissingText;
        }

        public static string GetOrFallback(string key, string fallback)
        {
            var value = Get(key);
            if (!string.IsNullOrEmpty(value) && value != MissingText)
            {
                return value;
            }

            return string.IsNullOrEmpty(fallback) ? MissingText : fallback;
        }

        /// <summary>
        /// 按 key 获取模板文案并格式化，不存在时返回 "?"。
        /// </summary>
        public static string Format(string key, params object[] args)
        {
            var format = Get(key);
            if (args == null || args.Length == 0)
            {
                return format;
            }

            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                ReportMissingKey("format-error:" + key);
                return MissingText;
            }
        }

        private static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            TextByKey.Clear();

            var assets = Resources.LoadAll<TextAsset>(ResourceFolderPath);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("未找到 UI 文案配置: Resources/" + ResourceFolderPath);
                return;
            }

            Array.Sort(assets, CompareTextAssets);
            for (var i = 0; i < assets.Length; i++)
            {
                LoadTextAsset(assets[i]);
            }
        }

        private static void ReportMissingKey(string key)
        {
            if (ReportedMissingKeys.Contains(key))
            {
                return;
            }

            ReportedMissingKeys.Add(key);
            Debug.LogWarning("UI 文案缺失 key: " + key);
        }

        private static int CompareTextAssets(TextAsset left, TextAsset right)
        {
            var leftName = left != null ? left.name : string.Empty;
            var rightName = right != null ? right.name : string.Empty;
            var leftPriority = string.Equals(leftName, SharedCatalogName, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            var rightPriority = string.Equals(rightName, SharedCatalogName, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            var priorityCompare = leftPriority.CompareTo(rightPriority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
        }

        private static void LoadTextAsset(TextAsset asset)
        {
            if (asset == null || string.IsNullOrWhiteSpace(asset.text) || !IsCatalogAsset(asset.name))
            {
                return;
            }

            UiTextPayload payload;
            try
            {
                payload = JsonUtility.FromJson<UiTextPayload>(asset.text);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("解析 UI 文案配置失败: " + asset.name + " - " + exception.Message);
                return;
            }

            if (payload == null || payload.entries == null)
            {
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
                if (TextByKey.TryGetValue(key, out existingValue) &&
                    !string.Equals(existingValue, value, StringComparison.Ordinal))
                {
                    Debug.LogWarning("UI 文案 key 冲突: " + key + " in " + asset.name);
                }

                TextByKey[key] = value;
            }
        }

        private static bool IsCatalogAsset(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return false;
            }

            return string.Equals(assetName, SharedCatalogName, StringComparison.OrdinalIgnoreCase) ||
                assetName.EndsWith(".ui_texts.zh-CN", StringComparison.OrdinalIgnoreCase);
        }
    }
}
