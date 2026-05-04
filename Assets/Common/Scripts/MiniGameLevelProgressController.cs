using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    internal interface IMiniGameLevelProgressStore
    {
        MiniGameProgressData GetProgress(string gameId);

        void SetLevelProgress(string gameId, int currentLevelIndex, int unlockedLevelCount);
    }

    internal sealed class MiniGameLevelProgressController
    {
        private readonly IMiniGameLevelProgressStore progressStore;
        private readonly string gameId;
        private readonly int levelCount;

        public MiniGameLevelProgressController(MonoBehaviour hostBehaviour, string gameId, int levelCount)
        {
            progressStore = hostBehaviour as IMiniGameLevelProgressStore;
            this.gameId = gameId;
            this.levelCount = Mathf.Max(1, levelCount);

            var progress = progressStore != null
                ? progressStore.GetProgress(gameId)
                : CreateEmptyProgress(gameId);
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
    }
}
