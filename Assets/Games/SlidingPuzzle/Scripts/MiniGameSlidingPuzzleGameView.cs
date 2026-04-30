using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 4x4 经典数字华容道运行体。
    /// </summary>
    public sealed class MiniGameSlidingPuzzleGameView : MiniGameBase
    {
        public const string GameIdConstant = "slidingpuzzle";

        private const int BoardSize = 4;
        private const int CellCount = BoardSize * BoardSize;
        private const int MaxScore = 1000;
        private const int MovePenalty = 10;
        private const int MinScore = 100;
        private const int ShuffleMoveCount = 180;

        private static readonly Color BoardShadowColor = new Color(0.21f, 0.29f, 0.19f, 0.18f);
        private static readonly Color BoardColor = new Color(0.98f, 0.94f, 0.84f, 0.96f);
        private static readonly Color EmptyColor = new Color(0.72f, 0.80f, 0.70f, 0.40f);
        private static readonly Color TileColor = new Color(0.33f, 0.58f, 0.45f, 1f);
        private static readonly Color TilePressedColor = new Color(0.25f, 0.46f, 0.36f, 1f);
        private static readonly Color TileDisabledColor = new Color(0.33f, 0.58f, 0.45f, 0.38f);
        private static readonly Color TileLabelColor = Color.white;

        private readonly int[] board = new int[CellCount];
        private readonly RectTransform[] cells = new RectTransform[CellCount];
        private readonly Button[] tileButtons = new Button[CellCount];
        private readonly TextMeshProUGUI[] tileLabels = new TextMeshProUGUI[CellCount];

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private Button restartButton;
        private RectTransform boardRoot;
        private int blankIndex;
        private int moves;
        private bool isCompleted;

        public MiniGameSlidingPuzzleGameView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "MiniGameSlidingPuzzleView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        protected override void BuildOrBindSections()
        {
            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("SlidingPuzzleHeader"));
            titleLabel = topBarRefs.TitleText;
            scoreLabel = topBarRefs.ScoreText;

            BuildBoard(Shell.ContentHost);

            var bottomContainerRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("SlidingPuzzleActions"));
            restartButton = MiniGameShellBottomBarBuilder.CreateRestartButton(bottomContainerRefs.ActionBar).Button;

            if (restartButton != null)
            {
                restartButton.gameObject.name = "RestartButton";
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (titleLabel == null || scoreLabel == null || restartButton == null || boardRoot == null)
            {
                throw new InvalidOperationException("SlidingPuzzle prefab structure is incomplete.");
            }
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            CloseRewardSettlementPanel();
            moves = 0;
            isCompleted = false;
            GenerateShuffledBoard();
            RefreshAll();
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.slidingpuzzle.help", null);
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

            for (var i = 0; i < tileButtons.Length; i++)
            {
                if (tileButtons[i] != null)
                {
                    tileButtons[i].onClick.RemoveAllListeners();
                }
            }
        }

        private void BuildBoard(Transform parent)
        {
            var rootObject = CreateRectObject("SlidingPuzzleBoard", parent);
            boardRoot = rootObject.GetComponent<RectTransform>();
            boardRoot.anchorMin = new Vector2(0.5f, 0.5f);
            boardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            boardRoot.pivot = new Vector2(0.5f, 0.5f);
            boardRoot.sizeDelta = new Vector2(640f, 640f);
            boardRoot.anchoredPosition = new Vector2(0f, 12f);

            var shadow = CreateRoundedRect("BoardShadow", boardRoot, BoardShadowColor, 36f, false);
            Stretch(shadow.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, -18f), new Vector2(10f, -18f));

            var background = CreateRoundedRect("BoardBackground", boardRoot, BoardColor, 34f, false);
            Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var gridObject = CreateRectObject("BoardGrid", boardRoot);
            var gridRect = gridObject.GetComponent<RectTransform>();
            Stretch(gridRect, Vector2.zero, Vector2.one, new Vector2(24f, 24f), new Vector2(-24f, -24f));

            var grid = gridObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = BoardSize;
            grid.cellSize = new Vector2(136f, 136f);
            grid.spacing = new Vector2(16f, 16f);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;

            for (var i = 0; i < CellCount; i++)
            {
                CreateCell(gridRect, i);
            }
        }

        private void CreateCell(Transform parent, int index)
        {
            var cellObject = CreateRectObject("SlidingPuzzleCell_" + index, parent);
            var cellRect = cellObject.GetComponent<RectTransform>();
            cells[index] = cellRect;

            var empty = CreateRoundedRect("EmptySlot", cellRect, EmptyColor, 24f, false);
            Stretch(empty.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var tileObject = CreateRectObject("TileButton", cellRect);
            var tileRect = tileObject.GetComponent<RectTransform>();
            Stretch(tileRect, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            var tileGraphic = CreateRoundedRect("TileFace", tileRect, TileColor, 24f, true);
            Stretch(tileGraphic.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var button = tileObject.AddComponent<Button>();
            button.targetGraphic = tileGraphic;
            ConfigureTileButton(button);
            var capturedIndex = index;
            button.onClick.AddListener(delegate { OnCellClicked(capturedIndex); });
            tileButtons[index] = button;

            var labelObject = CreateRectObject("Label", tileRect);
            var labelRect = labelObject.GetComponent<RectTransform>();
            Stretch(labelRect, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = MiniGameFontProvider.DefaultFont;
            label.fontSize = 52f;
            label.fontStyle = FontStyles.Bold;
            label.color = TileLabelColor;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            tileLabels[index] = label;
        }

        private void GenerateShuffledBoard()
        {
            for (var i = 0; i < CellCount - 1; i++)
            {
                board[i] = i + 1;
            }

            board[CellCount - 1] = 0;
            blankIndex = CellCount - 1;

            var previousBlankIndex = -1;
            for (var i = 0; i < ShuffleMoveCount; i++)
            {
                var neighbors = GetMovableNeighborIndices(blankIndex);
                if (neighbors.Count > 1)
                {
                    neighbors.Remove(previousBlankIndex);
                }

                var nextIndex = neighbors[UnityEngine.Random.Range(0, neighbors.Count)];
                previousBlankIndex = blankIndex;
                Swap(blankIndex, nextIndex);
                blankIndex = nextIndex;
            }

            if (IsSolved())
            {
                var neighbors = GetMovableNeighborIndices(blankIndex);
                var nextIndex = neighbors[0];
                Swap(blankIndex, nextIndex);
                blankIndex = nextIndex;
            }
        }

        private void OnCellClicked(int index)
        {
            if (isCompleted || board[index] == 0 || !IsAdjacent(index, blankIndex))
            {
                return;
            }

            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.82f);
            MoveTile(index);
            if (IsSolved())
            {
                CompleteWin();
            }
        }

        private void MoveTile(int index)
        {
            Swap(index, blankIndex);
            blankIndex = index;
            moves++;
            RefreshAll();
        }

        private void CompleteWin()
        {
            isCompleted = true;
            RefreshAll();
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);

            var settlement = CreateSettlement();
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "SlidingPuzzleSettlementPanel",
                    Style = MiniGameRewardSettlementPanelStyle.Success,
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.Retry,
                    Title = UiTextCatalog.Get("popup.settlement.title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("slidingpuzzle.settlement.moves"), moves.ToString()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("slidingpuzzle.settlement.score"), settlement.Score.ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                ResetGame,
                delegate { CompleteGame?.Invoke(settlement); },
                true);
        }

        private void RefreshAll()
        {
            RefreshHud();
            RefreshBoard();
        }

        private void RefreshHud()
        {
            if (titleLabel != null)
            {
                titleLabel.text = UiTextCatalog.Get("game.slidingpuzzle.name");
            }

            if (scoreLabel != null)
            {
                scoreLabel.text = UiTextCatalog.Format("slidingpuzzle.hud.status", moves, CalculateScore(moves));
            }
        }

        private void RefreshBoard()
        {
            for (var i = 0; i < CellCount; i++)
            {
                var value = board[i];
                var isTile = value != 0;
                if (tileButtons[i] != null)
                {
                    tileButtons[i].gameObject.SetActive(isTile);
                    tileButtons[i].gameObject.name = isTile ? "TileButton_" + value : "TileButton_Empty";
                    tileButtons[i].interactable = !isCompleted && isTile && IsAdjacent(i, blankIndex);
                }

                if (tileLabels[i] != null)
                {
                    tileLabels[i].text = isTile ? value.ToString() : string.Empty;
                }
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
                "SlidingPuzzleSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("slidingpuzzle.settlement.moves"), moves.ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("slidingpuzzle.settlement.exit_label"), UiTextCatalog.Get("slidingpuzzle.settlement.exit_value")),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private void OnRestartClicked()
        {
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            ResetGame();
        }

        private MiniGameSettlement CreateSettlement()
        {
            var score = CalculateScore(moves);
            var coinCount = isCompleted ? Mathf.Max(30, score / 20) : (moves > 0 ? Mathf.Clamp(moves / 10, 1, 20) : 0);
            return new MiniGameSettlement
            {
                Score = score,
                CoinCount = coinCount,
                ChestCount = isCompleted ? 1 : 0,
                Summary = UiTextCatalog.Format("slidingpuzzle.settlement.summary", moves, score)
            };
        }

        private bool IsSolved()
        {
            for (var i = 0; i < CellCount - 1; i++)
            {
                if (board[i] != i + 1)
                {
                    return false;
                }
            }

            return board[CellCount - 1] == 0;
        }

        private void ApplyBoardState(int[] values, int moveCount)
        {
            if (values == null || values.Length != CellCount)
            {
                throw new ArgumentException("Sliding puzzle board must contain 16 cells.", nameof(values));
            }

            Array.Copy(values, board, CellCount);
            blankIndex = Array.IndexOf(board, 0);
            if (blankIndex < 0)
            {
                throw new ArgumentException("Sliding puzzle board must contain one empty cell.", nameof(values));
            }

            moves = Mathf.Max(0, moveCount);
            isCompleted = IsSolved();
            RefreshAll();
        }

        private static int CalculateScore(int moveCount)
        {
            return Mathf.Max(MaxScore - Mathf.Max(0, moveCount) * MovePenalty, MinScore);
        }

        private static bool IsSolvable(int[] values)
        {
            if (values == null || values.Length != CellCount)
            {
                return false;
            }

            var inversions = 0;
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] == 0)
                {
                    continue;
                }

                for (var j = i + 1; j < values.Length; j++)
                {
                    if (values[j] != 0 && values[i] > values[j])
                    {
                        inversions++;
                    }
                }
            }

            var blank = Array.IndexOf(values, 0);
            if (blank < 0)
            {
                return false;
            }

            var blankRowFromBottom = BoardSize - blank / BoardSize;
            return (blankRowFromBottom % 2 == 0) != (inversions % 2 == 0);
        }

        private static bool IsAdjacent(int firstIndex, int secondIndex)
        {
            var firstRow = firstIndex / BoardSize;
            var firstColumn = firstIndex % BoardSize;
            var secondRow = secondIndex / BoardSize;
            var secondColumn = secondIndex % BoardSize;
            return Mathf.Abs(firstRow - secondRow) + Mathf.Abs(firstColumn - secondColumn) == 1;
        }

        private List<int> GetMovableNeighborIndices(int index)
        {
            var neighbors = new List<int>(4);
            var row = index / BoardSize;
            var column = index % BoardSize;
            if (row > 0)
            {
                neighbors.Add(index - BoardSize);
            }

            if (row < BoardSize - 1)
            {
                neighbors.Add(index + BoardSize);
            }

            if (column > 0)
            {
                neighbors.Add(index - 1);
            }

            if (column < BoardSize - 1)
            {
                neighbors.Add(index + 1);
            }

            return neighbors;
        }

        private void Swap(int firstIndex, int secondIndex)
        {
            var temp = board[firstIndex];
            board[firstIndex] = board[secondIndex];
            board[secondIndex] = temp;
        }

        private static void ConfigureTileButton(Button button)
        {
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.95f, 1f, 0.94f, 1f);
            colors.pressedColor = TilePressedColor;
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = TileDisabledColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
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
    }
}
