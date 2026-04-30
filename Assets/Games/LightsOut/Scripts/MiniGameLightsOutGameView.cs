using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 数学电灯益智：点击灯泡触发十字翻转，使亮灯数字之和等于算式答案。
    /// </summary>
    public sealed class MiniGameLightsOutGameView : MiniGameBase
    {
        public const string GameIdConstant = "lightsout";

        private static readonly Color PanelColor = new Color(1f, 0.98f, 0.9f, 0.78f);
        private static readonly Color PanelShadowColor = new Color(0.28f, 0.36f, 0.21f, 0.12f);
        private static readonly Color LitCellColor = new Color(1f, 0.82f, 0.28f, 1f);
        private static readonly Color DimCellColor = new Color(0.35f, 0.48f, 0.39f, 1f);
        private static readonly Color LitTextColor = new Color(0.33f, 0.23f, 0.07f, 1f);
        private static readonly Color DimTextColor = new Color(0.9f, 0.96f, 0.88f, 1f);
        private static readonly Color QuestionTextColor = new Color(0.24f, 0.3f, 0.18f, 1f);
        private static readonly Color StatusTextColor = new Color(0.52f, 0.38f, 0.18f, 1f);

        private readonly List<Button> cellButtons = new List<Button>();
        private readonly List<RoundedRectGraphic> cellBackgrounds = new List<RoundedRectGraphic>();
        private readonly List<TextMeshProUGUI> cellLabels = new List<TextMeshProUGUI>();

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private TextMeshProUGUI questionLabel;
        private TextMeshProUGUI sumLabel;
        private TextMeshProUGUI stepLabel;
        private TextMeshProUGUI statusLabel;
        private Button restartButton;
        private RectTransform boardPanel;
        private GridLayoutGroup boardLayout;

        private System.Random random;
        private LightsOutPuzzle currentPuzzle;
        private bool[] currentLights;
        private Coroutine nextPuzzleCoroutine;
        private int score;
        private int completedQuestionCount;
        private int moveCount;
        private int pendingPuzzleRewardScore;
        private int pendingPuzzleRewardChestCount;
        private bool isTransitioning;

        public MiniGameLightsOutGameView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "MiniGameLightsOutView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        protected override void BuildOrBindSections()
        {
            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("LightsOutHeader"));
            titleLabel = topBarRefs.TitleText;
            scoreLabel = topBarRefs.ScoreText;

            BuildContent();

            var bottomContainerRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("LightsOutActions"));
            restartButton = MiniGameShellBottomBarBuilder.CreateRestartButton(bottomContainerRefs.ActionBar).Button;

            if (restartButton != null)
            {
                restartButton.gameObject.name = "RestartButton";
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (titleLabel == null ||
                scoreLabel == null ||
                questionLabel == null ||
                sumLabel == null ||
                stepLabel == null ||
                statusLabel == null ||
                restartButton == null ||
                boardPanel == null ||
                boardLayout == null)
            {
                throw new InvalidOperationException("LightsOut prefab structure is incomplete.");
            }
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            StopNextPuzzleCoroutine();
            random = new System.Random(unchecked(Environment.TickCount * 397) ^ DateTime.Now.Millisecond);
            score = 0;
            completedQuestionCount = 0;
            pendingPuzzleRewardScore = 0;
            pendingPuzzleRewardChestCount = 0;
            StartRandomPuzzle();
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.lightsout.help", null);
        }

        protected override void OnPauseRequested()
        {
            Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        protected override void OnBeforeDispose()
        {
            StopNextPuzzleCoroutine();
            Shell.ClosePopup();

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            for (var i = 0; i < cellButtons.Count; i++)
            {
                if (cellButtons[i] != null)
                {
                    cellButtons[i].onClick.RemoveAllListeners();
                }
            }
        }

        private void BuildContent()
        {
            var contentRootObject = new GameObject("LightsOutContent", typeof(RectTransform));
            var contentRoot = contentRootObject.GetComponent<RectTransform>();
            contentRoot.SetParent(Shell.ContentHost, false);
            Stretch(contentRoot, Vector2.zero, Vector2.one, new Vector2(38f, 16f), new Vector2(-38f, -14f));

            var layout = contentRootObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 18f;

            var questionPanel = CreateRoundedPanel("QuestionPanel", contentRoot, PanelColor, 30f, 0f);
            questionPanel.gameObject.AddComponent<LayoutElement>().preferredHeight = 178f;
            var questionLayout = questionPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            questionLayout.padding = new RectOffset(24, 24, 18, 16);
            questionLayout.spacing = 8f;
            questionLayout.childAlignment = TextAnchor.MiddleCenter;
            questionLayout.childControlWidth = true;
            questionLayout.childControlHeight = true;
            questionLayout.childForceExpandWidth = true;
            questionLayout.childForceExpandHeight = false;

            questionLabel = CreateText("Question", questionPanel.transform, 36f, FontStyles.Bold, QuestionTextColor, 48f);
            sumLabel = CreateText("CurrentSum", questionPanel.transform, 26f, FontStyles.Bold, StatusTextColor, 34f);
            stepLabel = CreateText("Steps", questionPanel.transform, 22f, FontStyles.Normal, QuestionTextColor, 30f);

            var boardOuter = CreateRectObject("BoardOuter", contentRoot);
            var boardOuterRect = boardOuter.GetComponent<RectTransform>();
            boardOuter.AddComponent<LayoutElement>().preferredHeight = 612f;

            var boardShadow = CreateRoundedPanel("BoardShadow", boardOuterRect, PanelShadowColor, 34f, -5f);
            Stretch(boardShadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            boardShadow.rectTransform.sizeDelta = new Vector2(612f, 612f);

            var boardGraphic = CreateRoundedPanel("BoardPanel", boardOuterRect, new Color(0.95f, 0.99f, 0.86f, 0.82f), 34f, 0f);
            boardPanel = boardGraphic.rectTransform;
            Stretch(boardPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            boardPanel.sizeDelta = new Vector2(592f, 592f);

            boardLayout = boardGraphic.gameObject.AddComponent<GridLayoutGroup>();
            boardLayout.childAlignment = TextAnchor.MiddleCenter;
            boardLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            boardLayout.spacing = new Vector2(12f, 12f);

            statusLabel = CreateText("Status", contentRoot, 24f, FontStyles.Bold, StatusTextColor, 46f);
        }

        private void StartPuzzle(int questionNumber)
        {
            StopNextPuzzleCoroutine();
            currentPuzzle = LightsOutPuzzleGenerator.Generate(questionNumber, random);
            currentLights = LightsOutPuzzleGenerator.CopyLights(currentPuzzle.InitialLights);
            moveCount = 0;
            isTransitioning = false;
            RebuildBoardIfNeeded();
            RefreshHud();
            RefreshBoard();
            RefreshStatus(UiTextCatalog.Get("lightsout.status.playing"));
        }

        private void StartRandomPuzzle()
        {
            StartPuzzle(RollRandomQuestionNumber());
        }

        private int RollRandomQuestionNumber()
        {
            var gridSize = random.Next(3, 6);
            switch (gridSize)
            {
                case 3:
                    return random.Next(1, 5);
                case 4:
                    return random.Next(5, 13);
                default:
                    return random.Next(13, 25);
            }
        }

        private void RebuildBoardIfNeeded()
        {
            if (currentPuzzle == null || cellButtons.Count == currentPuzzle.Numbers.Length)
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

            for (var i = 0; i < currentPuzzle.Numbers.Length; i++)
            {
                CreateCell(i);
            }

            ConfigureBoardLayout();
        }

        private void ConfigureBoardLayout()
        {
            if (currentPuzzle == null || boardLayout == null)
            {
                return;
            }

            boardLayout.constraintCount = currentPuzzle.GridSize;
            var cellSize = currentPuzzle.GridSize == 3 ? 134f : currentPuzzle.GridSize == 4 ? 112f : 92f;
            boardLayout.cellSize = new Vector2(cellSize, cellSize);
        }

        private void CreateCell(int index)
        {
            var cellObject = new GameObject("Cell" + index, typeof(RectTransform), typeof(Button));
            var cellRect = cellObject.GetComponent<RectTransform>();
            cellRect.SetParent(boardPanel, false);
            cellRect.sizeDelta = new Vector2(96f, 96f);

            var background = cellObject.AddComponent<RoundedRectGraphic>();
            background.CornerRadius = 24f;

            var button = cellObject.GetComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.65f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var capturedIndex = index;
            button.onClick.AddListener(delegate { HandleCellClicked(capturedIndex); });

            var label = CreateText("Number", cellRect, 36f, FontStyles.Bold, LitTextColor, 92f);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.enableWordWrapping = false;

            cellButtons.Add(button);
            cellBackgrounds.Add(background);
            cellLabels.Add(label);
        }

        private void HandleCellClicked(int index)
        {
            if (isTransitioning || currentPuzzle == null || currentLights == null)
            {
                return;
            }

            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.9f);
            LightsOutPuzzleGenerator.ToggleCross(currentLights, currentPuzzle.GridSize, index);
            moveCount++;
            RefreshHud();
            RefreshBoard();

            if (LightsOutPuzzleGenerator.SumLit(currentPuzzle.Numbers, currentLights) == currentPuzzle.TargetAnswer)
            {
                CompletePuzzle();
            }
            else
            {
                RefreshStatus(UiTextCatalog.Get("lightsout.status.playing"));
            }
        }

        private void CompletePuzzle()
        {
            isTransitioning = true;
            var gainedScore = CalculatePuzzleScore();
            score += gainedScore;
            completedQuestionCount++;
            pendingPuzzleRewardScore = gainedScore;
            pendingPuzzleRewardChestCount = 1;
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.85f);
            RefreshHud();
            RefreshBoard();
            RefreshStatus(UiTextCatalog.Format("lightsout.status.correct", gainedScore));
            nextPuzzleCoroutine = HostBehaviour.StartCoroutine(ContinueAfterPuzzleDelay());
        }

        private IEnumerator ContinueAfterPuzzleDelay()
        {
            yield return new WaitForSeconds(0.85f);
            nextPuzzleCoroutine = null;
            ShowPuzzleSettlement();
        }

        private void ShowPuzzleSettlement()
        {
            var settlement = CreateSettlement();
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "LightsOutSettlementPanel",
                    Style = MiniGameRewardSettlementPanelStyle.Success,
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.Continue,
                    Title = UiTextCatalog.Get("popup.settlement.title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("lightsout.settlement.score"), settlement.Score.ToString()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("lightsout.settlement.completed"), completedQuestionCount.ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                ContinueAfterClaimingPuzzleReward,
                delegate { CompleteGame?.Invoke(settlement); },
                true);
        }

        private void ContinueAfterClaimingPuzzleReward()
        {
            pendingPuzzleRewardScore = 0;
            pendingPuzzleRewardChestCount = 0;
            StartRandomPuzzle();
        }

        private int CalculatePuzzleScore()
        {
            if (currentPuzzle == null)
            {
                return 0;
            }

            if (moveCount <= currentPuzzle.ReferenceSteps)
            {
                return 15;
            }

            if (moveCount <= currentPuzzle.ReferenceSteps + 3)
            {
                return 12;
            }

            return 10;
        }

        private void RefreshHud()
        {
            if (titleLabel != null)
            {
                titleLabel.text = UiTextCatalog.Get("game.lightsout.name");
            }

            if (scoreLabel != null)
            {
                scoreLabel.text = UiTextCatalog.Format("lightsout.hud.score_questions", score);
            }

            if (currentPuzzle == null)
            {
                return;
            }

            if (questionLabel != null)
            {
                questionLabel.text = UiTextCatalog.Format("lightsout.question.prompt", currentPuzzle.Expression);
            }

            if (sumLabel != null)
            {
                sumLabel.text = UiTextCatalog.Format(
                    "lightsout.hud.current_sum",
                    LightsOutPuzzleGenerator.SumLit(currentPuzzle.Numbers, currentLights));
            }

            if (stepLabel != null)
            {
                stepLabel.text = UiTextCatalog.Format("lightsout.hud.steps", moveCount, currentPuzzle.ReferenceSteps);
            }
        }

        private void RefreshBoard()
        {
            if (currentPuzzle == null || currentLights == null)
            {
                return;
            }

            for (var i = 0; i < cellButtons.Count; i++)
            {
                var lit = currentLights[i];
                if (cellBackgrounds[i] != null)
                {
                    cellBackgrounds[i].color = lit ? LitCellColor : DimCellColor;
                    cellBackgrounds[i].SetAllDirty();
                }

                if (cellLabels[i] != null)
                {
                    cellLabels[i].text = currentPuzzle.Numbers[i].ToString();
                    cellLabels[i].color = lit ? LitTextColor : DimTextColor;
                }

                if (cellButtons[i] != null)
                {
                    cellButtons[i].interactable = !isTransitioning;
                }
            }
        }

        private void RefreshStatus(string text)
        {
            if (statusLabel != null)
            {
                statusLabel.text = text;
            }
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
                "LightsOutSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("lightsout.settlement.score"), settlement.Score.ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("lightsout.settlement.completed"), completedQuestionCount.ToString()),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private void OnRestartClicked()
        {
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            ResetGame();
        }

        private MiniGameSettlement CreateSettlement()
        {
            return new MiniGameSettlement
            {
                Score = score,
                CoinCount = pendingPuzzleRewardScore * 2,
                ChestCount = pendingPuzzleRewardChestCount,
                Summary = UiTextCatalog.Format("lightsout.settlement.summary", score, completedQuestionCount)
            };
        }

        private void StopNextPuzzleCoroutine()
        {
            if (nextPuzzleCoroutine != null)
            {
                HostBehaviour.StopCoroutine(nextPuzzleCoroutine);
                nextPuzzleCoroutine = null;
            }
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

        internal LightsOutPuzzle CurrentPuzzleForTests
        {
            get { return currentPuzzle; }
        }

        internal int ScoreForTests
        {
            get { return score; }
        }

        internal int CompletedQuestionCountForTests
        {
            get { return completedQuestionCount; }
        }

        internal string HudScoreTextForTests
        {
            get { return scoreLabel != null ? scoreLabel.text : string.Empty; }
        }

        internal void ApplyGeneratedSolutionForTests()
        {
            if (currentPuzzle == null || currentPuzzle.SolutionClickIndices == null)
            {
                return;
            }

            var solution = new int[currentPuzzle.SolutionClickIndices.Length];
            Array.Copy(currentPuzzle.SolutionClickIndices, solution, solution.Length);
            for (var i = 0; i < solution.Length; i++)
            {
                HandleCellClicked(solution[i]);
                if (isTransitioning)
                {
                    break;
                }
            }
        }
    }
}
