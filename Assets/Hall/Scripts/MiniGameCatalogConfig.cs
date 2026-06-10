using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    [CreateAssetMenu(fileName = "MiniGameCatalogConfig", menuName = "HuanYouYu/Hall/MiniGame Catalog Config")]
    /// <summary>
    /// 小游戏目录统一配置表，供大厅读取基础展示与状态信息。
    /// </summary>
    public sealed class MiniGameCatalogConfig : ScriptableObject
    {
        [SerializeField]
        private List<Entry> entries = new List<Entry>();

        public List<Entry> Entries => entries;

        [Serializable]
        public sealed class Entry
        {
            public string Id;
            public string NameKey;
            public string Name;
            public string DescriptionKey;
            [TextArea(2, 4)]
            public string Description;
            public bool IsPlayable;
            public string StatusLabelKey;
            public string StatusLabel;
            public string Category;
        }
    }

    public static class MiniGameCatalog
    {
        private const string ConfigResourcePath = "MiniGameCatalogConfig";

        private static readonly List<MiniGameDefinition> Definitions = new List<MiniGameDefinition>();
        private static bool initialized;

        public static IReadOnlyList<MiniGameDefinition> GetDefinitions()
        {
            EnsureInitialized();
            return Definitions;
        }

        public static MiniGameDefinition GetDefinition(string gameId)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(gameId))
            {
                return null;
            }

            var trimmedGameId = gameId.Trim();
            for (var i = 0; i < Definitions.Count; i++)
            {
                var definition = Definitions[i];
                if (definition != null && string.Equals(definition.Id, trimmedGameId, StringComparison.Ordinal))
                {
                    return definition;
                }
            }

            return null;
        }

        private static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            Definitions.Clear();

            var config = Resources.Load<MiniGameCatalogConfig>(ConfigResourcePath);
            if (config == null || config.Entries == null || config.Entries.Count == 0)
            {
                Debug.LogWarning("未找到小游戏目录配置: Resources/" + ConfigResourcePath);
                return;
            }

            for (var i = 0; i < config.Entries.Count; i++)
            {
                var entry = config.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                {
                    continue;
                }

                Definitions.Add(new MiniGameDefinition
                {
                    Id = entry.Id.Trim(),
                    Name = UiTextCatalog.Get(entry.NameKey),
                    Description = UiTextCatalog.Get(entry.DescriptionKey),
                    IsPlayable = entry.IsPlayable,
                    StatusLabel = UiTextCatalog.Get(entry.StatusLabelKey),
                    Category = string.IsNullOrWhiteSpace(entry.Category) ? string.Empty : entry.Category.Trim()
                });
            }
        }
    }
}
