using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    internal interface IMiniGameLevelProgressStore
    {
        MiniGameProgressData GetProgress(string gameId);

        void SetLevelProgress(string gameId, int currentLevelIndex, int unlockedLevelCount);

        void SetLevelCompletion(string gameId, int levelId, int score);
    }

    internal sealed class MiniGameLevelProgressController
    {
        private readonly IMiniGameLevelProgressStore progressStore;
        private readonly string gameId;
        private readonly int levelCount;
        private readonly int[] levelIds;
        private readonly Dictionary<int, MiniGameLevelProgressData> levelProgressLookup =
            new Dictionary<int, MiniGameLevelProgressData>();

        public MiniGameLevelProgressController(MonoBehaviour hostBehaviour, string gameId, int levelCount)
            : this(hostBehaviour, gameId, CreateSequentialLevelIds(levelCount))
        {
        }

        public MiniGameLevelProgressController(MonoBehaviour hostBehaviour, string gameId, IList<int> levelIds)
        {
            progressStore = hostBehaviour as IMiniGameLevelProgressStore;
            this.gameId = gameId;
            this.levelIds = CopyAndValidateLevelIds(levelIds);
            levelCount = this.levelIds.Length;

            var progress = progressStore != null
                ? progressStore.GetProgress(gameId)
                : CreateEmptyProgress(gameId);
            LoadLevelProgress(progress);
            UnlockedLevelCount = Mathf.Clamp(progress.UnlockedLevelCount, 1, this.levelCount);
            CurrentLevelIndex = Mathf.Clamp(progress.CurrentLevelIndex, 0, UnlockedLevelCount - 1);
            Save();
        }

        public int CurrentLevelIndex { get; private set; }

        public int UnlockedLevelCount { get; private set; }

        public int LevelCount
        {
            get { return levelCount; }
        }

        public bool CanSelect(int index)
        {
            return index >= 0 && index < levelCount && index < UnlockedLevelCount;
        }

        public void CompleteCurrentLevel(int score)
        {
            var levelId = levelIds[CurrentLevelIndex];
            MiniGameLevelProgressData levelProgress;
            if (!levelProgressLookup.TryGetValue(levelId, out levelProgress))
            {
                levelProgress = new MiniGameLevelProgressData
                {
                    LevelId = levelId,
                    IsCompleted = true,
                    BestScore = score
                };
                levelProgressLookup[levelId] = levelProgress;
            }
            else
            {
                if (!levelProgress.IsCompleted || score > levelProgress.BestScore)
                {
                    levelProgress.BestScore = score;
                }
                levelProgress.IsCompleted = true;
            }

            if (progressStore != null)
            {
                progressStore.SetLevelCompletion(gameId, levelId, score);
            }
            UnlockNext();
        }

        public bool IsLevelCompleted(int levelId)
        {
            MiniGameLevelProgressData levelProgress;
            return levelProgressLookup.TryGetValue(levelId, out levelProgress)
                && levelProgress.IsCompleted;
        }

        public int GetBestScore(int levelId)
        {
            MiniGameLevelProgressData levelProgress;
            return levelProgressLookup.TryGetValue(levelId, out levelProgress)
                && levelProgress.IsCompleted
                ? levelProgress.BestScore
                : 0;
        }

        public bool AreAllLevelsCompleted
        {
            get
            {
                for (var index = 0; index < levelIds.Length; index++)
                {
                    if (!IsLevelCompleted(levelIds[index]))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public bool Select(int index)
        {
            if (!CanSelect(index))
            {
                return false;
            }

            CurrentLevelIndex = index;
            Save();
            return true;
        }

        public void UnlockNext()
        {
            var targetUnlockedCount = Mathf.Min(levelCount, CurrentLevelIndex + 2);
            if (targetUnlockedCount > UnlockedLevelCount)
            {
                UnlockedLevelCount = targetUnlockedCount;
                Save();
            }
        }

        public bool CanGoNext()
        {
            return CurrentLevelIndex + 1 < levelCount && CurrentLevelIndex + 1 < UnlockedLevelCount;
        }

        public bool GoNext()
        {
            if (!CanGoNext())
            {
                return false;
            }

            CurrentLevelIndex += 1;
            Save();
            return true;
        }

        public bool SaveNextAsCurrent()
        {
            return GoNext();
        }

        private void Save()
        {
            if (progressStore == null)
            {
                return;
            }

            progressStore.SetLevelProgress(gameId, CurrentLevelIndex, UnlockedLevelCount);
        }

        private static MiniGameProgressData CreateEmptyProgress(string gameId)
        {
            return new MiniGameProgressData
            {
                GameId = gameId,
                PlayCount = 0,
                BestScore = 0,
                TotalChestCount = 0,
                TotalCoinCount = 0,
                CurrentLevelIndex = 0,
                UnlockedLevelCount = 1
            };
        }

        private void LoadLevelProgress(MiniGameProgressData progress)
        {
            if (progress == null || progress.LevelProgress == null)
            {
                return;
            }
            for (var index = 0; index < progress.LevelProgress.Count; index++)
            {
                var entry = progress.LevelProgress[index];
                if (entry != null && entry.IsCompleted)
                {
                    levelProgressLookup[entry.LevelId] = entry;
                }
            }
        }

        private static int[] CreateSequentialLevelIds(int levelCount)
        {
            var count = Mathf.Max(1, levelCount);
            var ids = new int[count];
            for (var index = 0; index < count; index++)
            {
                ids[index] = index;
            }
            return ids;
        }

        private static int[] CopyAndValidateLevelIds(IList<int> source)
        {
            if (source == null || source.Count == 0)
            {
                return new[] { 0 };
            }

            var ids = new int[source.Count];
            var uniqueIds = new HashSet<int>();
            for (var index = 0; index < source.Count; index++)
            {
                var levelId = source[index];
                if (!uniqueIds.Add(levelId))
                {
                    throw new ArgumentException("Level IDs must be unique.", nameof(source));
                }
                ids[index] = levelId;
            }
            return ids;
        }
    }
}
