using System;
using System.Collections.Generic;
using System.Reflection;

namespace HuanYouYu.Editor.MiniGameHall
{
    internal static class MiniGameLevelCatalog
    {
        public static IReadOnlyList<MiniGameLevelCatalogEntry> GetEntries()
        {
            var entries = new List<MiniGameLevelCatalogEntry>();
            var seenGameIds = new HashSet<string>(StringComparer.Ordinal);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (var assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                var assembly = assemblies[assemblyIndex];
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }

                if (types == null)
                {
                    continue;
                }

                for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    var type = types[typeIndex];
                    if (type == null || type.IsAbstract || string.IsNullOrWhiteSpace(type.Namespace) || !type.Namespace.StartsWith("HuanYouYu.", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!typeof(HuanYouYu.MiniGameHall.MiniGameBase).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    string gameId;
                    if (!TryGetGameId(type, out gameId) || !seenGameIds.Add(gameId))
                    {
                        continue;
                    }

                    int levelCount;
                    if (!TryGetLevelCount(type, out levelCount) || levelCount < 1)
                    {
                        continue;
                    }

                    entries.Add(new MiniGameLevelCatalogEntry(gameId, levelCount));
                }
            }

            entries.Sort(CompareEntries);
            return entries;
        }

        private static int CompareEntries(MiniGameLevelCatalogEntry left, MiniGameLevelCatalogEntry right)
        {
            return string.Compare(left.GameId, right.GameId, StringComparison.Ordinal);
        }

        private static bool TryGetGameId(Type type, out string gameId)
        {
            gameId = string.Empty;

            var field = type.GetField("GameIdConstant", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (field != null && field.FieldType == typeof(string))
            {
                gameId = field.GetValue(null) as string;
                gameId = gameId != null ? gameId.Trim() : string.Empty;
                if (!string.IsNullOrWhiteSpace(gameId))
                {
                    return true;
                }
            }

            var property = type.GetProperty("GameIdConstant", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (property != null && property.PropertyType == typeof(string))
            {
                gameId = property.GetValue(null, null) as string;
                gameId = gameId != null ? gameId.Trim() : string.Empty;
                return !string.IsNullOrWhiteSpace(gameId);
            }

            return false;
        }

        private static bool TryGetLevelCount(Type type, out int levelCount)
        {
            levelCount = 0;

            var field = type.GetField("LevelCount", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (field != null && field.FieldType == typeof(int))
            {
                levelCount = (int)field.GetValue(null);
                return levelCount > 0;
            }

            var property = type.GetProperty("LevelCount", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (property != null && property.PropertyType == typeof(int))
            {
                levelCount = (int)property.GetValue(null, null);
                return levelCount > 0;
            }

            return false;
        }
    }

    internal readonly struct MiniGameLevelCatalogEntry
    {
        public MiniGameLevelCatalogEntry(string gameId, int levelCount)
        {
            GameId = gameId;
            LevelCount = levelCount;
        }

        public string GameId { get; }

        public int LevelCount { get; }
    }
}
