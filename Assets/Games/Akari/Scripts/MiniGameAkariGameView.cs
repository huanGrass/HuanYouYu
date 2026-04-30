using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed class MiniGameAkariGameView : MiniGameBase
    {
        public const string GameIdConstant = "akari";

        private const float BoardSize = 618f;
        private const int RandomOptionValue = 0;

        private static readonly Color BoardPanelColor = new Color(0.96f, 0.94f, 0.84f, 0.94f);
        private static readonly Color BoardShadowColor = new Color(0.23f, 0.29f, 0.22f, 0.16f);
        private static readonly Color WhiteCellColor = new Color(0.98f, 0.97f, 0.90f, 1f);
        private static readonly Color LitCellColor = new Color(1f, 0.91f, 0.48f, 1f);
        private static readonly Color BulbCellColor = new Color(1f, 0.78f, 0.20f, 1f);
        private static readonly Color BlackCellColor = new Color(0.20f, 0.23f, 0.22f, 1f);
        private static readonly Color ConflictCellColor = new Color(0.88f, 0.28f, 0.24f, 1f);
        private static readonly Color WhiteTextColor = new Color(0.29f, 0.25f, 0.15f, 1f);
        private static readonly Color BlackTextColor = new Color(0.97f, 0.93f, 0.78f, 1f);
        private static readonly Color ConflictTextColor = new Color(1f, 0.36f, 0.30f, 1f);
        private static readonly Color SatisfiedTextColor = new Color(0.58f, 0.92f, 0.45f, 1f);
        private static readonly Color StatusTextColor = new Color(0.42f, 0.34f, 0.18f, 1f);

        private readonly List<Button> cellButtons = new List<Button>();
        private readonly List<RoundedRectGraphic> cellBackgrounds = new List<RoundedRectGraphic>();
        private readonly List<TextMeshProUGUI> cellLabels = new List<TextMeshProUGUI>();
        private readonly List<AkariBulbView> cellBulbs = new List<AkariBulbView>();

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private TextMeshProUGUI bulbLabel;
        private TextMeshProUGUI statusLabel;
        private MiniGameDropdown difficultyDropdown;
        private MiniGameDropdown gridSizeDropdown;
        private Button restartButton;
        private RectTransform boardPanel;
        private GridLayoutGroup boardLayout;

        private System.Random random;
        private AkariPuzzle currentPuzzle;
        private AkariEvaluation currentEvaluation;
        private bool[] playerBulbs;
        private int currentQuestionNumber;
        private int score;
        private int completedPuzzleCount;
        private int moveCount;
        private int selectedDifficultyOption;
        private int selectedGridSizeOption;
        private bool isTransitioning;

        public MiniGameAkariGameView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "MiniGameAkariView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        protected override void BuildOrBindSections()
        {
            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                CreateAkariTopBarConfig());
            titleLabel = topBarRefs.TitleText;
            scoreLabel = topBarRefs.ScoreText;

            BuildContent();

            var bottomContainerRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("AkariActions"));
            restartButton = MiniGameShellBottomBarBuilder.CreateRestartButton(bottomContainerRefs.ActionBar).Button;

            if (restartButton != null)
            {
                restartButton.gameObject.name = "RestartButton";
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (titleLabel == null ||
                scoreLabel == null ||
                bulbLabel == null ||
                statusLabel == null ||
                difficultyDropdown == null ||
                gridSizeDropdown == null ||
                restartButton == null ||
                boardPanel == null ||
                boardLayout == null)
            {
                throw new InvalidOperationException("Akari prefab structure is incomplete.");
            }
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            random = new System.Random(unchecked(Environment.TickCount * 397) ^ DateTime.Now.Millisecond);
            score = 0;
            completedPuzzleCount = 0;
            StartPuzzle(1);
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.akari.help", null);
        }

        protected override void OnPauseRequested()
        {
            Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        protected override void OnBeforeDispose()
        {
            Shell.ClosePopup();

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            if (difficultyDropdown != null)
            {
                difficultyDropdown.Close();
            }

            if (gridSizeDropdown != null)
            {
                gridSizeDropdown.Close();
            }

            for (var i = 0; i < cellButtons.Count; i++)
            {
                if (cellButtons[i] != null)
                {
                    cellButtons[i].onClick.RemoveAllListeners();
                }
            }
        }

        private static MiniGameShellTopBarBuilder.TopBarConfig CreateAkariTopBarConfig()
        {
            var config = MiniGameShellTopBarBuilder.CreateDefaultConfig("AkariHeader");
            config.PreferredHeight = 116f;
            config.ShadowAnchorMin = new Vector2(0.20f, 0.16f);
            config.ShadowAnchorMax = new Vector2(0.80f, 0.88f);
            config.HeaderAnchorMin = new Vector2(0.19f, 0.18f);
            config.HeaderAnchorMax = new Vector2(0.81f, 0.90f);
            config.HeaderPadding = new RectOffset(20, 20, 12, 12);
            config.HeaderSpacing = 3f;
            config.TitleStyle = MiniGameShellTopBarBuilder.CreateTextStyle(
                "Title",
                string.Empty,
                new Color(0.29f, 0.39f, 0.22f, 1f),
                33f,
                FontStyles.Bold,
                38f);
            config.ScoreStyle = MiniGameShellTopBarBuilder.CreateTextStyle(
                "Score",
                string.Empty,
                new Color(0.82f, 0.58f, 0.25f, 1f),
                22f,
                FontStyles.Bold,
                26f);
            return config;
        }

        private void BuildTopSettings(Transform parent)
        {
            var rowObject = CreateRectObject("GenerationSettings", parent);
            var rowLayoutElement = rowObject.AddComponent<LayoutElement>();
            rowLayoutElement.preferredHeight = 70f;
            var rowBackground = rowObject.AddComponent<RoundedRectGraphic>();
            rowBackground.color = new Color(1f, 0.98f, 0.88f, 0.72f);
            rowBackground.CornerRadius = 20f;
            rowBackground.raycastTarget = false;
            var rowLayout = rowObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.padding = new RectOffset(18, 18, 8, 8);
            rowLayout.spacing = 22f;

            difficultyDropdown = CreateDropdownGroup(
                rowObject.transform,
                "Difficulty",
                UiTextCatalog.Get("akari.settings.difficulty"),
                CreateDifficultyOptions(),
                selectedDifficultyOption,
                OnDifficultyOptionChanged);
            gridSizeDropdown = CreateDropdownGroup(
                rowObject.transform,
                "GridSize",
                UiTextCatalog.Get("akari.settings.grid_size"),
                CreateGridSizeOptions(),
                selectedGridSizeOption,
                OnGridSizeOptionChanged);
        }

        private MiniGameDropdown CreateDropdownGroup(
            Transform parent,
            string name,
            string label,
            List<string> options,
            int value,
            Action<int> onValueChanged)
        {
            var groupObject = CreateRectObject(name + "Group", parent);
            var groupLayoutElement = groupObject.AddComponent<LayoutElement>();
            groupLayoutElement.preferredWidth = 294f;
            groupLayoutElement.preferredHeight = 48f;
            var groupLayout = groupObject.AddComponent<HorizontalLayoutGroup>();
            groupLayout.childAlignment = TextAnchor.MiddleCenter;
            groupLayout.childControlWidth = true;
            groupLayout.childControlHeight = true;
            groupLayout.childForceExpandWidth = false;
            groupLayout.childForceExpandHeight = false;
            groupLayout.spacing = 10f;

            var labelText = CreateText(name + "Label", groupObject.transform, 19f, FontStyles.Bold, StatusTextColor, 38f);
            labelText.text = label;
            var labelLayout = labelText.GetComponent<LayoutElement>();
            labelLayout.preferredWidth = 54f;

            var dropdownObject = CreateRectObject(name + "Dropdown", groupObject.transform);
            var dropdown = dropdownObject.AddComponent<MiniGameDropdown>();
            dropdown.Configure(
                options,
                Mathf.Clamp(value, 0, options.Count - 1),
                onValueChanged,
                230f,
                44f,
                40f,
                5,
                StatusTextColor);
            return dropdown;
        }

        private static List<string> CreateDifficultyOptions()
        {
            return new List<string>
            {
                UiTextCatalog.Get("akari.settings.random"),
                UiTextCatalog.Get("akari.difficulty.easy"),
                UiTextCatalog.Get("akari.difficulty.normal"),
                UiTextCatalog.Get("akari.difficulty.hard")
            };
        }

        private static List<string> CreateGridSizeOptions()
        {
            var options = new List<string> { UiTextCatalog.Get("akari.settings.random") };
            for (var size = AkariPuzzleGenerator.MinGridSize; size <= AkariPuzzleGenerator.MaxGridSize; size++)
            {
                options.Add(size + "x" + size);
            }

            return options;
        }

        private void OnDifficultyOptionChanged(int value)
        {
            selectedDifficultyOption = Mathf.Clamp(value, 0, 3);
        }

        private void OnGridSizeOptionChanged(int value)
        {
            selectedGridSizeOption = Mathf.Clamp(value, 0, AkariPuzzleGenerator.MaxGridSize - AkariPuzzleGenerator.MinGridSize + 1);
        }

        private AkariDifficulty ResolveSelectedDifficulty()
        {
            if (selectedDifficultyOption == RandomOptionValue)
            {
                return AkariPuzzleGenerator.ResolveRandomDifficulty(random);
            }

            if (selectedDifficultyOption == 1)
            {
                return AkariDifficulty.Easy;
            }

            if (selectedDifficultyOption == 3)
            {
                return AkariDifficulty.Hard;
            }

            return AkariDifficulty.Normal;
        }

        private int ResolveSelectedGridSize()
        {
            if (selectedGridSizeOption == RandomOptionValue)
            {
                return AkariPuzzleGenerator.ResolveRandomGridSize(random);
            }

            return Mathf.Clamp(
                AkariPuzzleGenerator.MinGridSize + selectedGridSizeOption - 1,
                AkariPuzzleGenerator.MinGridSize,
                AkariPuzzleGenerator.MaxGridSize);
        }

        private static string ResolveDifficultyText(AkariDifficulty difficulty)
        {
            if (difficulty == AkariDifficulty.Easy)
            {
                return UiTextCatalog.Get("akari.difficulty.easy");
            }

            if (difficulty == AkariDifficulty.Hard)
            {
                return UiTextCatalog.Get("akari.difficulty.hard");
            }

            return UiTextCatalog.Get("akari.difficulty.normal");
        }

        private static int DifficultyToOption(AkariDifficulty difficulty)
        {
            if (difficulty == AkariDifficulty.Easy)
            {
                return 1;
            }

            if (difficulty == AkariDifficulty.Hard)
            {
                return 3;
            }

            return 2;
        }

        private void BuildContent()
        {
            var contentRootObject = new GameObject("AkariContent", typeof(RectTransform));
            var contentRoot = contentRootObject.GetComponent<RectTransform>();
            contentRoot.SetParent(Shell.ContentHost, false);
            Stretch(contentRoot, Vector2.zero, Vector2.one, new Vector2(36f, 14f), new Vector2(-36f, -14f));

            var layout = contentRootObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 16f;

            BuildTopSettings(contentRoot);

            var infoPanel = CreateRoundedPanel("InfoPanel", contentRoot, new Color(1f, 0.98f, 0.90f, 0.70f), 28f, 0f);
            infoPanel.gameObject.AddComponent<LayoutElement>().preferredHeight = 146f;
            var infoLayout = infoPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            infoLayout.padding = new RectOffset(20, 20, 14, 12);
            infoLayout.spacing = 6f;
            infoLayout.childAlignment = TextAnchor.MiddleCenter;
            infoLayout.childControlWidth = true;
            infoLayout.childControlHeight = true;
            infoLayout.childForceExpandWidth = true;
            infoLayout.childForceExpandHeight = false;

            bulbLabel = CreateText("BulbStatus", infoPanel.transform, 25f, FontStyles.Bold, StatusTextColor, 34f);
            statusLabel = CreateText("Status", infoPanel.transform, 19f, FontStyles.Normal, StatusTextColor, 68f);
            statusLabel.enableWordWrapping = true;
            statusLabel.enableAutoSizing = true;
            statusLabel.fontSizeMin = 16f;
            statusLabel.fontSizeMax = 19f;
            statusLabel.overflowMode = TextOverflowModes.Overflow;

            var boardOuter = CreateRectObject("BoardOuter", contentRoot);
            boardOuter.AddComponent<LayoutElement>().preferredHeight = 650f;
            var boardOuterRect = boardOuter.GetComponent<RectTransform>();

            var boardShadow = CreateRoundedPanel("BoardShadow", boardOuterRect, BoardShadowColor, 34f, -6f);
            Stretch(boardShadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            boardShadow.rectTransform.sizeDelta = new Vector2(BoardSize + 20f, BoardSize + 20f);

            var boardGraphic = CreateRoundedPanel("BoardPanel", boardOuterRect, BoardPanelColor, 34f, 0f);
            boardPanel = boardGraphic.rectTransform;
            Stretch(boardPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            boardPanel.sizeDelta = new Vector2(BoardSize, BoardSize);

            boardLayout = boardGraphic.gameObject.AddComponent<GridLayoutGroup>();
            boardLayout.childAlignment = TextAnchor.MiddleCenter;
            boardLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            boardLayout.spacing = new Vector2(8f, 8f);
        }

        private void StartPuzzle(int questionNumber)
        {
            currentQuestionNumber = Mathf.Max(1, questionNumber);
            currentPuzzle = AkariPuzzleGenerator.Generate(
                currentQuestionNumber,
                ResolveSelectedGridSize(),
                ResolveSelectedDifficulty(),
                random);
            playerBulbs = new bool[currentPuzzle.Cells.Length];
            moveCount = 0;
            isTransitioning = false;
            RebuildBoardIfNeeded();
            RefreshAll(UiTextCatalog.Get("akari.status.playing"));
        }

        private void RebuildBoardIfNeeded()
        {
            if (currentPuzzle == null)
            {
                return;
            }

            if (cellButtons.Count == currentPuzzle.Cells.Length)
            {
                ConfigureBoardLayout();
                return;
            }

            for (var i = boardPanel.childCount - 1; i >= 0; i--)
            {
                var child = boardPanel.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }

            cellButtons.Clear();
            cellBackgrounds.Clear();
            cellLabels.Clear();
            cellBulbs.Clear();

            for (var i = 0; i < currentPuzzle.Cells.Length; i++)
            {
                CreateCell(i);
            }

            ConfigureBoardLayout();
        }

        private void ConfigureBoardLayout()
        {
            if (currentPuzzle == null)
            {
                return;
            }

            boardLayout.constraintCount = currentPuzzle.GridSize;
            var spacing = ResolveCellSpacing(currentPuzzle.GridSize);
            var cellSize = Mathf.Floor((BoardSize - (spacing * (currentPuzzle.GridSize - 1))) / currentPuzzle.GridSize);
            boardLayout.spacing = new Vector2(spacing, spacing);
            boardLayout.cellSize = new Vector2(cellSize, cellSize);

            var cornerRadius = Mathf.Clamp(cellSize * 0.20f, 6f, 18f);
            for (var i = 0; i < cellBackgrounds.Count; i++)
            {
                if (cellBackgrounds[i] != null)
                {
                    cellBackgrounds[i].CornerRadius = cornerRadius;
                    cellBackgrounds[i].SetAllDirty();
                }
            }
        }

        private void CreateCell(int index)
        {
            var cellObject = new GameObject("Cell" + index, typeof(RectTransform), typeof(Button));
            var cellRect = cellObject.GetComponent<RectTransform>();
            cellRect.SetParent(boardPanel, false);

            var background = cellObject.AddComponent<RoundedRectGraphic>();
            background.CornerRadius = 18f;

            var button = cellObject.GetComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.88f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = Color.white;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var capturedIndex = index;
            button.onClick.AddListener(delegate { HandleCellClicked(capturedIndex); });

            var label = CreateText("Label", cellRect, 34f, FontStyles.Bold, WhiteTextColor, 80f);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 1f), new Vector2(-3f, -1f));
            label.enableWordWrapping = false;

            var bulbObject = CreateRectObject("Bulb", cellRect);
            var bulbRect = bulbObject.GetComponent<RectTransform>();
            Stretch(bulbRect, Vector2.zero, Vector2.one, new Vector2(9f, 7f), new Vector2(-9f, -7f));
            bulbRect.localScale = Vector3.one * 0.65f;
            var bulbGraphic = bulbObject.AddComponent<AkariBulbView>();
            bulbGraphic.Build();
            bulbObject.SetActive(false);

            cellButtons.Add(button);
            cellBackgrounds.Add(background);
            cellLabels.Add(label);
            cellBulbs.Add(bulbGraphic);
        }

        private void HandleCellClicked(int index)
        {
            if (isTransitioning || currentPuzzle == null || playerBulbs == null || currentPuzzle.Cells[index] != AkariCellKind.White)
            {
                return;
            }

            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.88f);
            playerBulbs[index] = !playerBulbs[index];
            moveCount++;
            RefreshAll(UiTextCatalog.Get("akari.status.playing"));
            if (currentEvaluation != null && currentEvaluation.IsSolved)
            {
                CompletePuzzle();
            }
        }

        private void CompletePuzzle()
        {
            isTransitioning = true;
            var gainedScore = CalculatePuzzleScore();
            score += gainedScore;
            completedPuzzleCount++;
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.86f);
            RefreshAll(UiTextCatalog.Format("akari.status.solved", gainedScore));
            ShowCompletedSettlement();
        }

        private int CalculatePuzzleScore()
        {
            if (currentPuzzle == null)
            {
                return 0;
            }

            var baseScore = currentPuzzle.GridSize * currentPuzzle.GridSize * 2;
            var penalty = Mathf.Max(0, moveCount - currentPuzzle.ReferenceSteps) * 2;
            return Mathf.Max(12, baseScore - penalty);
        }

        private void RefreshAll(string status)
        {
            RefreshEvaluation();
            RefreshHud(status);
            RefreshBoard();
        }

        private void RefreshEvaluation()
        {
            currentEvaluation = AkariPuzzleGenerator.Evaluate(currentPuzzle, playerBulbs);
        }

        private void RefreshHud(string status)
        {
            if (titleLabel != null)
            {
                titleLabel.text = UiTextCatalog.Get("game.akari.name");
            }

            if (scoreLabel != null && currentPuzzle != null)
            {
                scoreLabel.text = UiTextCatalog.Format(
                    "akari.hud.status",
                    ResolveDifficultyText(currentPuzzle.Difficulty),
                    currentPuzzle.GridSize,
                    score);
            }

            if (bulbLabel != null && currentPuzzle != null && currentEvaluation != null)
            {
                bulbLabel.text = UiTextCatalog.Format(
                    "akari.hud.bulbs",
                    currentEvaluation.BulbCount,
                    currentPuzzle.ReferenceSteps,
                    moveCount);
            }

            if (statusLabel != null)
            {
                statusLabel.text = status;
            }
        }

        private void RefreshBoard()
        {
            if (currentPuzzle == null || playerBulbs == null || currentEvaluation == null)
            {
                return;
            }

            for (var i = 0; i < cellButtons.Count; i++)
            {
                var kind = currentPuzzle.Cells[i];
                var isWhite = kind == AkariCellKind.White;
                var isBulb = isWhite && playerBulbs[i];
                var isBulbConflict = currentEvaluation.BulbConflicts[i];
                var isNumberConflict = currentEvaluation.NumberConflicts[i];
                var cellColor = ResolveCellColor(i, kind, isBulb, isBulbConflict);

                if (cellBackgrounds[i] != null)
                {
                    cellBackgrounds[i].color = cellColor;
                    cellBackgrounds[i].SetAllDirty();
                }

                if (cellLabels[i] != null)
                {
                    cellLabels[i].fontSize = ResolveCellFontSize(currentPuzzle.GridSize);
                    cellLabels[i].text = ResolveCellText(i, kind, isBulb);
                    cellLabels[i].color = ResolveCellTextColor(kind, isNumberConflict);
                }

                if (cellBulbs[i] != null)
                {
                    cellBulbs[i].gameObject.SetActive(isBulb);
                    cellBulbs[i].SetColor(isBulbConflict ? new Color(0.55f, 0.08f, 0.07f, 1f) : new Color(0.06f, 0.07f, 0.06f, 1f));
                }

                if (cellButtons[i] != null)
                {
                    cellButtons[i].interactable = !isTransitioning && isWhite;
                }
            }
        }

        private Color ResolveCellColor(int index, AkariCellKind kind, bool isBulb, bool bulbConflict)
        {
            if (kind != AkariCellKind.White)
            {
                return BlackCellColor;
            }

            if (bulbConflict)
            {
                return ConflictCellColor;
            }

            if (isBulb)
            {
                return BulbCellColor;
            }

            return currentEvaluation.LitCells[index] ? LitCellColor : WhiteCellColor;
        }

        private static Color ResolveCellTextColor(AkariCellKind kind, bool numberConflict)
        {
            if (kind == AkariCellKind.White)
            {
                return WhiteTextColor;
            }

            if (kind == AkariCellKind.NumberedBlack)
            {
                return numberConflict ? ConflictTextColor : SatisfiedTextColor;
            }

            return BlackTextColor;
        }

        private string ResolveCellText(int index, AkariCellKind kind, bool isBulb)
        {
            if (kind == AkariCellKind.White)
            {
                return string.Empty;
            }

            if (kind == AkariCellKind.NumberedBlack)
            {
                return currentPuzzle.Numbers[index].ToString();
            }

            return string.Empty;
        }

        private static float ResolveCellSpacing(int gridSize)
        {
            if (gridSize <= 7)
            {
                return 8f;
            }

            if (gridSize <= 10)
            {
                return 5f;
            }

            return 3f;
        }

        private static float ResolveCellFontSize(int gridSize)
        {
            var spacing = ResolveCellSpacing(gridSize);
            var cellSize = Mathf.Floor((BoardSize - (spacing * (gridSize - 1))) / gridSize);
            return Mathf.Clamp(cellSize * 0.42f, 14f, 34f);
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
        }

        private void ConfirmExitToHall()
        {
            Shell.ClosePopup();
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            var settlement = CreateSettlement();
            ShowBackHallRewardSettlementPanel(
                settlement,
                "AkariSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("akari.settlement.score"), settlement.Score.ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("akari.settlement.completed"), completedPuzzleCount.ToString()),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private void OnRestartClicked()
        {
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            StartPuzzle(Mathf.Max(1, currentQuestionNumber));
        }

        private void ShowCompletedSettlement()
        {
            var settlement = CreateSettlement();
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "AkariSettlementPanel",
                    Style = MiniGameRewardSettlementPanelStyle.Success,
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.Retry,
                    Title = UiTextCatalog.Get("popup.settlement.title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("akari.settlement.score"), settlement.Score.ToString()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("akari.settlement.completed"), completedPuzzleCount.ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                ResetGame,
                delegate { CompleteGame?.Invoke(settlement); },
                true);
        }

        private MiniGameSettlement CreateSettlement()
        {
            return new MiniGameSettlement
            {
                Score = score,
                CoinCount = score * 2,
                ChestCount = completedPuzzleCount,
                Summary = UiTextCatalog.Format("akari.settlement.summary", completedPuzzleCount, score)
            };
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static RoundedRectGraphic CreateRoundedPanel(string name, Transform parent, Color color, float radius, float yOffset)
        {
            var panelObject = CreateRectObject(name, parent);
            var graphic = panelObject.AddComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = radius;
            graphic.raycastTarget = false;
            var rect = graphic.rectTransform;
            rect.anchoredPosition = new Vector2(0f, yOffset);
            return graphic;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            float fontSize,
            FontStyles fontStyle,
            Color color,
            float preferredHeight)
        {
            var textObject = CreateRectObject(name, parent);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            var font = MiniGameFontProvider.DefaultFont;
            if (font != null)
            {
                text.font = font;
            }

            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.enableWordWrapping = false;

            var layout = textObject.AddComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
            return text;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        internal AkariPuzzle CurrentPuzzleForTests
        {
            get { return currentPuzzle; }
        }

        internal int ScoreForTests
        {
            get { return score; }
        }

        internal int CompletedPuzzleCountForTests
        {
            get { return completedPuzzleCount; }
        }

        internal AkariEvaluation CurrentEvaluationForTests
        {
            get { return currentEvaluation; }
        }

        internal bool HasGenerationDropdownsForTests
        {
            get { return difficultyDropdown != null && gridSizeDropdown != null; }
        }

        internal void SelectGenerationOptionsForTests(int gridSize, AkariDifficulty? difficulty)
        {
            selectedGridSizeOption = gridSize <= 0
                ? RandomOptionValue
                : Mathf.Clamp(gridSize - AkariPuzzleGenerator.MinGridSize + 1, 1, AkariPuzzleGenerator.MaxGridSize - AkariPuzzleGenerator.MinGridSize + 1);
            selectedDifficultyOption = difficulty.HasValue
                ? DifficultyToOption(difficulty.Value)
                : RandomOptionValue;

            if (gridSizeDropdown != null)
            {
                gridSizeDropdown.SetValueWithoutNotify(selectedGridSizeOption);
            }

            if (difficultyDropdown != null)
            {
                difficultyDropdown.SetValueWithoutNotify(selectedDifficultyOption);
            }
        }

        internal void StartPuzzleForTests(int questionNumber)
        {
            StartPuzzle(questionNumber);
        }

        internal void LoadPuzzleForTests(AkariPuzzle puzzle, bool[] bulbs)
        {
            currentPuzzle = puzzle;
            currentQuestionNumber = puzzle != null ? puzzle.QuestionNumber : 1;
            playerBulbs = bulbs != null ? bulbs : new bool[puzzle != null && puzzle.Cells != null ? puzzle.Cells.Length : 0];
            moveCount = 0;
            isTransitioning = false;
            RebuildBoardIfNeeded();
            RefreshAll(string.Empty);
        }

        internal Color GetCellBackgroundColorForTests(int index)
        {
            return cellBackgrounds[index].color;
        }

        internal Color GetCellLabelColorForTests(int index)
        {
            return cellLabels[index].color;
        }

        internal void ApplyGeneratedSolutionForTests()
        {
            if (currentPuzzle == null || currentPuzzle.SolutionBulbs == null)
            {
                return;
            }

            for (var i = 0; i < currentPuzzle.SolutionBulbs.Length; i++)
            {
                if (currentPuzzle.SolutionBulbs[i])
                {
                    HandleCellClicked(i);
                    if (isTransitioning)
                    {
                        break;
                    }
                }
            }
        }

        private sealed class AkariBulbView : MonoBehaviour
        {
            private AkariBulbSilhouetteGraphic bodyOutline;
            private AkariBulbSilhouetteGraphic bodyFill;
            private RoundedRectGraphic shine;
            private RoundedRectGraphic baseTop;
            private RoundedRectGraphic baseMiddle;
            private RoundedRectGraphic baseBottom;

            public void Build()
            {
                if (bodyOutline != null)
                {
                    return;
                }

                var root = transform;
                bodyOutline = CreatePart<AkariBulbSilhouetteGraphic>("BodyOutline", root, Vector2.zero, Vector2.one);
                bodyFill = CreatePart<AkariBulbSilhouetteGraphic>("BodyFill", root, new Vector2(0.07f, 0.07f), new Vector2(0.93f, 0.94f));
                baseTop = CreateRoundedPart("BaseTopRib", root, new Vector2(0.38f, 0.26f), new Vector2(0.62f, 0.30f), 3f);
                baseMiddle = CreateRoundedPart("BaseMiddleRib", root, new Vector2(0.35f, 0.17f), new Vector2(0.65f, 0.22f), 3f);
                baseBottom = CreateRoundedPart("BaseBottomRib", root, new Vector2(0.38f, 0.08f), new Vector2(0.62f, 0.13f), 3f);
                shine = CreateRoundedPart("Shine", root, new Vector2(0.35f, 0.62f), new Vector2(0.47f, 0.79f), 8f);
                shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
                SetColor(new Color(0.06f, 0.07f, 0.06f, 1f));
            }

            public void SetColor(Color color)
            {
                if (bodyOutline == null)
                {
                    return;
                }

                var detailColor = color.r > 0.3f && color.g < 0.2f
                    ? new Color(0.48f, 0.07f, 0.06f, 1f)
                    : new Color(0.08f, 0.09f, 0.08f, 1f);
                var shineColor = new Color(1f, 1f, 1f, 0.34f);

                bodyOutline.color = detailColor;
                bodyFill.color = color.r > 0.3f && color.g < 0.2f
                    ? new Color(1f, 0.88f, 0.82f, 1f)
                    : Color.white;
                baseTop.color = detailColor;
                baseMiddle.color = detailColor;
                baseBottom.color = detailColor;
                shine.color = shineColor;

                bodyOutline.SetAllDirty();
                bodyFill.SetAllDirty();
                baseTop.SetAllDirty();
                baseMiddle.SetAllDirty();
                baseBottom.SetAllDirty();
                shine.SetAllDirty();
            }

            private static RoundedRectGraphic CreateRoundedPart(
                string name,
                Transform parent,
                Vector2 anchorMin,
                Vector2 anchorMax,
                float radius)
            {
                var graphic = CreatePart<RoundedRectGraphic>(name, parent, anchorMin, anchorMax);
                graphic.CornerRadius = radius;
                return graphic;
            }

            private static T CreatePart<T>(
                string name,
                Transform parent,
                Vector2 anchorMin,
                Vector2 anchorMax)
                where T : MaskableGraphic
            {
                var gameObject = new GameObject(name, typeof(RectTransform));
                gameObject.transform.SetParent(parent, false);
                var rect = gameObject.GetComponent<RectTransform>();
                Stretch(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
                var graphic = gameObject.AddComponent<T>();
                graphic.raycastTarget = false;
                return graphic;
            }
        }

        [RequireComponent(typeof(CanvasRenderer))]
        private sealed class AkariBulbSilhouetteGraphic : MaskableGraphic
        {
            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();

                var rect = rectTransform.rect;
                var width = rect.width;
                var height = rect.height;
                if (width <= 0.01f || height <= 0.01f)
                {
                    return;
                }

                var center = AddVertex(vh, Vector2.zero);
                var points = new List<Vector2>();
                AddPoint(points, rect, 0.34f, 0.08f);
                AddPoint(points, rect, 0.66f, 0.08f);
                AddPoint(points, rect, 0.69f, 0.17f);
                AddPoint(points, rect, 0.68f, 0.30f);
                AddPoint(points, rect, 0.62f, 0.39f);
                AddPoint(points, rect, 0.59f, 0.45f);
                AddArc(points, rect, new Vector2(0.50f, 0.62f), new Vector2(0.36f, 0.40f), -67f, 247f, 30);
                AddPoint(points, rect, 0.41f, 0.45f);
                AddPoint(points, rect, 0.38f, 0.39f);
                AddPoint(points, rect, 0.32f, 0.30f);
                AddPoint(points, rect, 0.31f, 0.17f);

                var indices = new int[points.Count];
                for (var i = 0; i < points.Count; i++)
                {
                    indices[i] = AddVertex(vh, points[i]);
                }

                for (var i = 0; i < indices.Length; i++)
                {
                    vh.AddTriangle(center, indices[i], indices[(i + 1) % indices.Length]);
                }
            }

            private static void AddArc(
                List<Vector2> points,
                Rect rect,
                Vector2 center,
                Vector2 radius,
                float startDegrees,
                float endDegrees,
                int steps)
            {
                for (var i = 0; i <= steps; i++)
                {
                    var t = i / (float)steps;
                    var angle = Mathf.Lerp(startDegrees, endDegrees, t) * Mathf.Deg2Rad;
                    var x = center.x + Mathf.Cos(angle) * radius.x;
                    var y = center.y + Mathf.Sin(angle) * radius.y;
                    AddPoint(points, rect, x, y);
                }
            }

            private static void AddPoint(List<Vector2> points, Rect rect, float normalizedX, float normalizedY)
            {
                points.Add(new Vector2(
                    rect.xMin + (rect.width * normalizedX),
                    rect.yMin + (rect.height * normalizedY)));
            }

            private int AddVertex(VertexHelper vh, Vector2 position)
            {
                var vertex = UIVertex.simpleVert;
                vertex.color = color;
                vertex.position = position;
                vh.AddVert(vertex);
                return vh.currentVertCount - 1;
            }
        }
    }
}
