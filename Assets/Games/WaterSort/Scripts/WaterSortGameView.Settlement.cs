using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    public sealed partial class WaterSortGameView
    {
        private void ShowWinSettlement(MiniGameSettlement settlement)
        {
            if (settlement == null)
            {
                return;
            }

            CloseLevelSelectView();
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "WaterSortSettlementPanel",
                    Title = UiTextCatalog.Get("popup.settlement.title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("water_sort.settlement.steps"), moveCount + UiTextCatalog.Get("water_sort.settlement.step_unit")),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("water_sort.settlement.rating"), ResolveSettlementRating(moveCount)),
                    RewardLabel = UiTextCatalog.Get("water_sort.settlement.reward"),
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.NextLevel,
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                delegate { LoadNextLevel(settlement); },
                delegate
                {
                    SaveNextLevelForReturn();
                    CompleteGame?.Invoke(settlement);
                },
                false);
        }

        private void ShowLevelSelectView()
        {
            Shell.ClosePopup();
            CloseRewardSettlementPanel();
            CloseLevelSelectView();
            EnsureLevelProgress();
            levelSelectView = MiniGameLevelSelectView.Create(
                Shell.PopupHost,
                fontAsset,
                LevelDefinitions.Length,
                currentLevelIndex,
                unlockedLevelCount,
                "WaterSortLevelSelectPanel",
                "WaterSortLevelButton_",
                SelectLevel,
                CloseLevelSelectView);
        }

        private void CloseLevelSelectView()
        {
            if (levelSelectView != null)
            {
                levelSelectView.Dispose();
                levelSelectView = null;
            }
        }

        private static string ResolveSettlementRating(int moveCount)
        {
            if (moveCount <= 12)
            {
                return UiTextCatalog.Get("water_sort.settlement.rating_great");
            }

            if (moveCount <= 20)
            {
                return UiTextCatalog.Get("water_sort.settlement.rating_good");
            }

            return UiTextCatalog.Get("water_sort.settlement.rating_done");
        }
    }
}
