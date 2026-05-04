using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed partial class WaterSortGameView : MiniGameBase
    {
        public const string GameIdConstant = "water-sort";

        private const int BottleCapacity = 4;
        private const float BottleEmptyEpsilon = 0.001f;
        private const int CoinsPerCompletedBottle = 12;
        private const float FallbackContentWidth = 620f;
        private const float FallbackContentHeight = 680f;
        private const float MaxCellWidth = 150f;
        private const float MaxCellHeight = 238f;
        private const float BottleAspect = 0.62f;
        private const float PourBaseDuration = 0.72f;
        private const float PourDurationPerLayer = 0.42f;
        private const float PourDurationScale = 2f;
        private const float PourFlowDurationRatio = 0.34f;
        private const float PourMoveBaseDuration = 0.16f;
        private const float PourMoveSpeed = 900f;
        private const float PourPreFlowDelay = 0.05f;
        private const float PourReceiveMinSpeed = 0.01f;
        private const float PourMoveLift = 92f;
        private const float PourExtraLift = -64f;
        private const float MinPourTiltDegrees = 12f;
        private const float MaxPourTiltDegrees = 90f;
        private const float IdleWaveAmplitude = 0f;
        private const float ActiveWaveAmplitude = 0f;
        private const float PourMouthLiquidClipInset = 2f;
        private const float BottleLiquidHorizontalOverflow = 8f;
        private const float BottleBottomLiquidVerticalOverflowPixels = 72f;
        private const float BottleLiquidMaxHeightAnchor = 3f;
        private const float BottleLiquidLookupMaxValueEpsilon = 0.0001f;
        private const int BottlePourTiltSearchIterations = 12;
        private const int MaxWaterColorCount = 12;
        private const float BottleSideDockGap = 16f;
        private const float BottleShapeHorizontalInset = 20f;
        private const float BottleShapeTopInset = 14f;
        private const float BottleShapeTopStrokeInset = 3f;
        private const float BottleShapeHalfWidthRatio = 0.28f;
        private const float BottleFillHorizontalInset = 44f;
        private const float BottleFillTopInset = 31f;
        private const float BottleFillBottomInset = 17f;
        private const float BottleFullFillRatio = 0.94f;
        private const float BottleCapWidth = 70f;
        private const float BottleCapHeight = 16f;
        private const float BottleCapTopInset = 23f;
        private const float BottleCapCornerRadius = 7f;
        private const float BottleCapDropOffset = 34f;
        private const float BottleCapDropDuration = 0.22f;
        private const float BottleSelectionLift = 16f;
        private const float PourStreamStartOffset = 12f;
        private const float PourStreamEndInset = 16f;
        private const float PourStreamTargetSurfaceInset = 6f;
        private const string LevelResourcePath = "Levels/water-sort.levels";

        private static readonly Color32[] WaterColors =
        {
            new Color32(238, 85, 92, 255),
            new Color32(69, 148, 224, 255),
            new Color32(248, 188, 68, 255),
            new Color32(91, 183, 112, 255),
            new Color32(152, 105, 214, 255),
            new Color32(246, 132, 63, 255),
            new Color32(45, 185, 188, 255),
            new Color32(232, 91, 166, 255),
            new Color32(132, 171, 54, 255),
            new Color32(111, 116, 224, 255),
            new Color32(184, 117, 62, 255),
            new Color32(74, 75, 89, 255)
        };

        private static readonly WaterSortLevelDefinition[] LevelDefinitions = LoadLevelDefinitions();

        public static int LevelCount
        {
            get { return LevelDefinitions.Length; }
        }

        private readonly List<List<int>> bottles = new List<List<int>>();
        private readonly List<BottleView> bottleViews = new List<BottleView>();

        private TMP_FontAsset fontAsset;
        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private RectTransform contentRoot;
        private RectTransform bottleGrid;
        private RectTransform streamLayer;
        private Button levelSelectButton;
        private Button restartButton;
        private MiniGameLevelSelectView levelSelectView;
        private MiniGameLevelProgressController levelProgress;
        private int currentLevelIndex;
        private int unlockedLevelCount = 1;
        private int selectedBottleIndex = -1;
        private int moveCount;
        private bool settlementShown;
        private Vector2 lastContentSize;
        private readonly List<PourAnimationState> activePourAnimations = new List<PourAnimationState>();
        private readonly HashSet<int> lockedSourceBottleIndices = new HashSet<int>();
        private readonly Dictionary<int, BottleReceiveAnimationState> receiveAnimations = new Dictionary<int, BottleReceiveAnimationState>();

        public WaterSortGameView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "WaterSortView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        private static Color32 GetWaterColor(int colorIndex)
        {
            if (colorIndex < 0 || colorIndex >= MaxWaterColorCount)
            {
                throw new ArgumentOutOfRangeException(nameof(colorIndex), "水排序颜色值越界: " + colorIndex);
            }

            return WaterColors[colorIndex];
        }

        protected override void BuildOrBindSections()
        {
            fontAsset = MiniGameFontProvider.DefaultFont;

            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("WaterSortTop"));
            titleLabel = topBarRefs.TitleText;
            scoreLabel = topBarRefs.ScoreText;

            BuildContentSection();
            BuildBottomSection();
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            CloseRewardSettlementPanel();
            CloseLevelSelectView();
            StopPourAnimation();
            EnsureLevelProgress();
            selectedBottleIndex = -1;
            moveCount = 0;
            settlementShown = false;
            lastContentSize = Vector2.zero;
            LoadCurrentPuzzle();
            BuildBottleViews();
            RefreshAll();
        }

        public override void Tick(float deltaTime)
        {
            RefreshGridIfContentSizeChanged();
            AdvanceBottleReceiveAnimations(deltaTime);
            AdvanceBottleCapAnimations(deltaTime);
            AdvanceIdleWater(deltaTime);
        }

        protected override void OnPauseRequested()
        {
            if (settlementShown || activePourAnimations.Count > 0)
            {
                return;
            }

            Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        protected override void OnBeforeDispose()
        {
            Shell.ClosePopup();
            CloseRewardSettlementPanel();
            StopPourAnimation();

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            if (levelSelectButton != null)
            {
                levelSelectButton.onClick.RemoveListener(OnLevelSelectClicked);
            }

        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.water_sort.help", null);
        }

        private void CompleteRound()
        {
            settlementShown = true;
            EnsureLevelProgress();
            levelProgress.UnlockNext();
            SyncLevelProgressFields();
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            ShowWinSettlement(CreateSettlement(true));
        }

        private void ConfirmExitToHall()
        {
            Shell.ClosePopup();
            StopPourAnimation();
            selectedBottleIndex = -1;
            settlementShown = true;
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            var settlement = CreateSettlement(false);
            ShowBackHallRewardSettlementPanel(
                settlement,
                "WaterSortSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("water_sort.settlement.steps"), moveCount + UiTextCatalog.Get("water_sort.settlement.step_unit")),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("water_sort.settlement.rating"), ResolveSettlementRating(moveCount)),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private MiniGameSettlement CreateSettlement(bool completed)
        {
            var completedBottleCount = CountCompletedBottles();
            var coinCount = completedBottleCount * CoinsPerCompletedBottle;
            var chestCount = completed ? 1 : 0;
            return new MiniGameSettlement
            {
                Score = completed ? Mathf.Max(1, 100 - moveCount) : completedBottleCount,
                CoinCount = coinCount,
                ChestCount = chestCount,
                Summary = completed
                    ? UiTextCatalog.Format("water_sort.settlement.win", moveCount, coinCount, chestCount)
                    : UiTextCatalog.Format("water_sort.settlement.exit", completedBottleCount, coinCount, chestCount)
            };
        }

        private bool IsPuzzleSolved()
        {
            for (var i = 0; i < bottles.Count; i++)
            {
                var bottle = bottles[i];
                if (bottle.Count == 0)
                {
                    continue;
                }

                if (!IsCompletedBottle(bottle))
                {
                    return false;
                }
            }

            return true;
        }

        private void TryCompleteRoundAfterAnimations()
        {
            if (settlementShown || activePourAnimations.Count > 0 || receiveAnimations.Count > 0)
            {
                return;
            }

            RefreshBottleViews();
            RefreshBottleSelection();
            if (IsPuzzleSolved())
            {
                CompleteRound();
            }
        }

        private int CountCompletedBottles()
        {
            var count = 0;
            for (var i = 0; i < bottles.Count; i++)
            {
                if (IsCompletedBottle(bottles[i]))
                {
                    count += 1;
                }
            }

            return count;
        }

        private static bool IsCompletedBottle(List<int> bottle)
        {
            if (bottle == null || bottle.Count != BottleCapacity)
            {
                return false;
            }

            var color = bottle[0];
            for (var i = 1; i < bottle.Count; i++)
            {
                if (bottle[i] != color)
                {
                    return false;
                }
            }

            return true;
        }

        private void SelectLevel(int index)
        {
            EnsureLevelProgress();
            if (!levelProgress.CanSelect(index))
            {
                return;
            }

            if (levelProgress.CurrentLevelIndex == index)
            {
                CloseLevelSelectView();
                return;
            }

            levelProgress.Select(index);
            SyncLevelProgressFields();
            ResetGame();
        }

        private void OnRestartClicked()
        {
            ResetGame();
        }

        private void OnLevelSelectClicked()
        {
            ShowLevelSelectView();
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
        }

        private bool CanGoNextLevel()
        {
            EnsureLevelProgress();
            return levelProgress.CanGoNext();
        }

        private void LoadNextLevel(MiniGameSettlement settlement)
        {
            EnsureLevelProgress();
            if (!CanGoNextLevel())
            {
                CompleteGame?.Invoke(settlement);
                return;
            }

            levelProgress.GoNext();
            GrantSettlementReward(settlement);
            SyncLevelProgressFields();
            ResetGame();
        }

        private void SaveNextLevelForReturn()
        {
            EnsureLevelProgress();
            levelProgress.SaveNextAsCurrent();
            SyncLevelProgressFields();
        }

        private void EnsureLevelProgress()
        {
            if (levelProgress == null)
            {
                levelProgress = new MiniGameLevelProgressController(HostBehaviour, GameIdConstant, LevelDefinitions.Length);
            }

            SyncLevelProgressFields();
        }

        private void SyncLevelProgressFields()
        {
            if (levelProgress == null)
            {
                currentLevelIndex = 0;
                unlockedLevelCount = 1;
                return;
            }

            currentLevelIndex = levelProgress.CurrentLevelIndex;
            unlockedLevelCount = levelProgress.UnlockedLevelCount;
        }

    }
}
