using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using HuanYouYu.Nonogram;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 数织小游戏运行体：负责搭建界面、控制关卡切换和暂停流程。
    /// </summary>
    public sealed class NonogramGameView : MiniGameBase
    {
        public const string GameIdConstant = "nonogram";
        private const float UiScale = 0.82f;
        private const float TopPadding = 14f * UiScale;
        private const float SidePadding = 28f * UiScale;
        private const float ContentInset = 20f * UiScale;
        private const float LayoutPadding = 18f * UiScale;
        private const float LayoutSpacing = 14f * UiScale;
        private const float ControlSpacing = 10f * UiScale;
        private const float TipsHeight = 58f * UiScale;
        private const float ModeHeight = 30f * UiScale;
        private const float ControlsHeight = 74f * UiScale;
        private const float BoardFramePadding = 28f * UiScale;
        private const float ButtonWidth = 176f * UiScale;
        private const float ButtonHeight = 76f * UiScale;
        private const float ButtonFontSize = 32f * UiScale;
        private const float SecondaryButtonWidth = 118f * UiScale;
        private const float SecondaryButtonHeight = 52f * UiScale;
        private const float SecondaryButtonFontSize = 22f * UiScale;
        private const float TitleFontSize = 46f * UiScale;
        private const float ProgressFontSize = 24f * UiScale;
        private const float TipsFontSize = 26f * UiScale;
        private const float ModeFontSize = 26f * UiScale;
        private const float DefaultHintFontSize = 30f * UiScale;
        private const float DefaultCellLabelFontSize = 60f * UiScale;
        private const float MaxCellSize = 92f * UiScale;
        private const float MinCellSize = 34f * UiScale;
        private const float MinHintStripSize = 76f * UiScale;
        private const float MinBoardWidth = 560f * UiScale;
        private const float MinBoardHeight = 560f * UiScale;
        private const float FallbackBoardWidth = 900f * UiScale;
        private const float FallbackBoardHeight = 780f * UiScale;
        private const float HintHorizontalPadding = 6f * UiScale;
        private const float HintVerticalPadding = 4f * UiScale;
        private const float SmallGridGap = 8f * UiScale;
        private const float LargeGridGap = 4f * UiScale;
        private const float SmallMajorGridGap = 14f * UiScale;
        private const float LargeMajorGridGap = 10f * UiScale;
        private const float HintGridGap = 10f * UiScale;
        private const string CompletedHintColor = "#82C98A";
        private const float SolveRowDuration = 0.09f;
        private const float SolveCellScaleBoost = 0.08f;
        private const float SolveCellGlowBoost = 0.24f;

        private readonly HashSet<int> dragVisitedCells = new HashSet<int>();

        private enum DragStrokeAction
        {
            None = 0,
            Fill = 1,
            Clear = 2
        }

        private enum DragAxis
        {
            None = 0,
            Row = 1,
            Column = 2
        }

        private RectTransform topRoot;
        private RectTransform contentRoot;
        private RectTransform bottomRoot;
        private RectTransform boardInputRoot;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI progressText;
        private TextMeshProUGUI modeText;
        private Button fillModeButton;
        private Button crossModeButton;
        private TextMeshProUGUI[] rowHintTexts;
        private TextMeshProUGUI[] columnHintTexts;
        private NonogramBoardState boardState;
        private NonogramPuzzle activePuzzle;
        private NonogramCellView[,] cellViews;
        private int puzzleIndex = -1;
        private NonogramInputMode inputMode;
        private bool dragActive;
        private NonogramInputMode dragMode;
        private DragStrokeAction dragStrokeAction;
        private DragAxis dragAxis;
        private int dragOriginRow;
        private int dragOriginColumn;
        private int dragCurrentRow;
        private int dragCurrentColumn;
        private Vector2 dragOriginLocalPoint;
        private BoardLayoutMetrics boardMetrics;
        private Coroutine solveAnimationCoroutine;
        private bool isSolveAnimationPlaying;
        private int pendingSolvedPuzzleCount;
        private int pendingSolvedCoinCount;
        private int pendingSolvedChestCount;

        public NonogramGameView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "NonogramView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        protected override MiniGameShellLayout CreateShellLayout()
        {
            return new MiniGameShellLayout(
                MiniGameShellLayout.DefaultTopInset,
                188f * UiScale,
                MiniGameShellBottomMode.DefaultSlot);
        }

        protected override void BuildOrBindSections()
        {
            var topConfig = MiniGameShellTopBarBuilder.CreateDefaultConfig("NonogramTop");
            topConfig.TitleStyle.FontSize = TitleFontSize;
            topConfig.TitleStyle.Color = new Color(0.25f, 0.36f, 0.22f);
            topConfig.ScoreStyle.FontSize = ProgressFontSize;
            topConfig.ScoreStyle.Color = new Color(0.86f, 0.58f, 0.20f);
            topRoot = MiniGameShellTopBarBuilder.CreateTopBar(Shell.TopHost, topConfig).Root;
            bottomRoot = CreateBottomRoot();
            contentRoot = CreateContentRoot();
            CreateBottomContentRoot(bottomRoot);
            CreateBottomSecondaryActions(bottomRoot);

            titleText = FindRequiredText(topRoot, "Header/Title");
            progressText = FindRequiredText(topRoot, "Header/Score");
        }

        protected override void ResetGame()
        {
            pendingSolvedPuzzleCount = 0;
            pendingSolvedCoinCount = 0;
            pendingSolvedChestCount = 0;
            SetInputMode(NonogramInputMode.Fill);
            LoadPuzzle(SelectRandomPuzzleIndex(-1));
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.nonogram.help", null);
        }

        protected override void OnPauseRequested()
        {
            if (isSolveAnimationPlaying)
            {
                return;
            }

            Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        protected override void OnBeforeDispose()
        {
            StopSolveAnimation();
            Shell.ClosePopup();
            if (topRoot != null)
            {
                UnityEngine.Object.Destroy(topRoot.gameObject);
            }

            if (contentRoot != null)
            {
                UnityEngine.Object.Destroy(contentRoot.gameObject);
            }

            if (bottomRoot != null)
            {
                UnityEngine.Object.Destroy(bottomRoot.gameObject);
            }
        }

        private void LoadPuzzle(int index)
        {
            StopSolveAnimation();
            EndDragStroke();
            puzzleIndex = index;
            activePuzzle = NonogramPuzzleLibrary.GetByIndex(index);
            boardState = new NonogramBoardState(activePuzzle.Width, activePuzzle.Height);
            var puzzleTitle = ResolvePuzzleTitle(activePuzzle.Title);

            titleText.text = UiTextCatalog.Get("nonogram.title");
            progressText.text = UiTextCatalog.Format(
                "nonogram.progress",
                puzzleTitle,
                activePuzzle.Width,
                activePuzzle.Height);

            RebuildBoard();
            RefreshModeVisuals();
        }

        private void AdvancePuzzle()
        {
            StopSolveAnimation();
            Shell.ClosePopup();
            LoadPuzzle(SelectRandomPuzzleIndex(puzzleIndex));
        }

        private void RequestAdvancePuzzle()
        {
            if (isSolveAnimationPlaying)
            {
                return;
            }

            EndDragStroke();
            if (!HasBoardProgress())
            {
                AdvancePuzzle();
                return;
            }

            Shell.ShowConfirmPopup(
                UiTextCatalog.Get("nonogram.confirm.next.title"),
                UiTextCatalog.Get("nonogram.confirm.next.message"),
                UiTextCatalog.Get("nonogram.confirm.next.confirm"),
                UiTextCatalog.Get("common.action.cancel"),
                ResumeFromPause,
                AdvancePuzzle);
        }

        private void RestartPuzzle()
        {
            StopSolveAnimation();
            Shell.ClosePopup();
            EndDragStroke();
            boardState.Clear();
            RefreshBoard();
        }

        private void RequestRestartPuzzle()
        {
            if (isSolveAnimationPlaying)
            {
                return;
            }

            EndDragStroke();
            if (!HasBoardProgress())
            {
                RestartPuzzle();
                return;
            }

            Shell.ShowConfirmPopup(
                UiTextCatalog.Get("nonogram.confirm.reset.title"),
                UiTextCatalog.Get("nonogram.confirm.reset.message"),
                UiTextCatalog.Get("nonogram.confirm.reset.confirm"),
                UiTextCatalog.Get("common.action.cancel"),
                ResumeFromPause,
                RestartPuzzle);
        }

        private bool HasBoardProgress()
        {
            if (boardState == null || activePuzzle == null)
            {
                return false;
            }

            for (var row = 0; row < activePuzzle.Height; row++)
            {
                for (var column = 0; column < activePuzzle.Width; column++)
                {
                    if (boardState.GetMark(row, column) != NonogramCellMark.Unknown)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void SetInputMode(NonogramInputMode mode)
        {
            inputMode = mode;
            RefreshModeVisuals();
        }

        private void RefreshModeVisuals()
        {
            if (fillModeButton != null)
            {
                SetButtonSelected(fillModeButton, inputMode == NonogramInputMode.Fill);
            }

            if (crossModeButton != null)
            {
                SetButtonSelected(crossModeButton, inputMode == NonogramInputMode.Cross);
            }

            if (modeText != null)
            {
                modeText.text = inputMode == NonogramInputMode.Fill
                    ? UiTextCatalog.Get("nonogram.mode.fill")
                    : UiTextCatalog.Get("nonogram.mode.cross");
            }
        }

        private void RebuildBoard()
        {
            for (var index = contentRoot.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.Destroy(contentRoot.GetChild(index).gameObject);
            }

            cellViews = new NonogramCellView[activePuzzle.Height, activePuzzle.Width];
            boardMetrics = BuildBoardLayoutMetrics();

            var layoutRoot = CreateVerticalGroup("LayoutRoot", contentRoot, LayoutSpacing, CreateOffset(LayoutPadding, LayoutPadding, LayoutPadding, LayoutPadding));
            Stretch(layoutRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var layoutElement = layoutRoot.gameObject.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.flexibleHeight = 0f;

            var tipsText = CreateText("Tips", layoutRoot, TipsFontSize, FontStyles.Normal, TextAlignmentOptions.Center, true);
            tipsText.text = UiTextCatalog.Get("nonogram.hint");
            tipsText.color = new Color(0.36f, 0.43f, 0.53f);
            AddLayoutSize(tipsText.rectTransform, 0f, TipsHeight, 1f, -1f);

            var boardFrame = CreateBoardFrame(layoutRoot, boardMetrics);
            BuildBoard(boardFrame, boardMetrics);

            RefreshBoard();
        }

        private RectTransform CreateBoardFrame(Transform parent, BoardLayoutMetrics metrics)
        {
            var frameObject = new GameObject("BoardFrame", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            var frame = frameObject.GetComponent<RectTransform>();
            frame.SetParent(parent, false);

            var image = frameObject.GetComponent<Image>();
            image.color = new Color(0.09f, 0.14f, 0.22f, 0.90f);

            AddLayoutSize(frame, metrics.BoardWidth + BoardFramePadding, metrics.BoardHeight + BoardFramePadding, -1f, -1f);
            return frame;
        }

        private void BuildBoard(RectTransform boardFrame, BoardLayoutMetrics metrics)
        {
            var boardRoot = new GameObject("BoardRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            boardRoot.SetParent(boardFrame, false);
            boardRoot.anchorMin = new Vector2(0.5f, 0.5f);
            boardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            boardRoot.pivot = new Vector2(0.5f, 0.5f);
            boardRoot.sizeDelta = new Vector2(metrics.BoardWidth, metrics.BoardHeight);
            boardRoot.anchoredPosition = Vector2.zero;

            rowHintTexts = new TextMeshProUGUI[activePuzzle.Height];
            columnHintTexts = new TextMeshProUGUI[activePuzzle.Width];

            var rowHintX = (-metrics.BoardWidth * 0.5f) + (metrics.RowHintWidth * 0.5f);
            var columnHintY = (metrics.BoardHeight * 0.5f) - (metrics.ColumnHintHeight * 0.5f);
            CreateHintBox(boardRoot, rowHintX, columnHintY, metrics.RowHintWidth, metrics.ColumnHintHeight, string.Empty, TextAlignmentOptions.Center, metrics.HintFontSize, HintHorizontalPadding, HintVerticalPadding);

            var gridStartX = (-metrics.BoardWidth * 0.5f) + metrics.RowHintWidth + HintGridGap;
            for (var column = 0; column < activePuzzle.Width; column++)
            {
                var x = GetCellCenter(gridStartX, column, activePuzzle.Width, metrics.CellSize, metrics.GridGap, metrics.MajorGridGap);
                columnHintTexts[column] = CreateHintBox(
                    boardRoot,
                    x,
                    columnHintY,
                    metrics.CellSize,
                    metrics.ColumnHintHeight,
                    FormatHints(activePuzzle.ColumnHints[column], true),
                    TextAlignmentOptions.Bottom,
                    metrics.HintFontSize,
                    HintHorizontalPadding,
                    2f * UiScale);
            }

            var gridTopY = (metrics.BoardHeight * 0.5f) - metrics.ColumnHintHeight - HintGridGap;
            for (var row = 0; row < activePuzzle.Height; row++)
            {
                var y = GetCellCenterFromTop(gridTopY, row, activePuzzle.Height, metrics.CellSize, metrics.GridGap, metrics.MajorGridGap);
                rowHintTexts[row] = CreateHintBox(
                    boardRoot,
                    rowHintX,
                    y,
                    metrics.RowHintWidth,
                    metrics.CellSize,
                    FormatHints(activePuzzle.RowHints[row], false),
                    TextAlignmentOptions.MidlineRight,
                    metrics.HintFontSize,
                    4f * UiScale,
                    HintVerticalPadding);

                for (var column = 0; column < activePuzzle.Width; column++)
                {
                    var x = GetCellCenter(gridStartX, column, activePuzzle.Width, metrics.CellSize, metrics.GridGap, metrics.MajorGridGap);
                    cellViews[row, column] = CreateCell(boardRoot, row, column, x, y, metrics);
                }
            }

            AddBoardInputOverlay(boardRoot, metrics);
            boardInputRoot = boardRoot;
        }

        private void RefreshBoard()
        {
            for (var row = 0; row < activePuzzle.Height; row++)
            {
                for (var column = 0; column < activePuzzle.Width; column++)
                {
                    RefreshCell(row, column);
                }
            }

            RefreshHintVisuals();
        }

        private void OnCellPressed(int row, int column)
        {
            if (isSolveAnimationPlaying)
            {
                return;
            }

            dragMode = inputMode;
            dragVisitedCells.Clear();
            dragOriginRow = row;
            dragOriginColumn = column;
            dragCurrentRow = row;
            dragCurrentColumn = column;
            dragAxis = DragAxis.None;

            var targetMark = dragMode == NonogramInputMode.Fill
                ? NonogramCellMark.Filled
                : NonogramCellMark.Crossed;
            var currentMark = boardState.GetMark(row, column);

            if (currentMark == targetMark)
            {
                dragStrokeAction = DragStrokeAction.Clear;
                dragActive = true;
                boardState.ClearMark(row, column);
                dragVisitedCells.Add(ToIndex(row, column));
                RefreshCell(row, column);
                RefreshHintVisuals();
                CheckSolvedAndShowPopup();
                return;
            }

            if (currentMark != NonogramCellMark.Unknown)
            {
                dragStrokeAction = DragStrokeAction.None;
                dragActive = false;
                return;
            }

            dragStrokeAction = DragStrokeAction.Fill;
            dragActive = true;

            boardState.SetMark(row, column, targetMark);
            dragVisitedCells.Add(ToIndex(row, column));
            RefreshLineCompletion(row, column, targetMark);
            CheckSolvedAndShowPopup();
        }

        private void OnBoardPointerDown(PointerEventData eventData)
        {
            if (isSolveAnimationPlaying || eventData == null || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (!TryResolvePointerToCell(eventData, out var row, out var column, out var localPoint))
            {
                return;
            }

            dragOriginLocalPoint = localPoint;
            OnCellPressed(row, column);
        }

        private void OnBoardPointerDrag(PointerEventData eventData)
        {
            if (isSolveAnimationPlaying || eventData == null || !dragActive || dragStrokeAction == DragStrokeAction.None)
            {
                return;
            }

            if (!TryResolvePointerToCell(eventData, out var row, out var column, out var localPoint))
            {
                return;
            }

            if (!TryLockDragAxis(localPoint))
            {
                return;
            }

            if (dragAxis == DragAxis.Row)
            {
                ApplyDragPath(dragOriginRow, column);
                return;
            }

            ApplyDragPath(row, dragOriginColumn);
        }

        private void OnBoardPointerUp(PointerEventData eventData)
        {
            if (isSolveAnimationPlaying)
            {
                return;
            }

            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            EndDragStroke();
        }

        private void OnBoardEndDrag(PointerEventData eventData)
        {
            if (isSolveAnimationPlaying)
            {
                return;
            }

            EndDragStroke();
        }

        private void EndDragStroke()
        {
            dragActive = false;
            dragVisitedCells.Clear();
            dragStrokeAction = DragStrokeAction.None;
            dragAxis = DragAxis.None;
        }

        private bool TryLockDragAxis(Vector2 localPoint)
        {
            if (dragAxis != DragAxis.None)
            {
                return true;
            }

            var delta = localPoint - dragOriginLocalPoint;
            var activationDistance = Mathf.Max(6f * UiScale, boardMetrics.CellSize * 0.18f);
            if (Mathf.Abs(delta.x) < activationDistance && Mathf.Abs(delta.y) < activationDistance)
            {
                return false;
            }

            dragAxis = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? DragAxis.Row
                : DragAxis.Column;
            return true;
        }

        private void ApplyDragPath(int row, int column)
        {
            if (dragAxis == DragAxis.Row)
            {
                if (row != dragOriginRow)
                {
                    row = dragOriginRow;
                }

                if (column == dragCurrentColumn)
                {
                    return;
                }

                var step = column > dragCurrentColumn ? 1 : -1;
                for (var currentColumn = dragCurrentColumn + step; currentColumn != column + step; currentColumn += step)
                {
                    if (!TryApplyDragCell(row, currentColumn))
                    {
                        return;
                    }
                }

                dragCurrentColumn = column;
                dragCurrentRow = row;
                return;
            }

            if (column != dragOriginColumn)
            {
                column = dragOriginColumn;
            }

            if (row == dragCurrentRow)
            {
                return;
            }

            var rowStep = row > dragCurrentRow ? 1 : -1;
            for (var currentRow = dragCurrentRow + rowStep; currentRow != row + rowStep; currentRow += rowStep)
            {
                if (!TryApplyDragCell(currentRow, column))
                {
                    return;
                }
            }

            dragCurrentRow = row;
            dragCurrentColumn = column;
        }

        private bool TryApplyDragCell(int row, int column)
        {
            if (isSolveAnimationPlaying || !dragActive || dragStrokeAction == DragStrokeAction.None)
            {
                return false;
            }

            var index = ToIndex(row, column);
            if (dragVisitedCells.Contains(index))
            {
                return true;
            }

            dragVisitedCells.Add(index);
            var targetMark = dragMode == NonogramInputMode.Fill
                ? NonogramCellMark.Filled
                : NonogramCellMark.Crossed;
            var currentMark = boardState.GetMark(row, column);
            if (dragStrokeAction == DragStrokeAction.Fill)
            {
                if (currentMark == NonogramCellMark.Unknown)
                {
                    boardState.SetMark(row, column, targetMark);
                }
                else if (currentMark != targetMark)
                {
                    EndDragStroke();
                    return false;
                }
            }
            else if (dragStrokeAction == DragStrokeAction.Clear)
            {
                if (currentMark == targetMark)
                {
                    boardState.ClearMark(row, column);
                }
                else if (currentMark != NonogramCellMark.Unknown)
                {
                    EndDragStroke();
                    return false;
                }
            }
            else
            {
                return false;
            }

            if (dragStrokeAction == DragStrokeAction.Fill)
            {
                RefreshLineCompletion(row, column, targetMark);
            }
            else
            {
                RefreshCell(row, column);
                RefreshHintVisuals();
            }

            CheckSolvedAndShowPopup();
            return dragActive;
        }

        private void AddBoardInputOverlay(Transform parent, BoardLayoutMetrics metrics)
        {
            var overlayObject = new GameObject("BoardInputOverlay", typeof(RectTransform), typeof(Image), typeof(NonogramBoardInputRelay));
            var overlayRect = overlayObject.GetComponent<RectTransform>();
            overlayRect.SetParent(parent, false);
            overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
            overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
            overlayRect.pivot = new Vector2(0.5f, 0.5f);
            overlayRect.sizeDelta = new Vector2(metrics.BoardWidth, metrics.BoardHeight);
            overlayRect.anchoredPosition = Vector2.zero;

            var overlayImage = overlayObject.GetComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0f);
            overlayImage.raycastTarget = true;

            var relay = overlayObject.GetComponent<NonogramBoardInputRelay>();
            relay.Bind(OnBoardPointerDown, OnBoardPointerDrag, OnBoardPointerUp, OnBoardEndDrag);
        }

        private bool TryResolvePointerToCell(PointerEventData eventData, out int row, out int column, out Vector2 localPoint)
        {
            row = 0;
            column = 0;
            localPoint = Vector2.zero;

            if (eventData == null || boardInputRoot == null)
            {
                return false;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(boardInputRoot, eventData.position, eventData.pressEventCamera, out localPoint))
            {
                return false;
            }

            if (!IsInsideBoardGrid(localPoint))
            {
                return false;
            }

            row = GetNearestRowIndex(localPoint.y);
            column = GetNearestColumnIndex(localPoint.x);
            return row >= 0 && row < activePuzzle.Height && column >= 0 && column < activePuzzle.Width;
        }

        private bool IsInsideBoardGrid(Vector2 localPoint)
        {
            var gridLeft = GetGridStartX();
            var gridRight = gridLeft + GetGridWidth();
            var gridTop = GetGridTopY();
            var gridBottom = gridTop - GetGridHeight();
            var hitPadding = Mathf.Max(boardMetrics.CellSize * 0.45f, 10f * UiScale);

            return localPoint.x >= gridLeft - hitPadding &&
                   localPoint.x <= gridRight + hitPadding &&
                   localPoint.y >= gridBottom - hitPadding &&
                   localPoint.y <= gridTop + hitPadding;
        }

        private int GetNearestColumnIndex(float localX)
        {
            var gridStartX = GetGridStartX();
            var bestColumn = 0;
            var bestDistance = float.MaxValue;

            for (var column = 0; column < activePuzzle.Width; column++)
            {
                var centerX = GetCellCenter(gridStartX, column, activePuzzle.Width, boardMetrics.CellSize, boardMetrics.GridGap, boardMetrics.MajorGridGap);
                var distance = Mathf.Abs(localX - centerX);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestColumn = column;
                }
            }

            return bestColumn;
        }

        private int GetNearestRowIndex(float localY)
        {
            var gridTopY = GetGridTopY();
            var bestRow = 0;
            var bestDistance = float.MaxValue;

            for (var row = 0; row < activePuzzle.Height; row++)
            {
                var centerY = GetCellCenterFromTop(gridTopY, row, activePuzzle.Height, boardMetrics.CellSize, boardMetrics.GridGap, boardMetrics.MajorGridGap);
                var distance = Mathf.Abs(localY - centerY);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestRow = row;
                }
            }

            return bestRow;
        }

        private float GetGridStartX()
        {
            return (-boardMetrics.BoardWidth * 0.5f) + boardMetrics.RowHintWidth + HintGridGap;
        }

        private float GetGridTopY()
        {
            return (boardMetrics.BoardHeight * 0.5f) - boardMetrics.ColumnHintHeight - HintGridGap;
        }

        private float GetGridWidth()
        {
            return (activePuzzle.Width * boardMetrics.CellSize) + GetGridGapTotal(activePuzzle.Width, boardMetrics.GridGap, boardMetrics.MajorGridGap);
        }

        private float GetGridHeight()
        {
            return (activePuzzle.Height * boardMetrics.CellSize) + GetGridGapTotal(activePuzzle.Height, boardMetrics.GridGap, boardMetrics.MajorGridGap);
        }

        private void CheckSolvedAndShowPopup()
        {
            if (isSolveAnimationPlaying || !boardState.IsSolved(activePuzzle))
            {
                return;
            }

            EndDragStroke();
            StartSolveAnimation();
        }

        private void StartSolveAnimation()
        {
            if (isSolveAnimationPlaying)
            {
                return;
            }

            if (HostBehaviour == null || activePuzzle == null || boardState == null)
            {
                ShowSolvedSettlementPanel();
                return;
            }

            isSolveAnimationPlaying = true;
            solveAnimationCoroutine = HostBehaviour.StartCoroutine(PlaySolveAnimationRoutine());
        }

        private IEnumerator PlaySolveAnimationRoutine()
        {
            for (var row = 0; row < activePuzzle.Height; row++)
            {
                var rowHasFilledCell = false;
                for (var column = 0; column < activePuzzle.Width; column++)
                {
                    if (boardState.GetMark(row, column) == NonogramCellMark.Filled)
                    {
                        rowHasFilledCell = true;
                        break;
                    }
                }

                if (!rowHasFilledCell)
                {
                    continue;
                }

                var elapsed = 0f;
                while (elapsed < SolveRowDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    var progress = Mathf.Clamp01(elapsed / SolveRowDuration);
                    var pulse = progress < 0.5f ? progress * 2f : (1f - progress) * 2f;
                    pulse = pulse * pulse * (3f - (2f * pulse));

                    for (var column = 0; column < activePuzzle.Width; column++)
                    {
                        if (boardState.GetMark(row, column) != NonogramCellMark.Filled)
                        {
                            continue;
                        }

                        cellViews[row, column].SetSolvePulse(pulse);
                    }

                    yield return null;
                }
            }

            solveAnimationCoroutine = null;
            isSolveAnimationPlaying = false;

            RefreshBoard();
            ShowSolvedSettlementPanel();
        }

        private void ShowSolvedSettlementPanel()
        {
            var settlement = BuildSolvedSettlement();
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "NonogramSettlementPanel",
                    Style = MiniGameRewardSettlementPanelStyle.Success,
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.Retry,
                    Title = UiTextCatalog.Get("nonogram.settlement.win_title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("nonogram.settlement.pattern"), ResolvePuzzleTitle(activePuzzle == null ? string.Empty : activePuzzle.Title)),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("nonogram.settlement.size"), activePuzzle == null ? string.Empty : activePuzzle.Width + "x" + activePuzzle.Height),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                StartRandomPuzzleAfterSolved,
                delegate { CompleteGame?.Invoke(settlement); },
                true);
        }

        private void StartRandomPuzzleAfterSolved()
        {
            StopSolveAnimation();
            EndDragStroke();
            LoadPuzzle(SelectRandomPuzzleIndex(puzzleIndex));
        }

        private void StopSolveAnimation()
        {
            if (solveAnimationCoroutine != null && HostBehaviour != null)
            {
                HostBehaviour.StopCoroutine(solveAnimationCoroutine);
                solveAnimationCoroutine = null;
            }

            if (isSolveAnimationPlaying)
            {
                isSolveAnimationPlaying = false;
                RefreshBoard();
            }
        }
        private RectTransform CreateContentRoot()
        {
            var rootObject = new GameObject("NonogramContent", typeof(RectTransform));
            var root = rootObject.GetComponent<RectTransform>();
            Shell.AttachContent(root);
            Stretch(root, Vector2.zero, Vector2.one, new Vector2(ContentInset, ContentInset), new Vector2(-ContentInset, -ContentInset));
            return root;
        }

        private RectTransform CreateBottomContentRoot(Transform parent)
        {
            var rootObject = new GameObject("NonogramControlCard", typeof(RectTransform));
            var root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(442f, 104f);
            root.anchoredPosition = new Vector2(0f, 104f);

            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(
                Mathf.RoundToInt(14f * UiScale),
                Mathf.RoundToInt(14f * UiScale),
                Mathf.RoundToInt(12f * UiScale),
                Mathf.RoundToInt(12f * UiScale));
            layout.spacing = Mathf.RoundToInt(6f * UiScale);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var firstRow = CreateHorizontalGroup("ModeButtonsRow", root, 10f * UiScale, CreateOffset(0f, 0f, 0f, 0f));
            firstRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            AddLayoutSize(firstRow, 0f, 68f * UiScale, 1f, -1f);

            fillModeButton = CreateActionButton(firstRow, UiTextCatalog.Get("nonogram.button.fill"));
            crossModeButton = CreateActionButton(firstRow, UiTextCatalog.Get("nonogram.button.cross"));

            fillModeButton.onClick.AddListener(delegate { SetInputMode(NonogramInputMode.Fill); });
            crossModeButton.onClick.AddListener(delegate { SetInputMode(NonogramInputMode.Cross); });

            return root;
        }

        private RectTransform CreateBottomSecondaryActions(Transform parent)
        {
            var rootObject = new GameObject("NonogramSecondaryActions", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.sizeDelta = new Vector2((SecondaryButtonWidth * 2f) + (10f * UiScale), SecondaryButtonHeight);
            root.anchoredPosition = new Vector2(24f * UiScale, 16f * UiScale);

            var layout = rootObject.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f * UiScale;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var resetButton = CreateSecondaryActionButton(root, UiTextCatalog.Get("nonogram.button.reset"));
            var nextButton = CreateSecondaryActionButton(root, UiTextCatalog.Get("nonogram.button.next"));
            resetButton.onClick.AddListener(RequestRestartPuzzle);
            nextButton.onClick.AddListener(RequestAdvancePuzzle);

            return root;
        }

        private RectTransform CreateBottomRoot()
        {
            var rootObject = new GameObject("NonogramBottom", typeof(RectTransform));
            var root = rootObject.GetComponent<RectTransform>();
            root.SetParent(Shell.BottomHost, false);
            Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return root;
        }

        private static TextMeshProUGUI FindRequiredText(Transform root, string path)
        {
            var target = root.Find(path);
            var text = target == null ? null : target.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                throw new InvalidOperationException("Missing text at path: " + path);
            }

            return text;
        }

        private NonogramCellView CreateCell(Transform parent, int row, int column, float x, float y, BoardLayoutMetrics metrics)
        {
            var cellObject = new GameObject(
                string.Format("Cell_{0}_{1}", row, column),
                typeof(RectTransform),
                typeof(Image));
            var rect = cellObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(metrics.CellSize, metrics.CellSize);
            rect.anchoredPosition = new Vector2(x, y);

            var image = cellObject.GetComponent<Image>();
            image.color = new Color(0.93f, 0.95f, 0.98f);

            var label = CreateText("Label", rect, metrics.CellFontSize, FontStyles.Bold, TextAlignmentOptions.Center, false);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.enabled = false;

            var crossRoot = CreateCrossMark(rect, metrics);
            return new NonogramCellView(rect, image, label, crossRoot);
        }

        private static RectTransform CreateVerticalGroup(string name, Transform parent, float spacing, RectOffset padding)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            var layout = gameObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.UpperCenter;

            return rect;
        }

        private static RectTransform CreateHorizontalGroup(string name, Transform parent, float spacing, RectOffset padding)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            var layout = gameObject.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;

            return rect;
        }

        private static RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offset, Color color, float cornerRadius)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            panel.transform.SetParent(parent, false);

            var rect = panel.GetComponent<RectTransform>();
            Stretch(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            rect.anchoredPosition = offset;

            var graphic = panel.GetComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = cornerRadius;
            graphic.raycastTarget = false;
            return rect;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment, bool wrap)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = MiniGameFontProvider.DefaultFont;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.white;
            text.enableWordWrapping = wrap;
            text.richText = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.text = string.Empty;
            text.raycastTarget = false;

            return text;
        }

        private static TextMeshProUGUI CreateHintBox(Transform parent, float x, float y, float width, float height, string value, TextAlignmentOptions alignment, float fontSize, float horizontalPadding, float verticalPadding)
        {
            var boxObject = new GameObject("HintBox", typeof(RectTransform), typeof(Image));
            var rect = boxObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);

            var image = boxObject.GetComponent<Image>();
            image.color = new Color(0.24f, 0.32f, 0.46f);

            var text = CreateText("HintText", rect, fontSize, FontStyles.Bold, alignment, true);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(horizontalPadding, verticalPadding), new Vector2(-horizontalPadding, -verticalPadding));
            text.color = new Color(0.97f, 0.98f, 1f);
            text.enableAutoSizing = true;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = Mathf.Max(14f * UiScale, fontSize * 0.58f);
            text.lineSpacing = -10f * UiScale;
            text.overflowMode = TextOverflowModes.Overflow;
            text.text = value;
            return text;
        }

        private static Button CreateActionButton(Transform parent, string label)
        {
            var buttonObject = new GameObject(
                label + "Button",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.96f, 0.97f, 0.92f, 1f);

            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredWidth = ButtonWidth;
            layout.preferredHeight = ButtonHeight;

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(1f, 0.98f, 0.88f, 1f);
            colors.pressedColor = new Color(0.84f, 0.89f, 0.76f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            var text = CreateText("Label", rect, ButtonFontSize, FontStyles.Bold, TextAlignmentOptions.Center, false);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.text = label;
            text.color = new Color(0.25f, 0.36f, 0.22f);

            return button;
        }

        private static Button CreateSecondaryActionButton(Transform parent, string label)
        {
            var buttonObject = new GameObject(
                label + "Button",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(SecondaryButtonWidth, SecondaryButtonHeight);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.83f, 0.86f, 0.78f, 1f);

            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredWidth = SecondaryButtonWidth;
            layout.preferredHeight = SecondaryButtonHeight;

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.90f, 0.92f, 0.84f, 1f);
            colors.pressedColor = new Color(0.70f, 0.76f, 0.66f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            var text = CreateText("Label", rect, SecondaryButtonFontSize, FontStyles.Bold, TextAlignmentOptions.Center, false);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.text = label;
            text.color = new Color(0.30f, 0.36f, 0.29f);

            return button;
        }

        private static GameObject CreateCrossMark(Transform parent, BoardLayoutMetrics metrics)
        {
            var rootObject = new GameObject("CrossMark", typeof(RectTransform));
            var root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            Stretch(root, Vector2.zero, Vector2.one, new Vector2(metrics.CrossPadding, metrics.CrossPadding), new Vector2(-metrics.CrossPadding, -metrics.CrossPadding));

            CreateCrossBar(root, 45f, metrics.CrossBarWidth, metrics.CrossBarHeight);
            CreateCrossBar(root, -45f, metrics.CrossBarWidth, metrics.CrossBarHeight);
            rootObject.SetActive(false);
            return rootObject;
        }

        private static void CreateCrossBar(Transform parent, float rotation, float width, float height)
        {
            var barObject = new GameObject("Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedBarGraphic));
            var rect = barObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);

            var graphic = barObject.GetComponent<RoundedBarGraphic>();
            graphic.color = new Color(0.78f, 0.2f, 0.17f);
        }

        private static void SetButtonSelected(Button button, bool selected)
        {
            var image = button == null ? null : button.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            image.color = selected ? new Color(0.90f, 0.75f, 0.28f) : new Color(0.96f, 0.97f, 0.92f, 1f);
        }

        private static void AddLayoutSize(RectTransform rect, float preferredWidth, float preferredHeight, float flexibleWidth, float flexibleHeight)
        {
            var layout = rect.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = rect.gameObject.AddComponent<LayoutElement>();
            }

            if (preferredWidth >= 0f)
            {
                layout.preferredWidth = preferredWidth;
            }

            if (preferredHeight >= 0f)
            {
                layout.preferredHeight = preferredHeight;
            }

            if (flexibleWidth >= 0f)
            {
                layout.flexibleWidth = flexibleWidth;
            }

            if (flexibleHeight >= 0f)
            {
                layout.flexibleHeight = flexibleHeight;
            }
        }

        private static RectOffset CreateOffset(float left, float right, float top, float bottom)
        {
            return new RectOffset(
                Mathf.RoundToInt(left),
                Mathf.RoundToInt(right),
                Mathf.RoundToInt(top),
                Mathf.RoundToInt(bottom));
        }

        private static string FormatHints(IReadOnlyList<int> values, bool multiline, IReadOnlyList<bool> completed = null)
        {
            if (values == null || values.Count == 0)
            {
                return "0";
            }

            if (!multiline)
            {
                var lineBuilder = new StringBuilder();
                for (var index = 0; index < values.Count; index++)
                {
                    if (index > 0)
                    {
                        lineBuilder.Append(' ');
                    }

                    AppendHintValue(lineBuilder, values[index], completed != null && index < completed.Count && completed[index]);
                }

                return lineBuilder.ToString();
            }

            var multilineBuilder = new StringBuilder();
            for (var index = 0; index < values.Count; index++)
            {
                if (index > 0)
                {
                    multilineBuilder.Append('\n');
                }

                AppendHintValue(multilineBuilder, values[index], completed != null && index < completed.Count && completed[index]);
            }

            return multilineBuilder.ToString();
        }

        private static void AppendHintValue(StringBuilder builder, int value, bool completed)
        {
            if (!completed)
            {
                builder.Append(value);
                return;
            }

            builder.Append("<color=");
            builder.Append(CompletedHintColor);
            builder.Append('>');
            builder.Append(value);
            builder.Append("</color>");
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static string ResolvePuzzleTitle(string titleKey)
        {
            if (string.IsNullOrWhiteSpace(titleKey))
            {
                return string.Empty;
            }

            return UiTextCatalog.GetOrFallback(titleKey, titleKey);
        }

        private void RefreshCell(int row, int column)
        {
            cellViews[row, column].Refresh(boardState.GetMark(row, column));
        }

        private void RefreshHintVisuals()
        {
            if (boardState == null || activePuzzle == null)
            {
                return;
            }

            if (rowHintTexts != null)
            {
                for (var row = 0; row < activePuzzle.Height; row++)
                {
                    var text = rowHintTexts[row];
                    if (text == null)
                    {
                        continue;
                    }

                    text.text = FormatHints(activePuzzle.RowHints[row], false, boardState.GetRowHintCompletion(activePuzzle, row));
                }
            }

            if (columnHintTexts != null)
            {
                for (var column = 0; column < activePuzzle.Width; column++)
                {
                    var text = columnHintTexts[column];
                    if (text == null)
                    {
                        continue;
                    }

                    text.text = FormatHints(activePuzzle.ColumnHints[column], true, boardState.GetColumnHintCompletion(activePuzzle, column));
                }
            }
        }

        private void RefreshLineCompletion(int row, int column, NonogramCellMark mark)
        {
            if (mark != NonogramCellMark.Filled)
            {
                RefreshCell(row, column);
                return;
            }

            var autoChanged = boardState.AutoCrossSatisfiedLines(activePuzzle);
            if (autoChanged)
            {
                RefreshBoard();
                return;
            }

            RefreshCell(row, column);
            RefreshHintVisuals();
        }

        private int SelectRandomPuzzleIndex(int excludedIndex)
        {
            if (NonogramPuzzleLibrary.Count <= 1)
            {
                return 0;
            }

            var nextIndex = UnityEngine.Random.Range(0, NonogramPuzzleLibrary.Count - 1);
            if (excludedIndex >= 0 && nextIndex >= excludedIndex)
            {
                nextIndex += 1;
            }

            return nextIndex;
        }

        private BoardLayoutMetrics BuildBoardLayoutMetrics()
        {
            var maxRowHintCount = GetMaxHintCount(activePuzzle.RowHints);
            var maxColumnHintCount = GetMaxHintCount(activePuzzle.ColumnHints);
            var gridGap = activePuzzle.Width >= 10 || activePuzzle.Height >= 10 ? LargeGridGap : SmallGridGap;
            var majorGridGap = activePuzzle.Width >= 10 || activePuzzle.Height >= 10 ? LargeMajorGridGap : SmallMajorGridGap;

            var contentWidth = Mathf.Max(MinBoardWidth, contentRoot.rect.width > 0f ? contentRoot.rect.width - (LayoutPadding * 2f) - BoardFramePadding : FallbackBoardWidth);
            var contentHeight = Mathf.Max(MinBoardHeight, contentRoot.rect.height > 0f ? contentRoot.rect.height - TipsHeight - LayoutSpacing - (LayoutPadding * 2f) - BoardFramePadding : FallbackBoardHeight);

            var provisionalHintUnit = 34f * UiScale;
            var rowHintWidth = Mathf.Max(MinHintStripSize, (maxRowHintCount * provisionalHintUnit) + (HintHorizontalPadding * 2f));
            var columnHintHeight = Mathf.Max(MinHintStripSize, (maxColumnHintCount * provisionalHintUnit) + (HintVerticalPadding * 2f));

            var gridWidthGaps = GetGridGapTotal(activePuzzle.Width, gridGap, majorGridGap);
            var gridHeightGaps = GetGridGapTotal(activePuzzle.Height, gridGap, majorGridGap);

            var cellSize = Mathf.Min(
                (contentWidth - rowHintWidth - HintGridGap - gridWidthGaps) / activePuzzle.Width,
                (contentHeight - columnHintHeight - HintGridGap - gridHeightGaps) / activePuzzle.Height);

            cellSize = Mathf.Clamp(cellSize, MinCellSize, MaxCellSize);

            var hintUnit = Mathf.Clamp(cellSize * 0.82f, 22f * UiScale, 36f * UiScale);
            rowHintWidth = Mathf.Max(MinHintStripSize, (maxRowHintCount * hintUnit) + (HintHorizontalPadding * 2f));
            columnHintHeight = Mathf.Max(MinHintStripSize, (maxColumnHintCount * hintUnit) + (HintVerticalPadding * 2f));

            cellSize = Mathf.Min(
                (contentWidth - rowHintWidth - HintGridGap - gridWidthGaps) / activePuzzle.Width,
                (contentHeight - columnHintHeight - HintGridGap - gridHeightGaps) / activePuzzle.Height);
            cellSize = Mathf.Clamp(cellSize, MinCellSize, MaxCellSize);

            var boardWidth = rowHintWidth + HintGridGap + (activePuzzle.Width * cellSize) + gridWidthGaps;
            var boardHeight = columnHintHeight + HintGridGap + (activePuzzle.Height * cellSize) + gridHeightGaps;

            return new BoardLayoutMetrics(
                cellSize,
                gridGap,
                majorGridGap,
                rowHintWidth,
                columnHintHeight,
                boardWidth,
                boardHeight,
                Mathf.Clamp(cellSize * 0.46f, 16f * UiScale, DefaultHintFontSize),
                Mathf.Clamp(cellSize * 0.68f, 20f * UiScale, DefaultCellLabelFontSize),
                Mathf.Clamp(cellSize * 0.14f, 6f * UiScale, 12f * UiScale),
                Mathf.Clamp(cellSize * 0.74f, 20f * UiScale, 68f * UiScale),
                Mathf.Clamp(cellSize * 0.18f, 8f * UiScale, 18f * UiScale));
        }

        private static int GetMaxHintCount(IReadOnlyList<int>[] hints)
        {
            var maxCount = 1;
            if (hints == null)
            {
                return maxCount;
            }

            for (var i = 0; i < hints.Length; i++)
            {
                if (hints[i] != null && hints[i].Count > maxCount)
                {
                    maxCount = hints[i].Count;
                }
            }

            return maxCount;
        }

        private static float GetGridGapTotal(int count, float gridGap, float majorGridGap)
        {
            var total = 0f;
            for (var boundary = 1; boundary < count; boundary++)
            {
                total += boundary % 5 == 0 ? majorGridGap : gridGap;
            }

            return total;
        }

        private static float GetCellCenter(float start, int index, int count, float cellSize, float gridGap, float majorGridGap)
        {
            var offset = 0f;
            for (var boundary = 1; boundary <= index; boundary++)
            {
                offset += boundary % 5 == 0 ? majorGridGap : gridGap;
            }

            return start + (index * cellSize) + offset + (cellSize * 0.5f);
        }

        private static float GetCellCenterFromTop(float top, int index, int count, float cellSize, float gridGap, float majorGridGap)
        {
            var offset = 0f;
            for (var boundary = 1; boundary <= index; boundary++)
            {
                offset += boundary % 5 == 0 ? majorGridGap : gridGap;
            }

            return top - (index * cellSize) - offset - (cellSize * 0.5f);
        }

        private int ToIndex(int row, int column)
        {
            return (row * activePuzzle.Width) + column;
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
        }

        private void ConfirmExitToHall()
        {
            StopSolveAnimation();
            EndDragStroke();
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.92f);
            var settlement = BuildSessionSettlementForExit();
            ShowBackHallRewardSettlementPanel(
                settlement,
                "NonogramSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("nonogram.settlement.pattern"), ResolvePuzzleTitle(activePuzzle == null ? string.Empty : activePuzzle.Title)),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("nonogram.settlement.size"), activePuzzle == null ? string.Empty : activePuzzle.Width + "x" + activePuzzle.Height),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private MiniGameSettlement BuildExitSettlement()
        {
            return new MiniGameSettlement
            {
                Score = 0,
                CoinCount = 20,
                ChestCount = 0,
                Summary = UiTextCatalog.Get("nonogram.settlement.exit")
            };
        }

        private MiniGameSettlement BuildSolvedSettlement()
        {
            return new MiniGameSettlement
            {
                Score = 1,
                CoinCount = 60,
                ChestCount = 1,
                Summary = string.Format(
                    "完成了“{0}”，获得 60 金币和 1 个宝箱。",
                    ResolvePuzzleTitle(activePuzzle == null ? string.Empty : activePuzzle.Title))
            };
        }

        private void AccumulateSolvedSettlement()
        {
            var settlement = BuildSolvedSettlement();
            pendingSolvedPuzzleCount += 1;
            pendingSolvedCoinCount += settlement.CoinCount;
            pendingSolvedChestCount += settlement.ChestCount;
        }

        private MiniGameSettlement BuildSessionSettlementForExit()
        {
            var exitSettlement = BuildExitSettlement();
            if (pendingSolvedPuzzleCount <= 0)
            {
                return exitSettlement;
            }

            return new MiniGameSettlement
            {
                Score = pendingSolvedPuzzleCount,
                CoinCount = pendingSolvedCoinCount + exitSettlement.CoinCount,
                ChestCount = pendingSolvedChestCount,
                Summary = string.Format(
                    "本次已完成 {0} 个图案，获得 {1} 金币和 {2} 个宝箱；当前题退出额外获得 {3} 金币。",
                    pendingSolvedPuzzleCount,
                    pendingSolvedCoinCount,
                    pendingSolvedChestCount,
                    exitSettlement.CoinCount)
            };
        }

        private sealed class NonogramCellView
        {
            private static readonly Color FilledColor = new Color(0.16f, 0.22f, 0.34f);
            private static readonly Color CrossedColor = new Color(0.97f, 0.95f, 0.94f);
            private static readonly Color EmptyColor = new Color(0.93f, 0.95f, 0.98f);
            private static readonly Color SolveGlowColor = new Color(0.30f, 0.45f, 0.72f);

            private readonly RectTransform rectTransform;
            private readonly Image background;
            private readonly TextMeshProUGUI label;
            private readonly GameObject crossMark;
            private NonogramCellMark currentMark;

            public NonogramCellView(RectTransform cellTransform, Image backgroundImage, TextMeshProUGUI labelText, GameObject crossMarkObject)
            {
                rectTransform = cellTransform;
                background = backgroundImage;
                label = labelText;
                crossMark = crossMarkObject;
            }

            public void Refresh(NonogramCellMark mark)
            {
                currentMark = mark;
                ApplyBaseVisuals();
            }

            public void SetSolvePulse(float pulse)
            {
                if (currentMark != NonogramCellMark.Filled)
                {
                    return;
                }

                var scale = 1f + (SolveCellScaleBoost * pulse);
                rectTransform.localScale = Vector3.one * scale;
                background.color = Color.Lerp(FilledColor, SolveGlowColor, SolveCellGlowBoost * pulse);
            }

            private void ApplyBaseVisuals()
            {
                rectTransform.localScale = Vector3.one;

                switch (currentMark)
                {
                    case NonogramCellMark.Filled:
                        background.color = FilledColor;
                        label.text = string.Empty;
                        crossMark.SetActive(false);
                        break;
                    case NonogramCellMark.Crossed:
                        background.color = CrossedColor;
                        label.text = string.Empty;
                        crossMark.SetActive(true);
                        break;
                    default:
                        background.color = EmptyColor;
                        label.text = string.Empty;
                        crossMark.SetActive(false);
                        break;
                }
            }
        }

        private readonly struct BoardLayoutMetrics
        {
            public BoardLayoutMetrics(
                float cellSize,
                float gridGap,
                float majorGridGap,
                float rowHintWidth,
                float columnHintHeight,
                float boardWidth,
                float boardHeight,
                float hintFontSize,
                float cellFontSize,
                float crossPadding,
                float crossBarWidth,
                float crossBarHeight)
            {
                CellSize = cellSize;
                GridGap = gridGap;
                MajorGridGap = majorGridGap;
                RowHintWidth = rowHintWidth;
                ColumnHintHeight = columnHintHeight;
                BoardWidth = boardWidth;
                BoardHeight = boardHeight;
                HintFontSize = hintFontSize;
                CellFontSize = cellFontSize;
                CrossPadding = crossPadding;
                CrossBarWidth = crossBarWidth;
                CrossBarHeight = crossBarHeight;
            }

            public float CellSize { get; }

            public float GridGap { get; }

            public float MajorGridGap { get; }

            public float RowHintWidth { get; }

            public float ColumnHintHeight { get; }

            public float BoardWidth { get; }

            public float BoardHeight { get; }

            public float HintFontSize { get; }

            public float CellFontSize { get; }

            public float CrossPadding { get; }

            public float CrossBarWidth { get; }

            public float CrossBarHeight { get; }
        }

        private sealed class NonogramBoardInputRelay : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IEndDragHandler
        {
            private Action<PointerEventData> pointerDownAction;
            private Action<PointerEventData> dragAction;
            private Action<PointerEventData> pointerUpAction;
            private Action<PointerEventData> endDragAction;

            public void Bind(
                Action<PointerEventData> onPointerDown,
                Action<PointerEventData> onDrag,
                Action<PointerEventData> onPointerUp,
                Action<PointerEventData> onEndDrag)
            {
                pointerDownAction = onPointerDown;
                dragAction = onDrag;
                pointerUpAction = onPointerUp;
                endDragAction = onEndDrag;
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                pointerDownAction?.Invoke(eventData);
            }

            public void OnDrag(PointerEventData eventData)
            {
                dragAction?.Invoke(eventData);
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                pointerUpAction?.Invoke(eventData);
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                endDragAction?.Invoke(eventData);
            }
        }

        [RequireComponent(typeof(CanvasRenderer))]
        private sealed class RoundedBarGraphic : MaskableGraphic
        {
            protected override void OnPopulateMesh(VertexHelper vertexHelper)
            {
                vertexHelper.Clear();

                var rect = rectTransform.rect;
                var radius = Mathf.Min(rect.height * 0.5f, rect.width * 0.5f);
                var leftCenter = new Vector2(rect.xMin + radius, rect.center.y);
                var rightCenter = new Vector2(rect.xMax - radius, rect.center.y);
                AddCenterQuad(vertexHelper, rect, radius);

                AddSemicircle(vertexHelper, leftCenter, radius, 90f, 270f);
                AddSemicircle(vertexHelper, rightCenter, radius, -90f, 90f);
            }

            private void AddCenterQuad(VertexHelper vertexHelper, Rect rect, float radius)
            {
                var startIndex = vertexHelper.currentVertCount;
                vertexHelper.AddVert(new Vector2(rect.xMin + radius, rect.yMin), color, Vector2.zero);
                vertexHelper.AddVert(new Vector2(rect.xMin + radius, rect.yMax), color, Vector2.zero);
                vertexHelper.AddVert(new Vector2(rect.xMax - radius, rect.yMax), color, Vector2.zero);
                vertexHelper.AddVert(new Vector2(rect.xMax - radius, rect.yMin), color, Vector2.zero);
                vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
                vertexHelper.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
            }

            private void AddSemicircle(VertexHelper vertexHelper, Vector2 center, float radius, float startDegrees, float endDegrees)
            {
                var centerIndex = vertexHelper.currentVertCount;
                vertexHelper.AddVert(center, color, Vector2.zero);

                var steps = 12;
                var angleStep = (endDegrees - startDegrees) / steps;
                for (var i = 0; i <= steps; i++)
                {
                    var angle = Mathf.Deg2Rad * (startDegrees + (angleStep * i));
                    var point = new Vector2(
                        center.x + (Mathf.Cos(angle) * radius),
                        center.y + (Mathf.Sin(angle) * radius));
                    vertexHelper.AddVert(point, color, Vector2.zero);
                }

                for (var i = 0; i < steps; i++)
                {
                    vertexHelper.AddTriangle(centerIndex, centerIndex + i + 1, centerIndex + i + 2);
                }
            }
        }
    }
}
