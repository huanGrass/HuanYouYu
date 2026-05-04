using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 经典倒水量杯谜题：通过装满、倒空和互倒得到目标水量。
    /// </summary>
    public sealed class MiniGameWaterPouringGameView : MiniGameBase
    {
        public const string GameIdConstant = "waterpouring";

        private const int EmptySelection = -1;
        private const float CupHeight = 380f;
        private const float CupWidth = 156f;
        private const float MinCupVisualHeight = 250f;
        private const float WaterBottomPadding = 20f;
        private const float WaterTopPadding = 24f;
        private const float CompletionFocusDuration = 0.86f;

        private static readonly Color ContentPanelColor = new Color(0.95f, 0.98f, 0.96f, 0.72f);
        private static readonly Color CupBodyColor = new Color(0.96f, 0.99f, 1f, 0.72f);
        private static readonly Color CupOutlineColor = new Color(0.31f, 0.46f, 0.48f, 0.72f);
        private static readonly Color CupSelectedColor = new Color(1f, 0.83f, 0.3f, 0.92f);
        private static readonly Color CupNormalColor = new Color(0.76f, 0.88f, 0.89f, 0.84f);
        private static readonly Color CupTargetColor = new Color(1f, 0.72f, 0.18f, 0.96f);
        private static readonly Color CupTargetLineColor = new Color(1f, 0.62f, 0.16f, 0.9f);
        private static readonly Color WaterColor = new Color(0.18f, 0.66f, 0.86f, 0.86f);
        private static readonly Color TextColor = new Color(0.18f, 0.29f, 0.27f, 1f);
        private static readonly Color MutedTextColor = new Color(0.35f, 0.47f, 0.45f, 0.92f);
        private static readonly Color ButtonColor = new Color(0.22f, 0.55f, 0.49f, 1f);
        private static readonly Color DisabledButtonColor = new Color(0.55f, 0.62f, 0.61f, 0.58f);

        private static readonly LevelConfig[] Levels =
        {
            new LevelConfig("waterpouring.level.1", 4, new[] { 3, 5 }),
            new LevelConfig("waterpouring.level.2", 2, new[] { 3, 4 }),
            new LevelConfig("waterpouring.level.3", 6, new[] { 5, 7 }),
            new LevelConfig("waterpouring.level.4", 1, new[] { 4, 9 }),
            new LevelConfig("waterpouring.level.5", 8, new[] { 5, 11 }),
            new LevelConfig("waterpouring.level.6", 7, new[] { 3, 8, 10 }),
            new LevelConfig("waterpouring.level.7", 9, new[] { 4, 7, 13 }),
            new LevelConfig("waterpouring.level.8", 11, new[] { 6, 10, 15 }),
            new LevelConfig("waterpouring.level.9", 5, new[] { 8, 11, 13 }),
            new LevelConfig("waterpouring.level.10", 12, new[] { 7, 10, 19 })
        };

        public static int LevelCount
        {
            get { return Levels.Length; }
        }

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private TextMeshProUGUI targetLabel;
        private TextMeshProUGUI movesLabel;
        private TextMeshProUGUI instructionLabel;
        private RectTransform cupsHost;
        private Button restartButton;
        private Button levelSelectButton;
        private Button fillButton;
        private Button emptyButton;
        private Graphic fillButtonGraphic;
        private Graphic emptyButtonGraphic;

        private MiniGameLevelProgressController levelProgress;
        private MiniGameLevelSelectView levelSelectView;
        private CupState[] cups = Array.Empty<CupState>();
        private int currentLevelIndex;
        private int selectedCupIndex = EmptySelection;
        private int moves;
        private int score;
        private bool inputLocked;
        private Coroutine completionFocusRoutine;

        public MiniGameWaterPouringGameView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "MiniGameWaterPouringView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        protected override void BuildOrBindSections()
        {
            levelProgress = new MiniGameLevelProgressController(HostBehaviour, GameIdConstant, Levels.Length);

            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("WaterPouringHeader"));
            titleLabel = topBarRefs.TitleText;
            scoreLabel = topBarRefs.ScoreText;

            BuildContent();
            BuildBottomActions();

            if (titleLabel == null || scoreLabel == null || cupsHost == null || restartButton == null || levelSelectButton == null || fillButton == null || emptyButton == null)
            {
                throw new InvalidOperationException("WaterPouring prefab structure is incomplete.");
            }
        }

        protected override void ResetGame()
        {
            StopCompletionFocusRoutine();
            Shell.ClosePopup();
            CloseRewardSettlementPanel();
            CloseLevelSelect();
            currentLevelIndex = levelProgress != null ? levelProgress.CurrentLevelIndex : currentLevelIndex;
            LoadLevel(currentLevelIndex);
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.waterpouring.help", null);
        }

        protected override void OnPauseRequested()
        {
            CloseLevelSelect();
            Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        protected override void OnBeforeDispose()
        {
            StopCompletionFocusRoutine();
            Shell.ClosePopup();
            CloseRewardSettlementPanel();

            if (restartButton != null)
            {
                restartButton.onClick.RemoveAllListeners();
            }

            if (levelSelectButton != null)
            {
                levelSelectButton.onClick.RemoveAllListeners();
            }

            if (fillButton != null)
            {
                fillButton.onClick.RemoveAllListeners();
            }

            if (emptyButton != null)
            {
                emptyButton.onClick.RemoveAllListeners();
            }

            RemoveCupListeners();
            CloseLevelSelect();
        }

        private void BuildContent()
        {
            var contentRoot = CreateRectObject("WaterPouringContent", Shell.ContentHost);
            var contentRect = contentRoot.GetComponent<RectTransform>();
            Stretch(contentRect, Vector2.zero, Vector2.one, new Vector2(34f, 26f), new Vector2(-34f, -18f));

            var panel = CreateRoundedRect("GamePanel", contentRect, ContentPanelColor, 34f, false);
            Stretch(panel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            targetLabel = CreateText("TargetLabel", contentRect, 30f, FontStyles.Bold, TextColor);
            targetLabel.alignment = TextAlignmentOptions.Center;
            targetLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            targetLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            targetLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            targetLabel.rectTransform.offsetMin = new Vector2(28f, -82f);
            targetLabel.rectTransform.offsetMax = new Vector2(-28f, -22f);

            movesLabel = CreateText("MovesLabel", contentRect, 24f, FontStyles.Normal, MutedTextColor);
            movesLabel.alignment = TextAlignmentOptions.Center;
            movesLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            movesLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            movesLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            movesLabel.rectTransform.offsetMin = new Vector2(28f, -128f);
            movesLabel.rectTransform.offsetMax = new Vector2(-28f, -84f);

            cupsHost = CreateRectObject("CupsHost", contentRect).GetComponent<RectTransform>();
            cupsHost.anchorMin = new Vector2(0f, 0f);
            cupsHost.anchorMax = new Vector2(1f, 1f);
            cupsHost.offsetMin = new Vector2(22f, 86f);
            cupsHost.offsetMax = new Vector2(-22f, -136f);

            instructionLabel = CreateText("InstructionLabel", contentRect, 22f, FontStyles.Normal, MutedTextColor);
            instructionLabel.alignment = TextAlignmentOptions.Center;
            instructionLabel.enableWordWrapping = true;
            instructionLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            instructionLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
            instructionLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
            instructionLabel.rectTransform.offsetMin = new Vector2(34f, 22f);
            instructionLabel.rectTransform.offsetMax = new Vector2(-34f, 78f);
        }

        private void BuildBottomActions()
        {
            var bottomContainerRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("WaterPouringActions"));

            restartButton = MiniGameShellBottomBarBuilder.CreateRestartButton(bottomContainerRefs.ActionBar).Button;
            if (restartButton != null)
            {
                restartButton.gameObject.name = "RestartButton";
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            levelSelectButton = MiniGameShellBottomBarBuilder.CreateLevelSelectButton(bottomContainerRefs.ActionBar, "LevelSelectButton").Button;
            if (levelSelectButton != null)
            {
                levelSelectButton.onClick.RemoveAllListeners();
                levelSelectButton.onClick.AddListener(OnLevelSelectClicked);
            }

            fillButton = CreateTextActionButton(bottomContainerRefs.ActionBar, "FillButton", UiTextCatalog.Get("waterpouring.action.fill"), out fillButtonGraphic);
            fillButton.onClick.AddListener(OnFillClicked);

            emptyButton = CreateTextActionButton(bottomContainerRefs.ActionBar, "EmptyButton", UiTextCatalog.Get("waterpouring.action.empty"), out emptyButtonGraphic);
            emptyButton.onClick.AddListener(OnEmptyClicked);
        }

        private void LoadLevel(int levelIndex)
        {
            currentLevelIndex = Mathf.Clamp(levelIndex, 0, Levels.Length - 1);
            selectedCupIndex = EmptySelection;
            moves = 0;
            score = 0;
            inputLocked = false;
            BuildCups(Levels[currentLevelIndex]);
            RefreshAll();
        }

        private void BuildCups(LevelConfig level)
        {
            RemoveCupListeners();

            for (var i = cupsHost.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(cupsHost.GetChild(i).gameObject);
            }

            cups = new CupState[level.Capacities.Length];
            var spacing = level.Capacities.Length == 2 ? 210f : 178f;
            var startX = -spacing * (level.Capacities.Length - 1) * 0.5f;
            GetCapacityBounds(level.Capacities, out var smallestCapacity, out var largestCapacity);

            for (var i = 0; i < level.Capacities.Length; i++)
            {
                cups[i] = CreateCup(i, level.Capacities[i], smallestCapacity, largestCapacity, new Vector2(startX + spacing * i, -6f));
            }
        }

        private CupState CreateCup(int index, int capacity, int smallestCapacity, int largestCapacity, Vector2 anchoredPosition)
        {
            var capacityRange = largestCapacity - smallestCapacity;
            var capacityRatio = capacityRange <= 0 ? 1f : Mathf.Clamp01((capacity - smallestCapacity) / (float)capacityRange);
            var visualHeight = Mathf.Lerp(MinCupVisualHeight, CupHeight, capacityRatio);
            var visualWidth = CupWidth;
            var cupCenterY = 8f - (CupHeight - visualHeight) * 0.5f;

            var cupRoot = CreateRectObject("Cup_" + index, cupsHost);
            var rootRect = cupRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(CupWidth + 42f, CupHeight + 120f);
            rootRect.anchoredPosition = anchoredPosition;

            var button = cupRoot.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(delegate { OnCupClicked(index); });

            var selectionFrame = CreateRoundedRect("SelectionFrame", rootRect, CupNormalColor, 28f, true);
            var selectionRect = selectionFrame.rectTransform;
            selectionRect.anchorMin = new Vector2(0.5f, 0.5f);
            selectionRect.anchorMax = new Vector2(0.5f, 0.5f);
            selectionRect.pivot = new Vector2(0.5f, 0.5f);
            selectionRect.sizeDelta = new Vector2(visualWidth + 28f, visualHeight + 36f);
            selectionRect.anchoredPosition = new Vector2(0f, cupCenterY);
            button.targetGraphic = selectionFrame;

            var cupBody = CreateRoundedRect("CupBody", rootRect, CupBodyColor, 18f, false);
            var cupRect = cupBody.rectTransform;
            cupRect.anchorMin = new Vector2(0.5f, 0.5f);
            cupRect.anchorMax = new Vector2(0.5f, 0.5f);
            cupRect.pivot = new Vector2(0.5f, 0.5f);
            cupRect.sizeDelta = new Vector2(visualWidth, visualHeight);
            cupRect.anchoredPosition = new Vector2(0f, cupCenterY);

            var outline = CreateRoundedRect("CupOutline", cupRect, CupOutlineColor, 16f, false);
            Stretch(outline.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var inner = CreateRoundedRect("CupInner", cupRect, new Color(1f, 1f, 1f, 0.76f), 13f, false);
            Stretch(inner.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));

            var water = CreateRoundedRect("WaterFill", cupRect, WaterColor, 11f, false);
            water.rectTransform.anchorMin = new Vector2(0f, 0f);
            water.rectTransform.anchorMax = new Vector2(1f, 0f);
            water.rectTransform.offsetMin = new Vector2(14f, WaterBottomPadding);
            water.rectTransform.offsetMax = new Vector2(-14f, WaterBottomPadding);

            var targetLine = CreateRoundedRect("TargetLine", cupRect, CupTargetLineColor, 2f, false);
            var targetLineRect = targetLine.rectTransform;
            targetLineRect.anchorMin = new Vector2(0f, 0f);
            targetLineRect.anchorMax = new Vector2(1f, 0f);
            targetLineRect.pivot = new Vector2(0.5f, 0.5f);
            targetLineRect.offsetMin = new Vector2(12f, WaterBottomPadding);
            targetLineRect.offsetMax = new Vector2(-12f, WaterBottomPadding + 5f);

            CreateTickMarks(cupRect, visualHeight, capacity);

            var amountLabel = CreateText("AmountLabel", rootRect, 24f, FontStyles.Bold, TextColor);
            amountLabel.alignment = TextAlignmentOptions.Center;
            amountLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            amountLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
            amountLabel.rectTransform.offsetMin = new Vector2(0f, 0f);
            amountLabel.rectTransform.offsetMax = new Vector2(0f, 42f);

            var capacityLabel = CreateText("CapacityLabel", rootRect, 20f, FontStyles.Normal, MutedTextColor);
            capacityLabel.alignment = TextAlignmentOptions.Center;
            capacityLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            capacityLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            capacityLabel.rectTransform.offsetMin = new Vector2(0f, -38f);
            capacityLabel.rectTransform.offsetMax = new Vector2(0f, -6f);

            return new CupState
            {
                Capacity = capacity,
                Amount = 0,
                CupButton = button,
                SelectionGraphic = selectionFrame,
                WaterFill = water.rectTransform,
                TargetLine = targetLineRect,
                VisualHeight = visualHeight,
                RootTransform = rootRect,
                AmountLabel = amountLabel,
                CapacityLabel = capacityLabel
            };
        }

        private void CreateTickMarks(RectTransform cupRect, float visualHeight, int capacity)
        {
            var divisionCount = Mathf.Clamp(capacity, 3, 10);
            for (var i = 1; i <= divisionCount; i++)
            {
                var tick = CreateRoundedRect("Tick_" + i, cupRect, new Color(0.34f, 0.47f, 0.48f, 0.42f), 1f, false);
                var rect = tick.rectTransform;
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0.5f);
                rect.sizeDelta = new Vector2(i == divisionCount ? 28f : 18f, 3f);
                rect.anchoredPosition = new Vector2(-18f, WaterBottomPadding + (visualHeight - WaterBottomPadding - WaterTopPadding) * i / divisionCount);
            }
        }

        private static void GetCapacityBounds(int[] capacities, out int smallestCapacity, out int largestCapacity)
        {
            smallestCapacity = int.MaxValue;
            largestCapacity = 0;
            for (var i = 0; i < capacities.Length; i++)
            {
                smallestCapacity = Mathf.Min(smallestCapacity, capacities[i]);
                largestCapacity = Mathf.Max(largestCapacity, capacities[i]);
            }
        }

        private void OnCupClicked(int cupIndex)
        {
            if (inputLocked || cupIndex < 0 || cupIndex >= cups.Length)
            {
                return;
            }

            if (selectedCupIndex == EmptySelection)
            {
                SelectCup(cupIndex);
                MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.85f);
                return;
            }

            if (selectedCupIndex == cupIndex)
            {
                SelectCup(EmptySelection);
                MiniGameSfxPlayer.Play(MiniGameSfxType.UiBack, 0.82f);
                return;
            }

            var sourceIndex = selectedCupIndex;
            if (Pour(sourceIndex, cupIndex))
            {
                CompleteMove();
            }
            else
            {
                SelectCup(cupIndex);
                MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.82f);
            }
        }

        private void OnFillClicked()
        {
            if (inputLocked || selectedCupIndex == EmptySelection)
            {
                RefreshInstruction();
                return;
            }

            var cup = cups[selectedCupIndex];
            if (cup.Amount >= cup.Capacity)
            {
                RefreshInstruction();
                return;
            }

            cup.Amount = cup.Capacity;
            CompleteMove();
        }

        private void OnEmptyClicked()
        {
            if (inputLocked || selectedCupIndex == EmptySelection)
            {
                RefreshInstruction();
                return;
            }

            var cup = cups[selectedCupIndex];
            if (cup.Amount <= 0)
            {
                RefreshInstruction();
                return;
            }

            cup.Amount = 0;
            CompleteMove();
        }

        private bool Pour(int sourceIndex, int targetIndex)
        {
            var source = cups[sourceIndex];
            var target = cups[targetIndex];
            if (source.Amount <= 0 || target.Amount >= target.Capacity)
            {
                return false;
            }

            var transfer = Mathf.Min(source.Amount, target.Capacity - target.Amount);
            source.Amount -= transfer;
            target.Amount += transfer;
            return transfer > 0;
        }

        private void CompleteMove()
        {
            moves += 1;
            SelectCup(EmptySelection);
            RefreshAll();
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.92f);
            CheckWin();
        }

        private void CheckWin()
        {
            var target = Levels[currentLevelIndex].TargetAmount;
            for (var i = 0; i < cups.Length; i++)
            {
                if (cups[i].Amount == target)
                {
                    HandleWin(i);
                    return;
                }
            }
        }

        private void HandleWin(int winningCupIndex)
        {
            inputLocked = true;
            selectedCupIndex = EmptySelection;
            score = Mathf.Max(100, 1000 - moves * 20);
            RefreshAll();
            if (levelProgress != null)
            {
                levelProgress.UnlockNext();
            }

            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);

            var settlement = CreateSettlement(UiTextCatalog.Format("waterpouring.settlement.win_summary", GetLevelName(), moves, score));
            var isLastLevel = currentLevelIndex >= Levels.Length - 1;
            StopCompletionFocusRoutine();
            completionFocusRoutine = HostBehaviour.StartCoroutine(ShowCompletionAfterFocus(winningCupIndex, settlement, isLastLevel));
        }

        private IEnumerator ShowCompletionAfterFocus(int winningCupIndex, MiniGameSettlement settlement, bool isLastLevel)
        {
            var cup = winningCupIndex >= 0 && winningCupIndex < cups.Length ? cups[winningCupIndex] : null;
            var elapsed = 0f;
            while (elapsed < CompletionFocusDuration)
            {
                var t = elapsed / CompletionFocusDuration;
                var pulse = Mathf.Sin(t * Mathf.PI * 3f);
                if (cup != null)
                {
                    if (cup.RootTransform != null)
                    {
                        var scale = 1f + pulse * 0.08f;
                        cup.RootTransform.localScale = new Vector3(scale, scale, 1f);
                    }

                    if (cup.SelectionGraphic != null)
                    {
                        cup.SelectionGraphic.color = Color.Lerp(CupTargetColor, CupSelectedColor, 0.5f + pulse * 0.5f);
                    }
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (cup != null)
            {
                if (cup.RootTransform != null)
                {
                    cup.RootTransform.localScale = Vector3.one;
                }

                if (cup.SelectionGraphic != null)
                {
                    cup.SelectionGraphic.color = CupTargetColor;
                }
            }

            completionFocusRoutine = null;
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "WaterPouringWinSettlementPanel",
                    Style = MiniGameRewardSettlementPanelStyle.Success,
                    PrimaryAction = isLastLevel ? MiniGameRewardSettlementPrimaryAction.BackHall : MiniGameRewardSettlementPrimaryAction.NextLevel,
                    Title = UiTextCatalog.Get("waterpouring.settlement.win_title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("waterpouring.settlement.level"), GetLevelName()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("waterpouring.settlement.moves"), moves.ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                isLastLevel ? delegate { CompleteGame?.Invoke(settlement); } : LoadNextLevel,
                delegate
                {
                    SaveNextLevelForReturn();
                    CompleteGame?.Invoke(settlement);
                },
                true);
        }

        private void StopCompletionFocusRoutine()
        {
            if (completionFocusRoutine == null || HostBehaviour == null)
            {
                completionFocusRoutine = null;
                return;
            }

            HostBehaviour.StopCoroutine(completionFocusRoutine);
            completionFocusRoutine = null;
            ResetCupScales();
        }

        private void ResetCupScales()
        {
            if (cups == null)
            {
                return;
            }

            for (var i = 0; i < cups.Length; i++)
            {
                if (cups[i] != null && cups[i].RootTransform != null)
                {
                    cups[i].RootTransform.localScale = Vector3.one;
                }
            }
        }

        private void LoadNextLevel()
        {
            if (levelProgress != null && levelProgress.GoNext())
            {
                ResetGame();
                return;
            }

            LoadLevel(currentLevelIndex + 1);
        }

        private void SaveNextLevelForReturn()
        {
            if (levelProgress != null)
            {
                levelProgress.SaveNextAsCurrent();
            }
        }

        private void OnRestartClicked()
        {
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            ResetGame();
        }

        private void OnLevelSelectClicked()
        {
            if (completionFocusRoutine != null)
            {
                return;
            }

            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.9f);
            if (levelSelectView != null)
            {
                CloseLevelSelect();
                return;
            }

            levelSelectView = MiniGameLevelSelectView.Create(
                Shell.PopupHost,
                MiniGameFontProvider.DefaultFont,
                Levels.Length,
                levelProgress != null ? levelProgress.CurrentLevelIndex : currentLevelIndex,
                levelProgress != null ? levelProgress.UnlockedLevelCount : 1,
                "WaterPouringLevelSelectPanel",
                "WaterPouringLevelButton_",
                OnLevelSelected,
                CloseLevelSelect);
        }

        private void OnLevelSelected(int index)
        {
            if (levelProgress == null || !levelProgress.Select(index))
            {
                return;
            }

            CloseLevelSelect();
            ResetGame();
        }

        private void CloseLevelSelect()
        {
            if (levelSelectView == null)
            {
                return;
            }

            levelSelectView.Dispose();
            levelSelectView = null;
        }

        private void SelectCup(int cupIndex)
        {
            selectedCupIndex = cupIndex;
            RefreshCupSelection();
            RefreshInstruction();
            RefreshActionButtons();
        }

        private void RefreshAll()
        {
            RefreshHud();
            RefreshCups();
            RefreshCupSelection();
            RefreshInstruction();
            RefreshActionButtons();
        }

        private void RefreshHud()
        {
            if (titleLabel != null)
            {
                titleLabel.text = UiTextCatalog.Get("game.waterpouring.name");
            }

            if (scoreLabel != null)
            {
                scoreLabel.text = UiTextCatalog.Format("waterpouring.hud.score", score);
            }

            if (targetLabel != null)
            {
                targetLabel.text = UiTextCatalog.Format("waterpouring.hud.target", GetLevelName(), Levels[currentLevelIndex].TargetAmount);
            }

            if (movesLabel != null)
            {
                movesLabel.text = UiTextCatalog.Format("waterpouring.hud.moves", moves);
            }
        }

        private void RefreshCups()
        {
            for (var i = 0; i < cups.Length; i++)
            {
                RefreshCup(cups[i]);
            }
        }

        private void RefreshCup(CupState cup)
        {
            if (cup == null)
            {
                return;
            }

            var ratio = cup.Capacity <= 0 ? 0f : Mathf.Clamp01(cup.Amount / (float)cup.Capacity);
            if (cup.WaterFill != null)
            {
                var fillHeight = (cup.VisualHeight - WaterBottomPadding - WaterTopPadding) * ratio;
                cup.WaterFill.offsetMin = new Vector2(14f, WaterBottomPadding);
                cup.WaterFill.offsetMax = new Vector2(-14f, WaterBottomPadding + fillHeight);
            }

            if (cup.TargetLine != null)
            {
                var target = Levels[currentLevelIndex].TargetAmount;
                var targetVisible = target > 0 && target <= cup.Capacity;
                cup.TargetLine.gameObject.SetActive(targetVisible);
                if (targetVisible)
                {
                    var targetRatio = Mathf.Clamp01(target / (float)cup.Capacity);
                    var targetY = WaterBottomPadding + (cup.VisualHeight - WaterBottomPadding - WaterTopPadding) * targetRatio;
                    cup.TargetLine.offsetMin = new Vector2(12f, targetY - 2.5f);
                    cup.TargetLine.offsetMax = new Vector2(-12f, targetY + 2.5f);
                }
            }

            if (cup.AmountLabel != null)
            {
                cup.AmountLabel.text = UiTextCatalog.Format("waterpouring.cup.amount", cup.Amount, cup.Capacity);
            }

            if (cup.CapacityLabel != null)
            {
                cup.CapacityLabel.text = UiTextCatalog.Format("waterpouring.cup.capacity", cup.Capacity);
            }

            if (cup.CupButton != null)
            {
                cup.CupButton.interactable = !inputLocked;
            }
        }

        private void RefreshCupSelection()
        {
            for (var i = 0; i < cups.Length; i++)
            {
                if (cups[i].SelectionGraphic != null)
                {
                    var isCompletedTarget = inputLocked && cups[i].Amount == Levels[currentLevelIndex].TargetAmount;
                    cups[i].SelectionGraphic.color = isCompletedTarget ? CupTargetColor : (i == selectedCupIndex ? CupSelectedColor : CupNormalColor);
                }
            }
        }

        private void RefreshInstruction()
        {
            if (instructionLabel == null)
            {
                return;
            }

            if (inputLocked)
            {
                instructionLabel.text = UiTextCatalog.Get("waterpouring.tip.completed");
            }
            else if (selectedCupIndex == EmptySelection)
            {
                instructionLabel.text = UiTextCatalog.Get("waterpouring.tip.select_cup");
            }
            else
            {
                instructionLabel.text = UiTextCatalog.Format("waterpouring.tip.selected", selectedCupIndex + 1);
            }
        }

        private void RefreshActionButtons()
        {
            var canOperate = !inputLocked && selectedCupIndex != EmptySelection;
            SetButtonState(fillButton, fillButtonGraphic, canOperate);
            SetButtonState(emptyButton, emptyButtonGraphic, canOperate);
        }

        private static void SetButtonState(Button button, Graphic graphic, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }

            if (graphic != null)
            {
                graphic.color = interactable ? ButtonColor : DisabledButtonColor;
            }
        }

        private string GetLevelName()
        {
            return UiTextCatalog.Get(Levels[currentLevelIndex].NameKey);
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
        }

        private void ConfirmExitToHall()
        {
            StopCompletionFocusRoutine();
            Shell.ClosePopup();
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            var settlement = CreateSettlement(UiTextCatalog.Format("waterpouring.settlement.exit_summary", GetLevelName(), score));
            ShowBackHallRewardSettlementPanel(
                settlement,
                "WaterPouringSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("waterpouring.settlement.score"), settlement.Score.ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("waterpouring.settlement.exit_label"), UiTextCatalog.Get("waterpouring.settlement.exit_value")),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private MiniGameSettlement CreateSettlement(string summary)
        {
            var isCompleted = score > 0;
            var coinCount = isCompleted ? Mathf.Max(25, score / 18) : 0;
            return new MiniGameSettlement
            {
                Score = score,
                CoinCount = coinCount,
                ChestCount = isCompleted ? 1 : 0,
                Summary = summary
            };
        }

        private void RemoveCupListeners()
        {
            if (cups == null)
            {
                return;
            }

            for (var i = 0; i < cups.Length; i++)
            {
                if (cups[i] != null && cups[i].CupButton != null)
                {
                    cups[i].CupButton.onClick.RemoveAllListeners();
                }
            }
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, FontStyles style, Color color)
        {
            var textObject = CreateRectObject(name, parent);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            var fontAsset = MiniGameFontProvider.DefaultFont;
            if (fontAsset != null)
            {
                text.font = fontAsset;
            }

            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            return text;
        }

        private static Button CreateTextActionButton(Transform parent, string name, string labelText, out Graphic backgroundGraphic)
        {
            var buttonObject = CreateRectObject(name, parent);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(132f, 72f);

            var layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 132f;
            layoutElement.preferredHeight = 72f;
            layoutElement.layoutPriority = 1;

            var background = CreateRoundedRect("Background", buttonRect, ButtonColor, 26f, true);
            Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            backgroundGraphic = background;

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.96f, 0.98f, 0.98f, 1f);
            colors.pressedColor = new Color(0.82f, 0.9f, 0.88f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = Color.white;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var label = CreateText("Label", buttonRect, 25f, FontStyles.Bold, Color.white);
            label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            label.text = labelText;
            return button;
        }

        private static RoundedRectGraphic CreateRoundedRect(string name, Transform parent, Color color, float cornerRadius, bool raycastTarget)
        {
            var gameObject = CreateRectObject(name, parent);
            gameObject.AddComponent<CanvasRenderer>();
            var graphic = gameObject.AddComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = cornerRadius;
            graphic.raycastTarget = raycastTarget;
            return graphic;
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private sealed class LevelConfig
        {
            public LevelConfig(string nameKey, int targetAmount, int[] capacities)
            {
                NameKey = nameKey;
                TargetAmount = targetAmount;
                Capacities = capacities;
            }

            public string NameKey { get; }

            public int TargetAmount { get; }

            public int[] Capacities { get; }
        }

        private sealed class CupState
        {
            public int Capacity;
            public int Amount;
            public Button CupButton;
            public Graphic SelectionGraphic;
            public RectTransform WaterFill;
            public RectTransform TargetLine;
            public float VisualHeight;
            public RectTransform RootTransform;
            public TextMeshProUGUI AmountLabel;
            public TextMeshProUGUI CapacityLabel;
        }
    }
}
