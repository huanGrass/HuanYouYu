using System;

namespace HuanYouYu.MiniGameHall
{
    [Serializable]
    public sealed class MiniGameProgressData
    {
        public string GameId;
        public int PlayCount;
        public int BestScore;
        public int TotalChestCount;
        public int TotalCoinCount;
        public int CurrentLevelIndex;
        public int UnlockedLevelCount;
        public int TutorialSeenVersion;
    }

    public sealed class MiniGameSettlement
    {
        public int Score;
        public int ChestCount;
        public int CoinCount;
        public string Summary;
    }

    internal interface IMiniGameRewardSink
    {
        void GrantSettlementReward(string gameId, MiniGameSettlement settlement);
    }

    public interface IMiniGameTutorialStore
    {
        int GetGameTutorialSeenVersion(string gameId);

        void SetGameTutorialSeenVersion(string gameId, int version);
    }
}
