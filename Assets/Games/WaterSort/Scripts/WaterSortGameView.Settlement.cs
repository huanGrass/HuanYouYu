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

            Shell.ClosePopup();
            CloseLevelSelectView();
            CloseSettlementView();
            activeSettlement = settlement;
            settlementView = MiniGameWinSettlementView.Create(
                Shell.PopupHost,
                fontAsset,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "WaterSortSettlementPanel",
                    Title = UiTextCatalog.Get("water_sort.settlement.title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("water_sort.settlement.steps"), moveCount + UiTextCatalog.Get("water_sort.settlement.step_unit")),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("water_sort.settlement.rating"), ResolveSettlementRating(moveCount)),
                    RewardLabel = UiTextCatalog.Get("water_sort.settlement.reward"),
                    NextButtonText = UiTextCatalog.Get("water_sort.settlement.next"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                LoadNextLevel,
                CompleteSettlement);
        }

        private void CompleteSettlement()
        {
            if (activeSettlement == null)
            {
                return;
            }

            var settlement = activeSettlement;
            CloseSettlementView();
            CompleteGame?.Invoke(settlement);
        }

        private void CloseSettlementView()
        {
            if (settlementView != null)
            {
                settlementView.Dispose();
                settlementView = null;
            }

            activeSettlement = null;
        }

        private void ShowLevelSelectView()
        {
            Shell.ClosePopup();
            CloseSettlementView();
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

        private void AdvanceSettlementView(float deltaTime)
        {
            if (settlementView != null)
            {
                settlementView.Tick(deltaTime);
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
