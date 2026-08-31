using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 大厅进度的本地持久化存储，基于 PlayerPrefs 读写 JSON。
    /// </summary>
    public sealed class MiniGameSaveStore
    {
        public const string PlayerPrefsKey = "huanyouyu.mini_game_hall.progress";

        public sealed class LoadedState
        {
            public Dictionary<string, MiniGameProgressData> ProgressLookup = new Dictionary<string, MiniGameProgressData>();
            public List<string> FavoriteGameIds = new List<string>();
            public int HallRewardChestCount;
            public bool HasPersistedState;
        }

        [Serializable]
        private sealed class MiniGameSaveData
        {
            public List<MiniGameProgressData> Entries = new List<MiniGameProgressData>();
            public List<string> FavoriteGameIds = new List<string>();
            public int HallRewardChestCount;
        }

        /// <summary>
        /// 读取存档并按当前游戏定义补齐缺失条目。
        /// </summary>
        public LoadedState Load(IEnumerable<MiniGameDefinition> definitions)
        {
            var loadedLookup = new Dictionary<string, MiniGameProgressData>();
            var favoriteGameIds = new List<string>();
            var favoriteIdLookup = new HashSet<string>();
            MiniGameSaveData saveData = null;
            var hasPersistedState = PlayerPrefs.HasKey(PlayerPrefsKey);
            if (hasPersistedState)
            {
                var rawJson = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(rawJson))
                {
                    saveData = JsonUtility.FromJson<MiniGameSaveData>(rawJson);
                    if (saveData != null && saveData.Entries != null)
                    {
                        for (var i = 0; i < saveData.Entries.Count; i++)
                        {
                            var entry = saveData.Entries[i];
                            if (entry == null || string.IsNullOrWhiteSpace(entry.GameId))
                            {
                                continue;
                            }

                            NormalizeLevelProgress(entry);
                            loadedLookup[entry.GameId] = entry;
                        }

                        if (saveData.FavoriteGameIds != null)
                        {
                            for (var i = 0; i < saveData.FavoriteGameIds.Count; i++)
                            {
                                var favoriteGameId = saveData.FavoriteGameIds[i];
                                favoriteGameId = favoriteGameId != null ? favoriteGameId.Trim() : string.Empty;
                                if (string.IsNullOrWhiteSpace(favoriteGameId))
                                {
                                    continue;
                                }

                                if (favoriteIdLookup.Add(favoriteGameId))
                                {
                                    favoriteGameIds.Add(favoriteGameId);
                                }
                            }
                        }
                    }
                }
            }

            var result = new LoadedState();
            foreach (var definition in definitions)
            {
                MiniGameProgressData progress;
                if (!loadedLookup.TryGetValue(definition.Id, out progress))
                {
                    progress = CreateEmpty(definition.Id);
                }
                else
                {
                    NormalizeLevelProgress(progress);
                }

                result.ProgressLookup[definition.Id] = progress;
            }

            result.FavoriteGameIds = favoriteGameIds;
            result.HallRewardChestCount = saveData != null ? Mathf.Max(0, saveData.HallRewardChestCount) : 0;
            result.HasPersistedState = hasPersistedState;
            return result;
        }

        /// <summary>
        /// 将当前进度字典序列化后写入 PlayerPrefs。
        /// </summary>
        public void Save(
            Dictionary<string, MiniGameProgressData> progressLookup,
            IList<string> favoriteGameIds,
            int hallRewardChestCount)
        {
            var saveData = new MiniGameSaveData
            {
                HallRewardChestCount = Mathf.Max(0, hallRewardChestCount)
            };
            foreach (var pair in progressLookup)
            {
                saveData.Entries.Add(new MiniGameProgressData
                {
                    GameId = pair.Value.GameId,
                    PlayCount = pair.Value.PlayCount,
                    BestScore = pair.Value.BestScore,
                    TotalChestCount = pair.Value.TotalChestCount,
                    TotalCoinCount = pair.Value.TotalCoinCount,
                    CurrentLevelIndex = Mathf.Max(0, pair.Value.CurrentLevelIndex),
                    UnlockedLevelCount = Mathf.Max(1, pair.Value.UnlockedLevelCount),
                    TutorialSeenVersion = Mathf.Max(0, pair.Value.TutorialSeenVersion),
                    LevelProgress = CloneLevelProgress(pair.Value.LevelProgress)
                });
            }

            if (favoriteGameIds != null)
            {
                foreach (var favoriteGameId in favoriteGameIds)
                {
                    if (!string.IsNullOrWhiteSpace(favoriteGameId))
                    {
                        saveData.FavoriteGameIds.Add(favoriteGameId.Trim());
                    }
                }
            }

            var rawJson = JsonUtility.ToJson(saveData);
            PlayerPrefs.SetString(PlayerPrefsKey, rawJson);
            PlayerPrefs.Save();
        }

        public static void ClearPersistedState()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 创建指定游戏 ID 的空白进度数据。
        /// </summary>
        public static MiniGameProgressData CreateEmpty(string gameId)
        {
            return new MiniGameProgressData
            {
                GameId = gameId,
                PlayCount = 0,
                BestScore = 0,
                TotalChestCount = 0,
                TotalCoinCount = 0,
                CurrentLevelIndex = 0,
                UnlockedLevelCount = 1,
                TutorialSeenVersion = 0,
                LevelProgress = new List<MiniGameLevelProgressData>()
            };
        }

        public static void NormalizeLevelProgress(MiniGameProgressData progress)
        {
            if (progress == null)
            {
                return;
            }

            progress.CurrentLevelIndex = Mathf.Max(0, progress.CurrentLevelIndex);
            progress.UnlockedLevelCount = Mathf.Max(1, progress.UnlockedLevelCount);
            if (progress.CurrentLevelIndex >= progress.UnlockedLevelCount)
            {
                progress.CurrentLevelIndex = progress.UnlockedLevelCount - 1;
            }
            NormalizeCompletedLevels(progress);
        }

        private static List<MiniGameLevelProgressData> CloneLevelProgress(
            IList<MiniGameLevelProgressData> source)
        {
            var result = new List<MiniGameLevelProgressData>();
            if (source == null)
            {
                return result;
            }
            for (var index = 0; index < source.Count; index++)
            {
                var entry = source[index];
                if (entry == null)
                {
                    continue;
                }
                result.Add(new MiniGameLevelProgressData
                {
                    LevelId = entry.LevelId,
                    IsCompleted = entry.IsCompleted,
                    BestScore = entry.BestScore
                });
            }
            return result;
        }

        private static void NormalizeCompletedLevels(MiniGameProgressData progress)
        {
            var normalized = new List<MiniGameLevelProgressData>();
            var lookup = new Dictionary<int, MiniGameLevelProgressData>();
            if (progress.LevelProgress != null)
            {
                for (var index = 0; index < progress.LevelProgress.Count; index++)
                {
                    var entry = progress.LevelProgress[index];
                    if (entry == null)
                    {
                        continue;
                    }

                    MiniGameLevelProgressData existing;
                    if (!lookup.TryGetValue(entry.LevelId, out existing))
                    {
                        existing = new MiniGameLevelProgressData
                        {
                            LevelId = entry.LevelId,
                            IsCompleted = entry.IsCompleted,
                            BestScore = entry.BestScore
                        };
                        lookup[entry.LevelId] = existing;
                        normalized.Add(existing);
                    }
                    else
                    {
                        if (entry.IsCompleted && (!existing.IsCompleted || entry.BestScore > existing.BestScore))
                        {
                            existing.BestScore = entry.BestScore;
                        }
                        existing.IsCompleted |= entry.IsCompleted;
                    }
                }
            }
            progress.LevelProgress = normalized;
        }
    }
}
