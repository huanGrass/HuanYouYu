using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed class MiniGameBlockPuzzleGameView : MiniGameBase
    {
        public const string GameIdConstant = "blockpuzzle";

        private const float BoardPanelSize = 626f;
        private const float BoardCellSize = 52f;
        private const float BoardCellGap = 5f;
        private const float BoardGridSize = (BlockPuzzleBoard.Size * BoardCellSize) + ((BlockPuzzleBoard.Size - 1) * BoardCellGap);
        private const float TrayCellSize = 30f;
        private const float TrayCellGap = 4f;

        private static readonly Color ContentStatusColor = new Color32(72, 91, 65, 255);
        private static readonly Color BoardPanelColor = new Color(1f, 0.98f, 0.91f, 0.84f);
        private static readonly Color BoardCellEmptyColor = new Color32(225, 234, 205, 255);
        private static readonly Color BoardCellPreviewColor = new Color32(137, 191, 117, 212);
        private static readonly Color BoardCellInvalidColor = new Color32(226, 89, 76, 190);
        private static readonly Color SlotColor = new Color(1f, 0.98f, 0.92f, 0.62f);
        private static readonly Color SlotStrokeColor = new Color(0.33f, 0.43f, 0.26f, 0.12f);
        private static readonly Color32[] PieceColors =
        {
            new Color32(93, 164, 219, 255),
            new Color32(237, 132, 81, 255),
            new Color32(111, 189, 128, 255),
            new Color32(221, 178, 70, 255),
            new Color32(153, 126, 211, 255),
            new Color32(232, 99, 124, 255),
            new Color32(67, 177, 170, 255)
        };

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private TextMeshProUGUI statusLabel;
        private Button restartButton;
        private RectTransform contentRoot;
        private RectTransform boardGrid;
        private readonly RoundedRectGraphic[,] boardCells = new RoundedRectGraphic[BlockPuzzleBoard.Size, BlockPuzzleBoard.Size];
        private readonly RectTransform[] traySlots = new RectTransform[BlockPuzzleGameState.TraySlotCount];
        private readonly BlockPuzzlePieceView[] pieceViews = new BlockPuzzlePieceView[BlockPuzzleGameState.TraySlotCount];
        private BlockPuzzleGameState gameState;
        private BlockPuzzlePieceView activeDragView;
        private int activeDragAnchorX;
        private int activeDragAnchorY;
        private bool activeDragCanPlace;
        private bool isGameOverSettlementVisible;

        public MiniGameBlockPuzzleGameView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "MiniGameBlockPuzzleView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        protected override void BuildOrBindSections()
        {
            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("BlockPuzzleHeader"));
            titleLabel = topBarRefs.TitleText;
            scoreLabel = topBarRefs.ScoreText;

            BuildContent();

            var bottomContainerRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("BlockPuzzleActions"));
            restartButton = MiniGameShellBottomBarBuilder.CreateRestartButton(bottomContainerRefs.ActionBar).Button;

            if (restartButton != null)
            {
                restartButton.gameObject.name = "RestartButton";
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (titleLabel == null || scoreLabel == null || statusLabel == null || restartButton == null || boardGrid == null)
            {
                throw new InvalidOperationException("BlockPuzzle runtime structure is incomplete.");
            }
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            CloseRewardSettlementPanel();
            isGameOverSettlementVisible = false;
            activeDragView = null;

            if (gameState == null)
            {
                gameState = new BlockPuzzleGameState();
            }

            gameState.Reset();
            RefreshHud();
            RefreshBoard();
            RefreshTray();
            SetStatus("blockpuzzle.status.ready");
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.blockpuzzle.help", null);
        }

        protected override void OnPauseRequested()
        {
            Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        protected override void OnBeforeDispose()
        {
            Shell.ClosePopup();
            activeDragView = null;

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }
        }

        private void BuildContent()
        {
            var rootObject = CreateRectObject("BlockPuzzleContent", Shell.ContentHost);
            contentRoot = rootObject;
            Stretch(contentRoot, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(0f, 0f));

            var layout = rootObject.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(34, 34, 10, 12);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            statusLabel = CreateTextObject("Status", contentRoot, 24f, FontStyles.Bold, ContentStatusColor);
            statusLabel.alignment = TextAlignmentOptions.Center;
            statusLabel.enableWordWrapping = false;
            var statusLayout = statusLabel.gameObject.AddComponent<LayoutElement>();
            statusLayout.preferredWidth = 640f;
            statusLayout.preferredHeight = 42f;

            var boardPanel = CreateRoundedRect("BoardPanel", contentRoot, BoardPanelColor, 34f);
            boardPanel.raycastTarget = false;
            var boardPanelRect = boardPanel.rectTransform;
            boardPanelRect.sizeDelta = new Vector2(BoardPanelSize, BoardPanelSize);
            var boardLayout = boardPanel.gameObject.AddComponent<LayoutElement>();
            boardLayout.preferredWidth = BoardPanelSize;
            boardLayout.preferredHeight = BoardPanelSize;

            boardGrid = CreateRectObject("BoardGrid", boardPanelRect);
            boardGrid.anchorMin = new Vector2(0.5f, 0.5f);
            boardGrid.anchorMax = new Vector2(0.5f, 0.5f);
            boardGrid.pivot = new Vector2(0.5f, 0.5f);
            boardGrid.anchoredPosition = Vector2.zero;
            boardGrid.sizeDelta = new Vector2(BoardGridSize, BoardGridSize);

            for (var y = 0; y < BlockPuzzleBoard.Size; y++)
            {
                for (var x = 0; x < BlockPuzzleBoard.Size; x++)
                {
                    var cell = CreateRoundedRect("Cell_" + x + "_" + y, boardGrid, BoardCellEmptyColor, 10f);
                    cell.raycastTarget = false;
                    ConfigureGridCell(cell.rectTransform, x, y, BoardCellSize, BoardCellGap, BoardGridSize);
                    boardCells[x, y] = cell;
                }
            }

            var trayRoot = CreateRectObject("Tray", contentRoot);
            trayRoot.sizeDelta = new Vector2(680f, 198f);
            var trayLayoutElement = trayRoot.gameObject.AddComponent<LayoutElement>();
            trayLayoutElement.preferredWidth = 680f;
            trayLayoutElement.preferredHeight = 198f;

            var trayLayout = trayRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            trayLayout.spacing = 18f;
            trayLayout.childAlignment = TextAnchor.MiddleCenter;
            trayLayout.childControlWidth = true;
            trayLayout.childControlHeight = true;
            trayLayout.childForceExpandWidth = false;
            trayLayout.childForceExpandHeight = false;

            for (var i = 0; i < traySlots.Length; i++)
            {
                traySlots[i] = CreateTraySlot("TraySlot_" + i, trayRoot, i);
            }
        }

        private RectTransform CreateTraySlot(string name, Transform parent, int trayIndex)
        {
            var slot = CreateRoundedRect(name, parent, SlotColor, 26f);
            slot.raycastTarget = true;
            var rect = slot.rectTransform;
            rect.sizeDelta = new Vector2(206f, 188f);
            var layout = slot.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 206f;
            layout.preferredHeight = 188f;

            var dragHandler = slot.gameObject.AddComponent<BlockPuzzlePieceDragHandler>();
            dragHandler.Bind(this, trayIndex);

            var stroke = CreateRoundedRect("Stroke", rect, SlotStrokeColor, 28f);
            stroke.raycastTarget = false;
            Stretch(stroke.rectTransform, Vector2.zero, Vector2.one, new Vector2(-4f, -4f), new Vector2(4f, 4f));
            return rect;
        }

        private void RefreshHud()
        {
            if (titleLabel != null)
            {
                titleLabel.text = UiTextCatalog.Get("game.blockpuzzle.name");
            }

            if (scoreLabel != null)
            {
                var currentScore = gameState != null ? gameState.Score : 0;
                scoreLabel.text = UiTextCatalog.Get("blockpuzzle.hud.score") + " " + currentScore;
            }
        }

        private void RefreshBoard()
        {
            if (gameState == null)
            {
                return;
            }

            for (var y = 0; y < BlockPuzzleBoard.Size; y++)
            {
                for (var x = 0; x < BlockPuzzleBoard.Size; x++)
                {
                    var value = gameState.Board.GetCellValue(x, y);
                    boardCells[x, y].color = value > 0 ? ResolvePieceColor(value) : BoardCellEmptyColor;
                }
            }
        }

        private void RefreshTray()
        {
            for (var i = 0; i < traySlots.Length; i++)
            {
                DestroyChildren(traySlots[i]);
                pieceViews[i] = null;

                var piece = gameState != null ? gameState.GetTrayPiece(i) : null;
                if (piece == null)
                {
                    continue;
                }

                pieceViews[i] = CreatePieceView(piece, i, traySlots[i], TrayCellSize, TrayCellGap);
            }
        }

        private BlockPuzzlePieceView CreatePieceView(BlockPuzzlePiece piece, int trayIndex, RectTransform parent, float cellSize, float cellGap)
        {
            var root = CreateRectObject("Piece_" + trayIndex, parent);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var canvasGroup = root.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            var cellGraphics = new RoundedRectGraphic[piece.CellCount];
            for (var i = 0; i < piece.CellCount; i++)
            {
                var cellGraphic = CreateRoundedRect("PieceCell_" + i, root, ResolvePieceColor(piece.ColorIndex), 8f);
                cellGraphic.raycastTarget = false;
                cellGraphics[i] = cellGraphic;
            }

            var view = new BlockPuzzlePieceView(piece, trayIndex, root, canvasGroup, cellGraphics);
            ConfigurePieceGeometry(view, cellSize, cellGap);
            return view;
        }

        private void ConfigurePieceGeometry(BlockPuzzlePieceView view, float cellSize, float cellGap)
        {
            if (view == null)
            {
                return;
            }

            var width = ComputePiecePixelSize(view.Piece.Width, cellSize, cellGap);
            var height = ComputePiecePixelSize(view.Piece.Height, cellSize, cellGap);
            view.Root.sizeDelta = new Vector2(width, height);

            for (var i = 0; i < view.Piece.CellCount; i++)
            {
                var cell = view.Piece.GetCell(i);
                var rect = view.CellGraphics[i].rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(cellSize, cellSize);
                rect.anchoredPosition = new Vector2(
                    (-width * 0.5f) + (cellSize * 0.5f) + (cell.X * (cellSize + cellGap)),
                    (-height * 0.5f) + (cellSize * 0.5f) + (cell.Y * (cellSize + cellGap)));
            }
        }

        private bool BeginPieceDrag(int trayIndex, PointerEventData eventData)
        {
            if (gameState == null || gameState.IsGameOver || !IsValidTrayIndex(trayIndex))
            {
                return false;
            }

            var view = pieceViews[trayIndex];
            if (view == null || view.Root == null)
            {
                return false;
            }

            activeDragView = view;
            activeDragCanPlace = false;
            view.HomeParent = view.Root.parent as RectTransform;
            view.Root.SetParent(contentRoot, false);
            view.Root.SetAsLastSibling();
            view.CanvasGroup.alpha = 0.92f;
            view.CanvasGroup.blocksRaycasts = false;
            ConfigurePieceGeometry(view, BoardCellSize, BoardCellGap);
            UpdatePieceDrag(trayIndex, eventData);
            MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.82f);
            return true;
        }

        private void UpdatePieceDrag(int trayIndex, PointerEventData eventData)
        {
            if (activeDragView == null || activeDragView.TrayIndex != trayIndex)
            {
                return;
            }

            Vector2 contentPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(contentRoot, eventData.position, eventData.pressEventCamera, out contentPoint))
            {
                activeDragView.Root.anchoredPosition = contentPoint;
            }

            if (TryResolvePlacement(activeDragView, eventData, out activeDragAnchorX, out activeDragAnchorY))
            {
                activeDragCanPlace = gameState.Board.CanPlace(activeDragView.Piece, activeDragAnchorX, activeDragAnchorY);
                ShowPlacementPreview(activeDragView.Piece, activeDragAnchorX, activeDragAnchorY, activeDragCanPlace);
            }
        }

        private void EndPieceDrag(int trayIndex, PointerEventData eventData)
        {
            if (activeDragView == null || activeDragView.TrayIndex != trayIndex)
            {
                return;
            }

            UpdatePieceDrag(trayIndex, eventData);
            var view = activeDragView;
            var shouldPlace = activeDragCanPlace;
            var anchorX = activeDragAnchorX;
            var anchorY = activeDragAnchorY;
            activeDragView = null;
            activeDragCanPlace = false;
            RefreshBoard();

            if (shouldPlace)
            {
                var result = gameState.TryPlaceTrayPiece(trayIndex, anchorX, anchorY);
                if (result.Success)
                {
                    DestroyPieceView(view);
                    RefreshHud();
                    RefreshBoard();
                    RefreshTray();
                    PlayPlacementSfx(result);
                    UpdateStatusAfterMove(result);
                    if (result.GameOver)
                    {
                        ShowGameOverSettlement();
                    }

                    return;
                }
            }

            RestorePieceView(view);
            MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.68f);
            SetStatus("blockpuzzle.status.ready");
        }

        private bool TryResolvePlacement(BlockPuzzlePieceView view, PointerEventData eventData, out int anchorX, out int anchorY)
        {
            anchorX = 0;
            anchorY = 0;
            if (view == null || boardGrid == null)
            {
                return false;
            }

            Vector2 boardPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(boardGrid, eventData.position, eventData.pressEventCamera, out boardPoint))
            {
                return false;
            }

            var pieceWidth = ComputePiecePixelSize(view.Piece.Width, BoardCellSize, BoardCellGap);
            var pieceHeight = ComputePiecePixelSize(view.Piece.Height, BoardCellSize, BoardCellGap);
            var left = boardPoint.x - (pieceWidth * 0.5f);
            var bottom = boardPoint.y - (pieceHeight * 0.5f);
            anchorX = Mathf.RoundToInt((left + (BoardGridSize * 0.5f)) / (BoardCellSize + BoardCellGap));
            anchorY = Mathf.RoundToInt((bottom + (BoardGridSize * 0.5f)) / (BoardCellSize + BoardCellGap));
            return true;
        }

        private void ShowPlacementPreview(BlockPuzzlePiece piece, int anchorX, int anchorY, bool canPlace)
        {
            RefreshBoard();
            var previewColor = canPlace ? BoardCellPreviewColor : BoardCellInvalidColor;
            for (var i = 0; i < piece.CellCount; i++)
            {
                var cell = piece.GetCell(i);
                var x = anchorX + cell.X;
                var y = anchorY + cell.Y;
                if (x >= 0 && x < BlockPuzzleBoard.Size && y >= 0 && y < BlockPuzzleBoard.Size)
                {
                    boardCells[x, y].color = previewColor;
                }
            }
        }

        private void RestorePieceView(BlockPuzzlePieceView view)
        {
            if (view == null || view.Root == null || view.HomeParent == null)
            {
                return;
            }

            view.Root.SetParent(view.HomeParent, false);
            view.Root.anchoredPosition = Vector2.zero;
            view.Root.localScale = Vector3.one;
            view.CanvasGroup.alpha = 1f;
            view.CanvasGroup.blocksRaycasts = true;
            ConfigurePieceGeometry(view, TrayCellSize, TrayCellGap);
        }

        private void DestroyPieceView(BlockPuzzlePieceView view)
        {
            if (view == null || view.Root == null)
            {
                return;
            }

            view.Root.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(view.Root.gameObject);
        }

        private void PlayPlacementSfx(BlockPuzzleMoveResult result)
        {
            if (result.LinesCleared > 1)
            {
                MiniGameSfxPlayer.Play(MiniGameSfxType.Combo, 0.92f);
            }
            else if (result.LinesCleared == 1)
            {
                MiniGameSfxPlayer.Play(MiniGameSfxType.MatchSuccess, 0.86f);
            }
            else
            {
                MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.7f);
            }
        }

        private void UpdateStatusAfterMove(BlockPuzzleMoveResult result)
        {
            if (result.GameOver)
            {
                SetStatus("blockpuzzle.status.game_over");
            }
            else if (result.LinesCleared > 0)
            {
                statusLabel.text = UiTextCatalog.Format("blockpuzzle.status.clear", result.LinesCleared);
            }
            else
            {
                SetStatus("blockpuzzle.status.ready");
            }
        }

        private void ShowGameOverSettlement()
        {
            if (isGameOverSettlementVisible)
            {
                return;
            }

            isGameOverSettlementVisible = true;
            SetStatus("blockpuzzle.status.game_over");
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);

            var settlement = CreateSettlement();
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "BlockPuzzleGameOverSettlementPanel",
                    Style = MiniGameRewardSettlementPanelStyle.Failure,
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.Retry,
                    Title = UiTextCatalog.Get("blockpuzzle.settlement.game_over_title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("blockpuzzle.settlement.score"), settlement.Score.ToString()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("blockpuzzle.settlement.reason_label"), UiTextCatalog.Get("blockpuzzle.settlement.no_moves")),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                delegate
                {
                    GrantSettlementReward(settlement);
                    ResetGame();
                },
                delegate
                {
                    GrantSettlementReward(settlement);
                    CompleteGame?.Invoke(settlement);
                },
                false);
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
                "BlockPuzzleSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("blockpuzzle.settlement.score"), settlement.Score.ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("blockpuzzle.settlement.exit_label"), UiTextCatalog.Get("blockpuzzle.settlement.exit_value")),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private void OnRestartClicked()
        {
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            ResetGame();
        }

        private MiniGameSettlement CreateSettlement()
        {
            var finalScore = gameState != null ? gameState.Score : 0;
            var coinCount = Mathf.Clamp(finalScore / 100, 0, 50);
            var chestCount = finalScore >= 1500 ? 1 : 0;
            return new MiniGameSettlement
            {
                Score = finalScore,
                CoinCount = coinCount,
                ChestCount = chestCount,
                Summary = UiTextCatalog.Format("blockpuzzle.settlement.summary", finalScore, coinCount, chestCount)
            };
        }

        private void SetStatus(string textKey)
        {
            if (statusLabel != null)
            {
                statusLabel.text = UiTextCatalog.Get(textKey);
            }
        }

        private static RectTransform CreateRectObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private static RoundedRectGraphic CreateRoundedRect(string name, Transform parent, Color color, float cornerRadius)
        {
            var rect = CreateRectObject(name, parent);
            rect.gameObject.AddComponent<CanvasRenderer>();
            var graphic = rect.gameObject.AddComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = cornerRadius;
            return graphic;
        }

        private static TextMeshProUGUI CreateTextObject(string name, Transform parent, float fontSize, FontStyles fontStyle, Color color)
        {
            var rect = CreateRectObject(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            var fontAsset = MiniGameFontProvider.DefaultFont;
            if (fontAsset != null)
            {
                text.font = fontAsset;
            }

            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static void ConfigureGridCell(RectTransform rect, int x, int y, float cellSize, float cellGap, float gridSize)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(cellSize, cellSize);
            rect.anchoredPosition = new Vector2(
                (-gridSize * 0.5f) + (cellSize * 0.5f) + (x * (cellSize + cellGap)),
                (-gridSize * 0.5f) + (cellSize * 0.5f) + (y * (cellSize + cellGap)));
        }

        private static float ComputePiecePixelSize(int cellCount, float cellSize, float cellGap)
        {
            if (cellCount <= 0)
            {
                return 0f;
            }

            return (cellCount * cellSize) + ((cellCount - 1) * cellGap);
        }

        private static Color ResolvePieceColor(int colorIndex)
        {
            return PieceColors[Mathf.Abs(colorIndex - 1) % PieceColors.Length];
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private static void DestroyChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (string.Equals(child.name, "Stroke", StringComparison.Ordinal))
                {
                    continue;
                }

                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        private static bool IsValidTrayIndex(int index)
        {
            return index >= 0 && index < BlockPuzzleGameState.TraySlotCount;
        }

        private sealed class BlockPuzzlePieceView
        {
            public BlockPuzzlePieceView(
                BlockPuzzlePiece piece,
                int trayIndex,
                RectTransform root,
                CanvasGroup canvasGroup,
                RoundedRectGraphic[] cellGraphics)
            {
                Piece = piece;
                TrayIndex = trayIndex;
                Root = root;
                CanvasGroup = canvasGroup;
                CellGraphics = cellGraphics;
            }

            public BlockPuzzlePiece Piece { get; }

            public int TrayIndex { get; }

            public RectTransform Root { get; }

            public CanvasGroup CanvasGroup { get; }

            public RoundedRectGraphic[] CellGraphics { get; }

            public RectTransform HomeParent { get; set; }
        }

        private sealed class BlockPuzzlePieceDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            private MiniGameBlockPuzzleGameView owner;
            private int trayIndex;

            public void Bind(MiniGameBlockPuzzleGameView view, int index)
            {
                owner = view;
                trayIndex = index;
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                owner?.BeginPieceDrag(trayIndex, eventData);
            }

            public void OnDrag(PointerEventData eventData)
            {
                owner?.UpdatePieceDrag(trayIndex, eventData);
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                owner?.EndPieceDrag(trayIndex, eventData);
            }
        }
    }
}
