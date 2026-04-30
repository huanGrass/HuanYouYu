using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed class GameMinesweeperView : MiniGameBase
    {
        public const string GameIdConstant = "minesweeper";

        private const int BoardWidth = 9;
        private const int BoardHeight = 9;
        private const int TotalMineCount = 10;
        private const int TotalSafeCellCount = (BoardWidth * BoardHeight) - TotalMineCount;
        private const float BoardPadding = 24f;
        private const float BoardPanelSize = 684f;
        private const float BoardGridInset = 18f;
        private const float CellSize = 68f;
        private const float CellSpacing = 4f;

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private Button restartButton;
        private Button modeButton;
        private TextMeshProUGUI modeButtonLabel;
        private RectTransform boardRoot;
        private RectTransform boardGrid;
        private CellState[,] cells;
        private MinesweeperCellView[,] cellViews;
        private int score;
        private int revealedSafeCellCount;
        private int flaggedCellCount;
        private int pendingSessionScore;
        private int pendingSessionCoinCount;
        private int pendingSessionChestCount;
        private bool isBoardGenerated;
        private bool isGameOver;
        private int explodedMineX = -1;
        private int explodedMineY = -1;
        private InteractionMode interactionMode;

        private enum InteractionMode
        {
            Reveal,
            Flag
        }

        private sealed class CellState
        {
            public bool HasMine;
            public bool IsRevealed;
            public bool IsFlagged;
            public int AdjacentMineCount;
        }

        public GameMinesweeperView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "GameMinesweeperView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        protected override void BuildOrBindSections()
        {
            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("MinesweeperTop"));
            var bottomRoot = CreateBottomRoot();

            titleLabel = topBarRefs.TitleText;
            scoreLabel = topBarRefs.ScoreText;
            restartButton = bottomRoot.transform.Find("ActionBar/RestartButton")?.GetComponent<Button>();
            var modeButtonHost = bottomRoot.transform.Find("ActionBar/ModeButtonHost") as RectTransform;
            if (titleLabel == null || scoreLabel == null || restartButton == null || modeButtonHost == null)
            {
                throw new InvalidOperationException("Minesweeper prefab structure is incomplete.");
            }

            modeButton = CreateModeButton(modeButtonHost, titleLabel.font, out modeButtonLabel);
            BuildBoardArea(titleLabel.font);

            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(OnRestartClicked);
            MiniGameSfxPlayer.Attach(restartButton, MiniGameSfxType.UiTap, 0.95f);

            modeButton.onClick.RemoveAllListeners();
            modeButton.onClick.AddListener(OnModeButtonClicked);
            MiniGameSfxPlayer.Attach(modeButton, MiniGameSfxType.UiTap, 0.95f);

        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            score = 0;
            revealedSafeCellCount = 0;
            flaggedCellCount = 0;
            isBoardGenerated = false;
            isGameOver = false;
            explodedMineX = -1;
            explodedMineY = -1;
            interactionMode = InteractionMode.Reveal;

            EnsureBoardCreated();
            ClearBoardState();
            RefreshBoard();
            RefreshHud();
            RefreshModeButton();
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.minesweeper.help", null);
        }

        protected override void OnPauseRequested()
        {
            if (isGameOver)
            {
                return;
            }

            Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        private void EnsureBoardCreated()
        {
            if (cells != null && cellViews != null)
            {
                return;
            }

            cells = new CellState[BoardWidth, BoardHeight];
            cellViews = new MinesweeperCellView[BoardWidth, BoardHeight];

            for (var y = 0; y < BoardHeight; y++)
            {
                for (var x = 0; x < BoardWidth; x++)
                {
                    cells[x, y] = new CellState();
                    cellViews[x, y] = MinesweeperCellView.Create(boardGrid, x, y, titleLabel.font, HandleCellPressed);
                }
            }
        }

        private void ClearBoardState()
        {
            for (var y = 0; y < BoardHeight; y++)
            {
                for (var x = 0; x < BoardWidth; x++)
                {
                    var cell = cells[x, y];
                    cell.HasMine = false;
                    cell.IsRevealed = false;
                    cell.IsFlagged = false;
                    cell.AdjacentMineCount = 0;
                }
            }
        }

        private void BuildBoardArea(TMP_FontAsset fontAsset)
        {
            var rootObject = new GameObject("BoardRoot", typeof(RectTransform));
            boardRoot = rootObject.GetComponent<RectTransform>();
            Shell.AttachContent(boardRoot);
            boardRoot.anchorMin = Vector2.zero;
            boardRoot.anchorMax = Vector2.one;
            boardRoot.offsetMin = new Vector2(BoardPadding, 18f);
            boardRoot.offsetMax = new Vector2(-BoardPadding, -18f);

            var panelObject = new GameObject("BoardPanel", typeof(RectTransform), typeof(Image));
            var panelTransform = panelObject.GetComponent<RectTransform>();
            panelTransform.SetParent(boardRoot, false);
            panelTransform.anchorMin = new Vector2(0.5f, 0.5f);
            panelTransform.anchorMax = new Vector2(0.5f, 0.5f);
            panelTransform.sizeDelta = new Vector2(BoardPanelSize, BoardPanelSize);
            panelTransform.anchoredPosition = Vector2.zero;

            var panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.97f, 0.93f, 0.82f, 0.98f);

            var gridObject = new GameObject("BoardGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            boardGrid = gridObject.GetComponent<RectTransform>();
            boardGrid.SetParent(panelTransform, false);
            boardGrid.anchorMin = Vector2.zero;
            boardGrid.anchorMax = Vector2.one;
            boardGrid.offsetMin = new Vector2(BoardGridInset, BoardGridInset);
            boardGrid.offsetMax = new Vector2(-BoardGridInset, -BoardGridInset);

            var gridLayout = gridObject.GetComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(CellSize, CellSize);
            gridLayout.spacing = new Vector2(CellSpacing, CellSpacing);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = BoardWidth;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.childAlignment = TextAnchor.UpperLeft;

            var hintObject = new GameObject("BoardHint", typeof(RectTransform), typeof(TextMeshProUGUI));
            var hintTransform = hintObject.GetComponent<RectTransform>();
            hintTransform.SetParent(panelTransform, false);
            hintTransform.anchorMin = new Vector2(0.5f, 0f);
            hintTransform.anchorMax = new Vector2(0.5f, 0f);
            hintTransform.anchoredPosition = new Vector2(0f, -34f);
            hintTransform.sizeDelta = new Vector2(BoardPanelSize, 32f);

            var hintText = hintObject.GetComponent<TextMeshProUGUI>();
            hintText.font = fontAsset;
            hintText.fontSize = 22f;
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.color = new Color(0.39f, 0.46f, 0.25f, 0.92f);
            hintText.text = UiTextCatalog.Get("minesweeper.board.hint");
        }

        private void RefreshHud()
        {
            if (titleLabel != null)
            {
                titleLabel.text = UiTextCatalog.Get("game.minesweeper.name");
            }

            if (scoreLabel != null)
            {
                scoreLabel.text = BuildRemainingMineText();
            }
        }

        private void RefreshModeButton()
        {
            if (modeButtonLabel != null)
            {
                modeButtonLabel.text = FormatText("minesweeper.mode.button", GetModeLabel(interactionMode));
            }
        }

        private void RefreshBoard()
        {
            for (var y = 0; y < BoardHeight; y++)
            {
                for (var x = 0; x < BoardWidth; x++)
                {
                    RefreshCellView(x, y);
                }
            }
        }

        private void RefreshCellView(int x, int y)
        {
            var cell = cells[x, y];
            var view = cellViews[x, y];
            if (view == null)
            {
                return;
            }

            if (cell.HasMine && (cell.IsRevealed || isGameOver))
            {
                view.RenderMine(x == explodedMineX && y == explodedMineY);
            }
            else if (cell.IsRevealed)
            {
                view.RenderRevealed(cell.AdjacentMineCount);
            }
            else if (cell.IsFlagged)
            {
                view.RenderFlag();
            }
            else
            {
                view.RenderCovered();
            }

            view.SetInteractable(!isGameOver && !cell.IsRevealed);
        }

        private void HandleCellPressed(int x, int y)
        {
            if (isGameOver || !IsInsideBoard(x, y))
            {
                return;
            }

            var cell = cells[x, y];
            if (interactionMode == InteractionMode.Flag)
            {
                if (!cell.IsRevealed)
                {
                    ToggleFlag(cell);
                    RefreshCellView(x, y);
                    RefreshHud();
                }

                return;
            }

            if (cell.IsFlagged || cell.IsRevealed)
            {
                return;
            }

            if (!isBoardGenerated)
            {
                GenerateMines(x, y);
            }

            RevealFrom(x, y);

            if (!isGameOver && revealedSafeCellCount >= TotalSafeCellCount)
            {
                HandleGameWon();
            }
        }

        private void ToggleFlag(CellState cell)
        {
            cell.IsFlagged = !cell.IsFlagged;
            flaggedCellCount += cell.IsFlagged ? 1 : -1;
        }

        private void GenerateMines(int safeX, int safeY)
        {
            var candidates = new List<int>(BoardWidth * BoardHeight - 1);
            for (var y = 0; y < BoardHeight; y++)
            {
                for (var x = 0; x < BoardWidth; x++)
                {
                    if (x == safeX && y == safeY)
                    {
                        continue;
                    }

                    candidates.Add((y * BoardWidth) + x);
                }
            }

            var random = new System.Random(unchecked(Environment.TickCount ^ (safeX * 397) ^ (safeY * 911)));
            for (var i = 0; i < TotalMineCount; i++)
            {
                var selectedIndex = random.Next(i, candidates.Count);
                var selected = candidates[selectedIndex];
                candidates[selectedIndex] = candidates[i];
                candidates[i] = selected;

                var mineX = selected % BoardWidth;
                var mineY = selected / BoardWidth;
                cells[mineX, mineY].HasMine = true;
            }

            RecalculateAdjacentMineCounts();
            isBoardGenerated = true;
        }

        private void RecalculateAdjacentMineCounts()
        {
            for (var y = 0; y < BoardHeight; y++)
            {
                for (var x = 0; x < BoardWidth; x++)
                {
                    var cell = cells[x, y];
                    if (cell.HasMine)
                    {
                        cell.AdjacentMineCount = 0;
                        continue;
                    }

                    var mineCount = 0;
                    ForEachNeighbor(x, y, (neighborX, neighborY) =>
                    {
                        if (cells[neighborX, neighborY].HasMine)
                        {
                            mineCount++;
                        }
                    });
                    cell.AdjacentMineCount = mineCount;
                }
            }
        }

        private void RevealFrom(int startX, int startY)
        {
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(startX, startY));

            while (queue.Count > 0)
            {
                var position = queue.Dequeue();
                var x = position.x;
                var y = position.y;

                if (!IsInsideBoard(x, y))
                {
                    continue;
                }

                var cell = cells[x, y];
                if (cell.IsRevealed || cell.IsFlagged)
                {
                    continue;
                }

                cell.IsRevealed = true;
                if (cell.HasMine)
                {
                    HandleMineTriggered(x, y);
                    return;
                }

                revealedSafeCellCount++;
                score = revealedSafeCellCount;
                RefreshCellView(x, y);

                if (cell.AdjacentMineCount == 0)
                {
                    ForEachNeighbor(x, y, (neighborX, neighborY) =>
                    {
                        var neighbor = cells[neighborX, neighborY];
                        if (!neighbor.IsRevealed && !neighbor.IsFlagged)
                        {
                            queue.Enqueue(new Vector2Int(neighborX, neighborY));
                        }
                    });
                }
            }

            RefreshHud();
        }

        private void HandleMineTriggered(int mineX, int mineY)
        {
            isGameOver = true;
            explodedMineX = mineX;
            explodedMineY = mineY;
            var settlement = BuildRoundSettlement(false);
            AccumulateSessionSettlement(settlement);
            RevealAllMines();
            RefreshBoard();
            ShowFailurePopup(settlement);
        }

        private void HandleGameWon()
        {
            isGameOver = true;
            var settlement = BuildRoundSettlement(true);
            AccumulateSessionSettlement(settlement);
            RefreshBoard();
            ShowSettlement(settlement);
        }

        private void RevealAllMines()
        {
            for (var y = 0; y < BoardHeight; y++)
            {
                for (var x = 0; x < BoardWidth; x++)
                {
                    if (cells[x, y].HasMine)
                    {
                        cells[x, y].IsRevealed = true;
                    }
                }
            }
        }

        private void ShowSettlement(MiniGameSettlement settlement)
        {
            ShowRoundSettlementPanel(settlement, MiniGameRewardSettlementPanelStyle.Success);
        }

        private void ShowFailurePopup(MiniGameSettlement settlement)
        {
            ShowRoundSettlementPanel(settlement, MiniGameRewardSettlementPanelStyle.Failure);
        }

        private void ShowRoundSettlementPanel(MiniGameSettlement settlement, MiniGameRewardSettlementPanelStyle style)
        {
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "MinesweeperSettlementPanel",
                    Style = style,
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.Retry,
                    Title = UiTextCatalog.Get(style == MiniGameRewardSettlementPanelStyle.Success ? "minesweeper.settlement.win_title" : "minesweeper.settlement.failure_title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("minesweeper.settlement.safe_cells"), score.ToString()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("minesweeper.settlement.flags"), flaggedCellCount.ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                delegate
                {
                    AccumulateSessionSettlement(settlement);
                    ResetGame();
                },
                delegate
                {
                    AccumulateSessionSettlement(settlement);
                    ExitSessionToHall();
                },
                false);
        }

        private void ExitSessionToHall()
        {
            Shell.ClosePopup();
            CompleteGame?.Invoke(BuildSessionSettlementForExit());
        }

        private void OnRestartClicked()
        {
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            ResetGame();
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
        }

        private void OnModeButtonClicked()
        {
            if (isGameOver)
            {
                return;
            }

            interactionMode = interactionMode == InteractionMode.Reveal ? InteractionMode.Flag : InteractionMode.Reveal;
            RefreshModeButton();
        }

        private void ConfirmExitToHall()
        {
            AccumulateSessionSettlement(BuildCurrentRoundExitSettlement());
            Shell.ClosePopup();
            isGameOver = true;
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.92f);
            var settlement = BuildSessionSettlementForExit();
            ShowBackHallRewardSettlementPanel(
                settlement,
                "MinesweeperSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("minesweeper.settlement.safe_cells"), pendingSessionScore.ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("minesweeper.settlement.flags"), flaggedCellCount.ToString()),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private void AccumulateSessionSettlement(MiniGameSettlement settlement)
        {
            if (settlement == null)
            {
                return;
            }

            pendingSessionScore += Mathf.Max(0, settlement.Score);
            pendingSessionCoinCount += Mathf.Max(0, settlement.CoinCount);
            pendingSessionChestCount += Mathf.Max(0, settlement.ChestCount);
        }

        private MiniGameSettlement BuildRoundSettlement(bool didWin)
        {
            var coinCount = (score * 2) + (didWin ? 20 : 0);
            return new MiniGameSettlement
            {
                Score = score,
                CoinCount = coinCount,
                ChestCount = didWin ? 1 : 0,
                Summary = didWin
                    ? FormatText("minesweeper.settlement.summary.win", score, coinCount, 1)
                    : FormatText("minesweeper.settlement.summary.lose", score, coinCount)
            };
        }

        private MiniGameSettlement BuildCurrentRoundExitSettlement()
        {
            var coinCount = score * 2;
            return new MiniGameSettlement
            {
                Score = score,
                CoinCount = coinCount,
                ChestCount = 0,
                Summary = FormatText("minesweeper.settlement.exit", score, coinCount)
            };
        }

        private MiniGameSettlement BuildSessionSettlementForExit()
        {
            return new MiniGameSettlement
            {
                Score = pendingSessionScore,
                CoinCount = pendingSessionCoinCount,
                ChestCount = pendingSessionChestCount,
                Summary = FormatText(
                    "minesweeper.settlement.summary.exit",
                    pendingSessionScore,
                    pendingSessionCoinCount,
                    pendingSessionChestCount)
            };
        }

        private string BuildRemainingMineText()
        {
            return FormatText("minesweeper.hud.remaining_mines", Mathf.Max(0, TotalMineCount - flaggedCellCount));
        }

        private string GetModeLabel(InteractionMode mode)
        {
            return mode == InteractionMode.Reveal
                ? UiTextCatalog.Get("minesweeper.mode.reveal")
                : UiTextCatalog.Get("minesweeper.mode.flag");
        }

        private static Button CreateModeButton(Transform parent, TMP_FontAsset fontAsset, out TextMeshProUGUI label)
        {
            var buttonObject = new GameObject("ModeButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.92f, 0.82f, 0.58f, 1f);

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelTransform = labelObject.GetComponent<RectTransform>();
            labelTransform.SetParent(rectTransform, false);
            labelTransform.anchorMin = Vector2.zero;
            labelTransform.anchorMax = Vector2.one;
            labelTransform.offsetMin = new Vector2(12f, 8f);
            labelTransform.offsetMax = new Vector2(-12f, -8f);

            label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = fontAsset;
            label.fontSize = 22f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.31f, 0.36f, 0.17f, 1f);
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;

            return button;
        }

        private string FormatText(string key, params object[] args)
        {
            var template = UiTextCatalog.Get(key);
            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        private GameObject CreateBottomRoot()
        {
            var rootObject = new GameObject("MinesweeperBottom", typeof(RectTransform), typeof(LayoutElement));
            var root = rootObject.GetComponent<RectTransform>();
            root.SetParent(Shell.BottomHost, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0.5f);

            rootObject.GetComponent<LayoutElement>().preferredHeight = 144f;

            var actionBarObject = new GameObject("ActionBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            var actionBar = actionBarObject.GetComponent<RectTransform>();
            actionBar.SetParent(root, false);
            actionBar.anchorMin = new Vector2(0.5f, 0.5f);
            actionBar.anchorMax = new Vector2(0.5f, 0.5f);
            actionBar.pivot = new Vector2(0.5f, 0.5f);
            actionBar.anchoredPosition = new Vector2(0f, 4f);
            actionBar.sizeDelta = new Vector2(216f, 88f);

            CreateBarBackground(
                "TrayShadow",
                actionBar,
                new Vector2(26f, 14f),
                -4f,
                new Color(0.31f, 0.42f, 0.26f, 0.10f),
                34f,
                0);

            CreateBarBackground(
                "ActionTray",
                actionBar,
                new Vector2(24f, 12f),
                0f,
                new Color(1f, 0.98f, 0.92f, 0.66f),
                32f,
                1);

            var layout = actionBarObject.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 32f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = actionBarObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            MiniGameShellBottomBarBuilder.CreateRestartButton(actionBar);

            var modeButtonHostObject = new GameObject("ModeButtonHost", typeof(RectTransform), typeof(LayoutElement));
            var modeButtonHost = modeButtonHostObject.GetComponent<RectTransform>();
            modeButtonHost.SetParent(actionBar, false);
            modeButtonHost.sizeDelta = new Vector2(160f, 84f);

            var modeLayout = modeButtonHostObject.GetComponent<LayoutElement>();
            modeLayout.preferredWidth = 160f;
            modeLayout.preferredHeight = 84f;

            return rootObject;
        }

        private static GameObject CreateBarBackground(
            string name,
            Transform parent,
            Vector2 padding,
            float yOffset,
            Color color,
            float cornerRadius,
            int siblingIndex)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic), typeof(LayoutElement));
            var rect = panel.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-padding.x, -padding.y + yOffset);
            rect.offsetMax = new Vector2(padding.x, padding.y + yOffset);
            rect.SetSiblingIndex(siblingIndex);

            var graphic = panel.GetComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = cornerRadius;
            graphic.raycastTarget = false;

            var layout = panel.GetComponent<LayoutElement>();
            layout.ignoreLayout = true;
            return panel;
        }

        private static bool IsInsideBoard(int x, int y)
        {
            return x >= 0 && x < BoardWidth && y >= 0 && y < BoardHeight;
        }

        private static void ForEachNeighbor(int x, int y, Action<int, int> visitor)
        {
            for (var offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (var offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    var neighborX = x + offsetX;
                    var neighborY = y + offsetY;
                    if (IsInsideBoard(neighborX, neighborY))
                    {
                        visitor(neighborX, neighborY);
                    }
                }
            }
        }
    }
}


