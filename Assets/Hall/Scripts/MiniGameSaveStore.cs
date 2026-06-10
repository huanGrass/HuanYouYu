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
            public bool HasPersistedState;
        }

        [Serializable]
        private sealed class MiniGameSaveData
        {
            public List<MiniGameProgressData> Entries = new List<MiniGameProgressData>();
            public List<string> FavoriteGameIds = new List<string>();
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
            result.HasPersistedState = hasPersistedState;
            return result;
        }

        /// <summary>
        /// 将当前进度字典序列化后写入 PlayerPrefs。
        /// </summary>
        public void Save(Dictionary<string, MiniGameProgressData> progressLookup, IList<string> favoriteGameIds)
        {
            var saveData = new MiniGameSaveData();
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
                    TutorialSeenVersion = Mathf.Max(0, pair.Value.TutorialSeenVersion)
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
                TutorialSeenVersion = 0
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
        }
    }
}
