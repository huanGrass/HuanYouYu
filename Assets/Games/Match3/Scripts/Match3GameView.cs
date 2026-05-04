using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 三消玩法运行时视图与交互控制器：负责棋盘渲染、输入处理、动画与结算。
    /// </summary>
    public sealed class Match3GameView : MiniGameBase
    {
        public const string GameIdConstant = "match-3";
        private const int Rows = 7;
        private const int Columns = 7;
        private const float SwipeThreshold = 28f;

        private const int TileTypeCount = 6;
        private const float ClearFlashDuration = 0.10f;
        private const float ClearFlashPeakAlpha = 0.82f;
        private const int ClearShardCountPerTile = 4;
        private const float ClearShardDuration = 0.18f;
        private const float ComboPopupDuration = 0.50f;
        private const float ComboPopupRiseDistance = 44f;
        private const float ScorePulseDuration = 0.18f;
        private const float ScorePulseScale = 1.08f;
        private const float GhostSettleScale = 1.08f;
        private const float GhostSettleDuration = 0.12f;
        private const float GhostFailSquashX = 1.10f;
        private const float GhostFailSquashY = 0.90f;
        private const float GhostFailSquashDuration = 0.10f;
        private static readonly Color ClearFlashColor = new Color(1f, 1f, 1f, ClearFlashPeakAlpha);
        private static readonly Color ClearGlowColor = new Color(1f, 0.96f, 0.80f, 0.28f);
        private static readonly Color ClearShardColor = new Color(1f, 0.97f, 0.80f, 0.92f);
        private static readonly Color ComboPopupColor = new Color(1f, 0.95f, 0.66f, 0.98f);

        private MonoBehaviour host;
        private Match3AnimationConfig animationConfig;
        private Action<MiniGameSettlement> completeGame;
        private Action exitToHall;
        private MiniGameShell shell;
        private GameObject root;
        private RectTransform boardFrameRoot;
        private RectTransform boardRoot;
        private RectTransform animationLayer;
        private RectTransform boardShadowRect;
        private RectTransform boardFrameLightRect;
        private GridLayoutGroup boardGridLayout;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI scoreText;
        private MatchTileView[] tileViews;
        private readonly int[,] board = new int[Rows, Columns];
        private readonly int[,] busyCounts = new int[Rows, Columns];
        private readonly int[,] hiddenCounts = new int[Rows, Columns];
        private readonly List<GameObject> transientEffects = new List<GameObject>();

        private int score;
        private int clearedTileCount;
        private int activeOperationCount;
        private bool isBusy;
        private bool isFinished;
        private bool isPaused;
        private Vector2Int? selectedTile;
        private Vector2Int? dragOriginTile;
        private Vector2 dragStartScreenPosition;
        private int dragPointerId = int.MinValue;
        private bool dragThresholdReached;
        private bool dragSwapTriggered;
        private Vector2Int? suppressedClickTile;
        private int suppressedClickPointerId = int.MinValue;
        private Vector2Int? hintedFirstSwap;
        private Vector2Int? hintedSecondSwap;
        private Coroutine scorePulseRoutine;

        private float SwapDuration => animationConfig.SwapDuration;
        private float InvalidSwapHoldDuration => animationConfig.InvalidSwapHoldDuration;
        private float SwapSettleDuration => animationConfig.SwapSettleDuration;
        private float ClearDuration => animationConfig.ClearDuration;
        private float ClearHoldDuration => animationConfig.ClearHoldDuration;
        private float FallDuration => animationConfig.FallDuration;
        private float FallSettleDuration => animationConfig.FallSettleDuration;
        private float ShuffleFadeDuration => animationConfig.ShuffleFadeDuration;

        public Match3GameView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "Match3View", hostBehaviour, parent, onComplete, onExit)
        {
        }

        private bool TryBuildRuntimeSections()
        {
            shell = Shell;
            root = shell.Root;

            var contentRoot = CreateContentSection(shell.ContentHost);
            var bottomContainerRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("Match3Bottom"));

            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("Match3Top"));
            var topBar = topBarRefs.Root;
            var boardFrame = contentRoot.GetComponent<RectTransform>();
            var footer = bottomContainerRefs.Root;
            shell.AttachTop(topBar);
            shell.AttachContent(boardFrame);
            shell.AttachBottom(footer);

            boardFrameRoot = boardFrame;
            titleText = topBarRefs.TitleText;
            scoreText = topBarRefs.ScoreText;
            boardShadowRect = boardFrame.Find("BoardShadow") as RectTransform;
            boardFrameLightRect = boardFrame.Find("BoardFrameLight") as RectTransform;
            boardRoot = boardFrame.Find("BoardSurface/BoardGrid") as RectTransform;
            animationLayer = boardFrame.Find("BoardSurface/AnimationLayer") as RectTransform;
            boardGridLayout = boardRoot != null ? boardRoot.GetComponent<GridLayoutGroup>() : null;

            var shuffleButton = MiniGameShellBottomBarBuilder.CreateShuffleButton(bottomContainerRefs.ActionBar);
            var hintButton = MiniGameShellBottomBarBuilder.CreateHintButton(bottomContainerRefs.ActionBar);

            if (titleText == null || scoreText == null || boardRoot == null || animationLayer == null || boardGridLayout == null || shuffleButton.Button == null || hintButton.Button == null || shuffleButton.Icon == null || hintButton.Icon == null)
            {
                UnityEngine.Object.Destroy(root);
                root = null;
                return false;
            }

            shuffleButton.Button.onClick.RemoveAllListeners();
            shuffleButton.Button.onClick.AddListener(ShuffleBoardByPlayer);
            hintButton.Button.onClick.RemoveAllListeners();
            hintButton.Button.onClick.AddListener(ShowHintByPlayer);
            RefreshStaticTexts();

            var template = boardRoot.Find("TileTemplate") as RectTransform;
            if (template == null)
            {
                template = boardRoot.Find("MatchTile_0_0") as RectTransform;
            }

            if (template == null)
            {
                UnityEngine.Object.Destroy(root);
                root = null;
                return false;
            }

            tileViews = BuildTilesFromTemplate(template);
            if (tileViews == null)
            {
                UnityEngine.Object.Destroy(root);
                root = null;
                return false;
            }

            return true;
        }

        private static GameObject CreateContentSection(Transform parent)
        {
            var root = CreateRectObject("Match3Content", parent);
            Stretch(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            CreatePanel(
                root.transform,
                "BoardShadow",
                new Vector2(0.18f, 0.12f),
                new Vector2(0.82f, 0.88f),
                new Vector2(0f, -4f),
                new Color(0.31f, 0.42f, 0.26f, 0.09f),
                28f);

            CreatePanel(
                root.transform,
                "BoardFrameLight",
                new Vector2(0.16f, 0.10f),
                new Vector2(0.84f, 0.90f),
                Vector2.zero,
                new Color(1f, 0.985f, 0.94f, 0.18f),
                30f);

            var boardSurface = CreateRectObject("BoardSurface", root.transform);
            Stretch(boardSurface.GetComponent<RectTransform>(), new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.96f), Vector2.zero, Vector2.zero);
            var boardSurfaceGraphic = boardSurface.AddComponent<RoundedRectGraphic>();
            boardSurfaceGraphic.color = new Color(0f, 0f, 0f, 0f);
            boardSurfaceGraphic.CornerRadius = 0f;
            boardSurfaceGraphic.raycastTarget = false;

            var boardGrid = CreateRectObject("BoardGrid", boardSurface.transform);
            Stretch(boardGrid.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var gridLayout = boardGrid.AddComponent<GridLayoutGroup>();
            gridLayout.childAlignment = TextAnchor.MiddleCenter;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.cellSize = new Vector2(44f, 44f);
            gridLayout.spacing = new Vector2(10f, 10f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = Columns;

            CreateTileTemplate(boardGrid.transform);

            var animationLayer = CreateRectObject("AnimationLayer", boardSurface.transform);
            Stretch(animationLayer.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            animationLayer.transform.SetAsLastSibling();

            return root;
        }

        private static void CreateTileTemplate(Transform parent)
        {
            var tile = new GameObject("TileTemplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic), typeof(Button), typeof(CanvasGroup));
            tile.transform.SetParent(parent, false);

            var rect = tile.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            var graphic = tile.GetComponent<RoundedRectGraphic>();
            graphic.color = new Color(0f, 0f, 0f, 0f);
            graphic.CornerRadius = 0f;

            var button = tile.GetComponent<Button>();
            button.targetGraphic = graphic;

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            icon.transform.SetParent(tile.transform, false);
            Stretch(icon.GetComponent<RectTransform>(), new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero);
            var iconImage = icon.GetComponent<RawImage>();
            iconImage.color = Color.white;
            iconImage.raycastTarget = false;

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(tile.transform, false);
            Stretch(label.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var text = label.GetComponent<TextMeshProUGUI>();
            text.font = MiniGameFontProvider.DefaultFont;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }

        private static RoundedRectGraphic CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Color color, float cornerRadius)
        {
            var panel = CreateRectObject(name, parent);
            var graphic = panel.AddComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = cornerRadius;
            graphic.raycastTarget = false;
            Stretch(panel.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            panel.GetComponent<RectTransform>().anchoredPosition = anchoredPosition;
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
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
        }

        private void RefreshStaticTexts()
        {
            titleText.text = UiTextCatalog.Get("game.match3.name");
        }

        /// <summary>
        /// 每帧推进运行状态与在途动画协程状态。
        /// </summary>
        public override void Tick(float deltaTime)
        {
            if (isFinished)
            {
                return;
            }

            if (isPaused)
            {
                return;
            }
        }

        protected override void BuildOrBindSections()
        {
            host = HostBehaviour;
            animationConfig = Match3AnimationConfig.LoadOrCreate();
            completeGame = CompleteGame;
            exitToHall = ExitToHall;

            if (!TryBuildRuntimeSections())
            {
                throw new InvalidOperationException("Match3 runtime sections not found or invalid.");
            }
        }

        protected override void ResetGame()
        {
            shell.ClosePopup();
            CloseRewardSettlementPanel();
            score = 0;
            clearedTileCount = 0;
            isFinished = false;
            isPaused = false;
            isBusy = false;
            activeOperationCount = 0;
            selectedTile = null;
            dragOriginTile = null;
            dragPointerId = int.MinValue;
            dragThresholdReached = false;
            dragSwapTriggered = false;
            suppressedClickTile = null;
            suppressedClickPointerId = int.MinValue;
            hintedFirstSwap = null;
            hintedSecondSwap = null;
            Match3BoardUtility.FillBoard(board, Rows, Columns, TileTypeCount);
            RefreshBoardLayout();
            RefreshAllTiles();
            RefreshHud(UiTextCatalog.Get("match3.hud.initial"));
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.match3.help", null);
        }

        private MatchTileView[] BuildTilesFromTemplate(RectTransform template)
        {
            var staleChildren = new List<Transform>();
            for (var i = 0; i < boardRoot.childCount; i++)
            {
                var child = boardRoot.GetChild(i);
                if (child != template)
                {
                    staleChildren.Add(child);
                }
            }

            for (var i = 0; i < staleChildren.Count; i++)
            {
                UnityEngine.Object.Destroy(staleChildren[i].gameObject);
            }

            template.name = "TileTemplate";
            template.gameObject.SetActive(false);

            var result = new MatchTileView[Rows * Columns];
            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    var tile = UnityEngine.Object.Instantiate(template, boardRoot, false);
                    tile.gameObject.SetActive(true);
                    tile.name = "MatchTile_" + row + "_" + column;

                    var button = tile.GetComponent<Button>();
                    var graphic = tile.GetComponent<RoundedRectGraphic>();
                    var label = tile.GetComponentInChildren<TextMeshProUGUI>(true);
                    var icon = tile.Find("Icon")?.GetComponent<RawImage>();
                    if (button == null || graphic == null || label == null || icon == null)
                    {
                        return null;
                    }

                    var canvasGroup = tile.GetComponent<CanvasGroup>();
                    if (canvasGroup == null)
                    {
                        canvasGroup = tile.gameObject.AddComponent<CanvasGroup>();
                    }

                    label.gameObject.SetActive(false);
                    var relay = tile.GetComponent<Match3TileInputRelay>();
                    if (relay == null)
                    {
                        relay = tile.gameObject.AddComponent<Match3TileInputRelay>();
                    }

                    var captureRow = row;
                    var captureColumn = column;
                    relay.PointerDown += delegate(PointerEventData eventData) { HandleTilePointerDown(captureRow, captureColumn, eventData); };
                    relay.Drag += delegate(PointerEventData eventData) { HandleTileDrag(captureRow, captureColumn, eventData); };
                    relay.EndDrag += delegate(PointerEventData eventData) { HandleTileEndDrag(captureRow, captureColumn, eventData); };
                    relay.PointerClick += delegate(PointerEventData eventData) { HandleTilePointerClick(captureRow, captureColumn, eventData); };

                    result[ToIndex(row, column)] = new MatchTileView
                    {
                        Row = row,
                        Column = column,
                        Button = button,
                        Rect = tile,
                        Graphic = graphic,
                        Label = label,
                        Icon = icon,
                        CanvasGroup = canvasGroup
                    };
                }
            }

            return result;
        }

        private void HandleTilePointerDown(int row, int column, PointerEventData eventData)
        {
            if (isFinished || isPaused || !IsCellInteractive(row, column))
            {
                return;
            }

            ClearHint();
            dragOriginTile = new Vector2Int(column, row);
            dragStartScreenPosition = eventData.position;
            dragPointerId = eventData.pointerId;
            dragThresholdReached = false;
            dragSwapTriggered = false;
        }

        private void HandleTileDrag(int row, int column, PointerEventData eventData)
        {
            if (isPaused)
            {
                return;
            }

            if (!dragOriginTile.HasValue || dragPointerId != eventData.pointerId || dragSwapTriggered)
            {
                return;
            }

            if (dragOriginTile.Value.x != column || dragOriginTile.Value.y != row)
            {
                return;
            }

            var delta = eventData.position - dragStartScreenPosition;
            if (delta.sqrMagnitude < SwipeThreshold * SwipeThreshold)
            {
                return;
            }

            dragThresholdReached = true;
            var direction = GetSwipeDirection(delta);
            var target = dragOriginTile.Value + direction;
            if (!IsInsideBoard(target) || !IsCellInteractive(target.y, target.x))
            {
                return;
            }

            dragSwapTriggered = true;
            suppressedClickTile = dragOriginTile;
            suppressedClickPointerId = dragPointerId;
            selectedTile = null;
            ClearHint();
            RefreshAllTiles();
            host.StartCoroutine(ResolveSwapRoutine(dragOriginTile.Value, target));
        }

        private void HandleTileEndDrag(int row, int column, PointerEventData eventData)
        {
            if (isPaused)
            {
                return;
            }

            if (!dragOriginTile.HasValue || dragPointerId != eventData.pointerId)
            {
                return;
            }

            if (dragThresholdReached)
            {
                suppressedClickTile = dragOriginTile;
                suppressedClickPointerId = dragPointerId;
            }

            ClearDragState();
        }

        private void HandleTilePointerClick(int row, int column, PointerEventData eventData)
        {
            if (isPaused)
            {
                return;
            }

            if (suppressedClickTile.HasValue &&
                suppressedClickPointerId == eventData.pointerId &&
                suppressedClickTile.Value.x == column &&
                suppressedClickTile.Value.y == row)
            {
                suppressedClickTile = null;
                suppressedClickPointerId = int.MinValue;
                return;
            }

            HandleTileClick(row, column);
        }

        private void HandleTileClick(int row, int column)
        {
            if (isFinished || isPaused)
            {
                return;
            }

            if (!IsCellInteractive(row, column))
            {
                if (IsCellBusy(row, column))
                {
                    RefreshHud(UiTextCatalog.Get("match3.hud.cell_busy"));
                }

                return;
            }

            ClearHint();
            var clicked = new Vector2Int(column, row);
            if (selectedTile.HasValue && !IsCellInteractive(selectedTile.Value.y, selectedTile.Value.x))
            {
                selectedTile = null;
            }

            if (!selectedTile.HasValue)
            {
                selectedTile = clicked;
                RefreshAllTiles();
                RefreshHud(UiTextCatalog.Get("match3.hud.first_selected"));
                MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.55f, UnityEngine.Random.Range(0.98f, 1.05f));
                return;
            }

            if (selectedTile.Value == clicked)
            {
                selectedTile = null;
                RefreshAllTiles();
                RefreshHud(UiTextCatalog.Get("match3.hud.cancel_selected"));
                MiniGameSfxPlayer.Play(MiniGameSfxType.UiBack, 0.45f, 1f);
                return;
            }

            if (!AreAdjacent(selectedTile.Value, clicked))
            {
                selectedTile = clicked;
                RefreshAllTiles();
                RefreshHud(UiTextCatalog.Get("match3.hud.only_adjacent"));
                MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.50f, UnityEngine.Random.Range(0.97f, 1.03f));
                return;
            }

            host.StartCoroutine(ResolveSwapRoutine(selectedTile.Value, clicked));
        }

        private IEnumerator ResolveSwapRoutine(Vector2Int first, Vector2Int second)
        {
            ClearHint();
            if (!IsCellInteractive(first.y, first.x) || !IsCellInteractive(second.y, second.x))
            {
                selectedTile = null;
                RefreshAllTiles();
                RefreshHud(UiTextCatalog.Get("match3.hud.cells_busy"));
                yield break;
            }

            var plan = Match3BoardUtility.BuildResolvePlan(board, Rows, Columns, TileTypeCount, first, second);
            var lockedCells = Match3BoardUtility.CollectPlanLockedCells(plan, Rows, Columns, first, second);
            if (!CanLockCells(lockedCells))
            {
                selectedTile = null;
                RefreshAllTiles();
                RefreshHud(UiTextCatalog.Get("match3.hud.area_busy"));
                yield break;
            }

            var operation = BeginOperation(lockedCells);
            selectedTile = null;
            RefreshAllTiles();
            RefreshHud(UiTextCatalog.Get("match3.hud.try_swap"));

            try
            {
                var firstValue = board[first.y, first.x];
                var secondValue = board[second.y, second.x];

                if (!plan.IsValidSwap)
                {
                    yield return AnimateSwapAttempt(operation, first, second, firstValue, secondValue, true);
                    RefreshHud(UiTextCatalog.Get("match3.hud.invalid_swap"));
                    MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.62f, 1f);
                    yield break;
                }

                yield return AnimateSwapAttempt(operation, first, second, firstValue, secondValue, false);
                MiniGameSfxPlayer.Play(MiniGameSfxType.MatchSuccess, 0.70f, UnityEngine.Random.Range(0.96f, 1.04f));
                Match3BoardUtility.ApplyBoard(board, plan.BoardAfterSwap, operation.LockedCells);
                ClearOperationHiddenCells(operation);
                RefreshAllTiles();
                yield return AnimatePause(SwapSettleDuration);

                for (var i = 0; i < plan.Steps.Count; i++)
                {
                    if (i > 0)
                    {
                        MiniGameSfxPlayer.Play(MiniGameSfxType.Combo, 0.72f, 1f);
                        ShowComboPopup(i + 1);
                    }

                    yield return AnimateCascadeStep(operation, plan.Steps[i]);
                }

                Match3BoardUtility.ApplyBoard(board, plan.FinalBoard, operation.LockedCells);
                ClearOperationHiddenCells(operation);
                RefreshAllTiles();

                if (plan.WasReshuffled)
                {
                    yield return AnimateBoardShuffleReveal();
                }

                clearedTileCount += plan.ClearedCount;
                score += plan.ClearedCount * 60 + Mathf.Max(0, plan.CascadeCount - 1) * 120;
                PlayScorePulse();
                RefreshAllTiles();

                var nextHint = plan.WasReshuffled
                    ? UiTextCatalog.Get("match3.hud.auto_shuffle")
                    : UiTextCatalog.Format("match3.hud.cleared", plan.ClearedCount);
                RefreshHud(nextHint);
                if (plan.WasReshuffled)
                {
                    MiniGameSfxPlayer.Play(MiniGameSfxType.Shuffle, 0.64f, 1f);
                }
            }
            finally
            {
                EndOperation(operation);
            }
        }

        private IEnumerator AnimateCascadeStep(Match3OperationContext operation, Match3CascadeStep step)
        {
            Match3BoardUtility.ApplyBoard(board, step.BoardBeforeClear, operation.LockedCells);
            ClearOperationHiddenCells(operation);
            RefreshAllTiles();

            yield return AnimateClear(step.ClearedCells);
            yield return AnimatePause(ClearHoldDuration);

            Match3BoardUtility.ApplyBoard(board, step.BoardAfterClear, operation.LockedCells);
            SetOperationHiddenCells(operation, step.ClearedCells);
            RefreshAllTiles();

            yield return AnimateFallAndRefill(operation, step);

            Match3BoardUtility.ApplyBoard(board, step.BoardAfterRefill, operation.LockedCells);
            ClearOperationHiddenCells(operation);
            RefreshAllTiles();
        }

        private IEnumerator AnimateSwapAttempt(Match3OperationContext operation, Vector2Int first, Vector2Int second, int firstValue, int secondValue, bool swapBack)
        {
            ClearOperationGhosts(operation);
            SetOperationHiddenCells(operation, new[] { new Vector2Int(first.x, first.y), new Vector2Int(second.x, second.y) });
            RefreshAllTiles();

            var firstGhost = CreateGhost(operation, firstValue, GetSlotCenter(first.y, first.x));
            var secondGhost = CreateGhost(operation, secondValue, GetSlotCenter(second.y, second.x));

            yield return AnimateGhostMove(
                new[] { firstGhost, secondGhost },
                new[] { GetSlotCenter(second.y, second.x), GetSlotCenter(first.y, first.x) },
                SwapDuration);
            yield return AnimateGhostScalePulse(new[] { firstGhost, secondGhost }, GhostSettleScale, GhostSettleDuration);

            if (swapBack)
            {
                yield return AnimatePause(InvalidSwapHoldDuration);
                yield return AnimateGhostMove(
                    new[] { firstGhost, secondGhost },
                    new[] { GetSlotCenter(first.y, first.x), GetSlotCenter(second.y, second.x) },
                    SwapDuration);
                yield return AnimateGhostSquash(new[] { firstGhost, secondGhost }, GhostFailSquashX, GhostFailSquashY, GhostFailSquashDuration);
            }

            ClearOperationGhosts(operation);
            ClearOperationHiddenCells(operation);
            RefreshAllTiles();
        }

        private IEnumerator AnimateClear(IList<Vector2Int> clearedCells)
        {
            var views = new List<MatchTileView>(clearedCells.Count);
            for (var i = 0; i < clearedCells.Count; i++)
            {
                var view = tileViews[ToIndex(clearedCells[i].y, clearedCells[i].x)];
                view.ClearFlashTriggered = false;
                view.ClearShardsTriggered = false;
                views.Add(view);
            }

            var startTime = Time.realtimeSinceStartup;
            while (true)
            {
                var progress = GetRealtimeProgress(startTime, ClearDuration);
                for (var i = 0; i < views.Count; i++)
                {
                    CreateClearFlashOnce(views[i], progress);
                    CreateClearShardsOnce(views[i], clearedCells[i], progress);
                    var pulse = 1f + 0.18f * progress;
                    views[i].Rect.localScale = new Vector3(pulse, pulse, 1f);
                    views[i].CanvasGroup.alpha = 1f - progress;
                }

                if (progress >= 1f)
                {
                    break;
                }

                yield return null;
            }

            for (var i = 0; i < views.Count; i++)
            {
                views[i].Rect.localScale = Vector3.one;
                views[i].CanvasGroup.alpha = 1f;
                views[i].ClearFlashTriggered = false;
                views[i].ClearShardsTriggered = false;
            }
        }

        private IEnumerator AnimateFallAndRefill(Match3OperationContext operation, Match3CascadeStep step)
        {
            ClearOperationGhosts(operation);

            var stageHidden = new List<Vector2Int>(step.ClearedCells.Count + Rows * Columns);
            var hiddenMarked = new bool[Rows, Columns];
            for (var i = 0; i < step.ClearedCells.Count; i++)
            {
                AddUniqueCell(stageHidden, hiddenMarked, step.ClearedCells[i]);
            }

            for (var column = 0; column < Columns; column++)
            {
                var survivors = new List<ColumnMove>();
                var sourceRows = new List<int>();
                var targetRows = new List<int>();

                for (var row = Rows - 1; row >= 0; row--)
                {
                    if (step.BoardAfterClear[row, column] != 0)
                    {
                        sourceRows.Add(row);
                    }

                    if (step.BoardAfterCollapse[row, column] != 0)
                    {
                        targetRows.Add(row);
                    }
                }

                for (var i = 0; i < sourceRows.Count; i++)
                {
                    survivors.Add(new ColumnMove(sourceRows[i], targetRows[i], step.BoardAfterClear[sourceRows[i], column]));
                }

                for (var i = 0; i < survivors.Count; i++)
                {
                    if (survivors[i].SourceRow == survivors[i].TargetRow)
                    {
                        continue;
                    }

                    AddUniqueCell(stageHidden, hiddenMarked, new Vector2Int(column, survivors[i].SourceRow));
                    var ghost = CreateGhost(operation, survivors[i].Value, GetSlotCenter(survivors[i].SourceRow, column));
                    ghost.Target = GetSlotCenter(survivors[i].TargetRow, column);
                }

                var spawnRows = new List<int>();
                for (var row = 0; row < Rows; row++)
                {
                    if (step.BoardAfterCollapse[row, column] == 0 && step.BoardAfterRefill[row, column] != 0)
                    {
                        spawnRows.Add(row);
                    }
                }

                for (var i = 0; i < spawnRows.Count; i++)
                {
                    var spawnRow = spawnRows[i];
                    var start = GetSpawnStartPosition(column, spawnRows.Count - i);
                    var ghost = CreateGhost(operation, step.BoardAfterRefill[spawnRow, column], start);
                    ghost.Target = GetSlotCenter(spawnRow, column);
                }
            }

            SetOperationHiddenCells(operation, stageHidden);
            RefreshAllTiles();

            if (operation.Ghosts.Count > 0)
            {
                var movingGhosts = operation.Ghosts.ToArray();
                var targets = new Vector2[movingGhosts.Length];
                for (var i = 0; i < movingGhosts.Length; i++)
                {
                    targets[i] = movingGhosts[i].Target;
                }

                yield return AnimateGhostMove(movingGhosts, targets, FallDuration);
                yield return AnimateGhostScalePulse(movingGhosts, GhostSettleScale, GhostSettleDuration);
            }

            yield return AnimatePause(FallSettleDuration);

            ClearOperationGhosts(operation);
        }

        private IEnumerator AnimateBoardShuffleReveal()
        {
            var startTime = Time.realtimeSinceStartup;
            while (true)
            {
                var progress = GetRealtimeProgress(startTime, ShuffleFadeDuration);
                var alpha = 0.55f + 0.45f * progress;
                for (var i = 0; i < tileViews.Length; i++)
                {
                    tileViews[i].CanvasGroup.alpha = alpha;
                    tileViews[i].Rect.localScale = Vector3.one * (0.95f + progress * 0.05f);
                }

                if (progress >= 1f)
                {
                    break;
                }

                yield return null;
            }

            for (var i = 0; i < tileViews.Length; i++)
            {
                tileViews[i].CanvasGroup.alpha = 1f;
                tileViews[i].Rect.localScale = Vector3.one;
            }
        }

        private IEnumerator AnimateGhostMove(AnimationGhost[] movingGhosts, Vector2[] targets, float duration)
        {
            var starts = new Vector2[movingGhosts.Length];
            for (var i = 0; i < movingGhosts.Length; i++)
            {
                starts[i] = movingGhosts[i].Rect.anchoredPosition;
            }

            var startTime = Time.realtimeSinceStartup;
            while (true)
            {
                var progress = GetRealtimeProgress(startTime, duration);
                var eased = EaseInOutCubic(progress);
                for (var i = 0; i < movingGhosts.Length; i++)
                {
                    movingGhosts[i].Rect.anchoredPosition = Vector2.Lerp(starts[i], targets[i], eased);
                }

                if (progress >= 1f)
                {
                    break;
                }

                yield return null;
            }

            for (var i = 0; i < movingGhosts.Length; i++)
            {
                movingGhosts[i].Rect.anchoredPosition = targets[i];
            }
        }

        private IEnumerator AnimateGhostScalePulse(AnimationGhost[] ghosts, float peakScale, float duration)
        {
            if (ghosts == null || ghosts.Length == 0)
            {
                yield break;
            }

            var halfDuration = Mathf.Max(0.01f, duration * 0.5f);
            var elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / halfDuration);
                var scale = Mathf.Lerp(1f, peakScale, EaseOutCubic(t));
                SetGhostScale(ghosts, new Vector3(scale, scale, 1f));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / halfDuration);
                var scale = Mathf.Lerp(peakScale, 1f, EaseOutCubic(t));
                SetGhostScale(ghosts, new Vector3(scale, scale, 1f));
                yield return null;
            }

            SetGhostScale(ghosts, Vector3.one);
        }

        private IEnumerator AnimateGhostSquash(AnimationGhost[] ghosts, float squashX, float squashY, float duration)
        {
            if (ghosts == null || ghosts.Length == 0)
            {
                yield break;
            }

            var halfDuration = Mathf.Max(0.01f, duration * 0.5f);
            var elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / halfDuration);
                SetGhostScale(ghosts, Vector3.Lerp(Vector3.one, new Vector3(squashX, squashY, 1f), EaseOutCubic(t)));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / halfDuration);
                SetGhostScale(ghosts, Vector3.Lerp(new Vector3(squashX, squashY, 1f), Vector3.one, EaseOutCubic(t)));
                yield return null;
            }

            SetGhostScale(ghosts, Vector3.one);
        }

        private IEnumerator AnimatePause(float duration)
        {
            var deadline = Time.realtimeSinceStartup + duration;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        private static float EaseInOutCubic(float progress)
        {
            if (progress < 0.5f)
            {
                return 4f * progress * progress * progress;
            }

            var t = -2f * progress + 2f;
            return 1f - (t * t * t) * 0.5f;
        }

        private static float EaseOutCubic(float progress)
        {
            var t = 1f - progress;
            return 1f - t * t * t;
        }

        private void ShuffleBoardByPlayer()
        {
            if (isFinished || isPaused || isBusy)
            {
                return;
            }

            ClearHint();
            selectedTile = null;
            Match3BoardUtility.FillBoard(board, Rows, Columns, TileTypeCount);
            RefreshAllTiles();
            RefreshHud(UiTextCatalog.Get("match3.hud.player_shuffle"));
            MiniGameSfxPlayer.Play(MiniGameSfxType.Shuffle, 0.66f, UnityEngine.Random.Range(0.96f, 1.04f));
        }

        private void ShowHintByPlayer()
        {
            if (isFinished || isPaused || isBusy)
            {
                return;
            }

            selectedTile = null;
            ClearDragState();
            ClearHint();

            Vector2Int first;
            Vector2Int second;
            if (!Match3BoardUtility.TryFindPossibleSwap(board, Rows, Columns, out first, out second))
            {
                Match3BoardUtility.FillBoard(board, Rows, Columns, TileTypeCount);
            }

            if (!Match3BoardUtility.TryFindPossibleSwap(board, Rows, Columns, out first, out second))
            {
                RefreshAllTiles();
                return;
            }

            hintedFirstSwap = first;
            hintedSecondSwap = second;
            RefreshAllTiles();
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.52f, 1.06f);
        }

        private void ConfirmExitToHall()
        {
            SettleAndReturn();
        }

        private void SettleAndReturn()
        {
            if (isFinished)
            {
                return;
            }

            isFinished = true;
            isPaused = true;
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.76f, 1f);
            var finalScore = score;
            var coinCount = clearedTileCount;
            var chestCount = coinCount / 120;
            var settlement = new MiniGameSettlement
            {
                Score = finalScore,
                CoinCount = coinCount,
                ChestCount = chestCount,
                Summary = UiTextCatalog.Format("match3.settlement.summary", finalScore, coinCount, chestCount)
            };
            ShowBackHallRewardSettlementPanel(
                settlement,
                "Match3SettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("match3.settlement.score"), finalScore.ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("match3.settlement.cleared"), clearedTileCount.ToString()),
                delegate { completeGame(settlement); });
        }

        private void RefreshAllTiles()
        {
            RefreshBoardLayout();
            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    RefreshTile(tileViews[ToIndex(row, column)]);
                }
            }
        }

        private void RefreshTile(MatchTileView tile)
        {
            var value = board[tile.Row, tile.Column];
            var selected = selectedTile.HasValue &&
                           selectedTile.Value.x == tile.Column &&
                           selectedTile.Value.y == tile.Row;
            var hinted = IsHintedSwapTile(tile.Row, tile.Column);
            var hidden = IsCellHidden(tile.Row, tile.Column);
            var busy = IsCellBusy(tile.Row, tile.Column);

            tile.Rect.localScale = Vector3.one;
            tile.Button.interactable = !isFinished && !busy && !hidden && value != 0;
            tile.Graphic.raycastTarget = !isFinished && !busy && !hidden && value != 0;
            var cellSize = GetCurrentCellSize();
            tile.Graphic.CornerRadius = 0f;

            if (value == 0 || hidden)
            {
                tile.CanvasGroup.alpha = 0f;
                tile.Graphic.color = new Color(0f, 0f, 0f, 0f);
                tile.Icon.texture = null;
                tile.Icon.color = new Color(0f, 0f, 0f, 0f);
                return;
            }

            tile.CanvasGroup.alpha = 1f;
            tile.Rect.localScale = hinted ? Vector3.one * 1.12f : selected ? Vector3.one * 1.06f : Vector3.one;
            tile.Graphic.color = hinted ? new Color(1f, 0.92f, 0.45f, 0.24f) : new Color(0f, 0f, 0f, 0f);
            tile.Icon.texture = MiniGameIconCatalog.GetMatch3Texture(value);
            tile.Icon.color = new Color(1f, 1f, 1f, 1f);
        }

        private bool IsHintedSwapTile(int row, int column)
        {
            return hintedFirstSwap.HasValue &&
                   hintedSecondSwap.HasValue &&
                   ((hintedFirstSwap.Value.x == column && hintedFirstSwap.Value.y == row) ||
                    (hintedSecondSwap.Value.x == column && hintedSecondSwap.Value.y == row));
        }

        private void RefreshHud(string message)
        {
            scoreText.text = UiTextCatalog.Format("match3.hud.score", score);
        }

        protected override void OnPauseRequested()
        {
            OpenPausePopup();
        }

        protected override void OnBeforeDispose()
        {
            ClearTransientEffects();
            StopScorePulse();
            shell = null;
            root = null;
        }

        private void OpenPausePopup()
        {
            if (isFinished || isPaused)
            {
                return;
            }

            isPaused = true;
            selectedTile = null;
            ClearHint();
            ClearDragState();
            RefreshAllTiles();
            shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        private void ResumeFromPause()
        {
            if (isFinished)
            {
                return;
            }

            isPaused = false;
            shell.ClosePopup();
            ClearHint();
            RefreshAllTiles();
            RefreshHud(string.Empty);
        }

        private void ClearHint()
        {
            hintedFirstSwap = null;
            hintedSecondSwap = null;
        }

        private Vector2 GetSlotCenter(int row, int column)
        {
            RefreshBoardLayout();
            Canvas.ForceUpdateCanvases();
            return (Vector2)animationLayer.InverseTransformPoint(tileViews[ToIndex(row, column)].Rect.TransformPoint(tileViews[ToIndex(row, column)].Rect.rect.center));
        }

        private Vector2 GetSpawnStartPosition(int column, int distanceAboveBoard)
        {
            var topCenter = GetSlotCenter(0, column);
            var verticalStep = Rows > 1 ? GetSlotCenter(1, column).y - topCenter.y : -(GetCurrentCellSize().y + boardGridLayout.spacing.y);
            return new Vector2(topCenter.x, topCenter.y - verticalStep * distanceAboveBoard);
        }

        private Match3OperationContext BeginOperation(IList<Vector2Int> lockedCells)
        {
            var operation = new Match3OperationContext();
            operation.LockedCells.AddRange(lockedCells);
            LockCells(operation.LockedCells);
            activeOperationCount += 1;
            SyncBusyFlag();
            return operation;
        }

        private void EndOperation(Match3OperationContext operation)
        {
            ClearOperationGhosts(operation);
            ClearOperationHiddenCells(operation);
            UnlockCells(operation.LockedCells);
            activeOperationCount = Mathf.Max(0, activeOperationCount - 1);
            SyncBusyFlag();
            RefreshAllTiles();
        }

        private bool CanLockCells(IList<Vector2Int> cells)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                if (busyCounts[cells[i].y, cells[i].x] > 0)
                {
                    return false;
                }
            }

            return true;
        }

        private void LockCells(IList<Vector2Int> cells)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                busyCounts[cells[i].y, cells[i].x] += 1;
            }
        }

        private void UnlockCells(IList<Vector2Int> cells)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                busyCounts[cell.y, cell.x] = Mathf.Max(0, busyCounts[cell.y, cell.x] - 1);
            }
        }

        private bool IsCellBusy(int row, int column)
        {
            return busyCounts[row, column] > 0;
        }

        private bool IsCellHidden(int row, int column)
        {
            return hiddenCounts[row, column] > 0;
        }

        private bool IsCellInteractive(int row, int column)
        {
            return board[row, column] != 0 && !IsCellBusy(row, column) && !IsCellHidden(row, column);
        }

        private bool IsInsideBoard(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Columns && cell.y >= 0 && cell.y < Rows;
        }

        private void ClearDragState()
        {
            dragOriginTile = null;
            dragPointerId = int.MinValue;
            dragThresholdReached = false;
            dragSwapTriggered = false;
        }

        private static Vector2Int GetSwipeDirection(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                return delta.x >= 0f ? Vector2Int.right : Vector2Int.left;
            }

            return delta.y >= 0f ? new Vector2Int(0, -1) : new Vector2Int(0, 1);
        }

        private void SyncBusyFlag()
        {
            isBusy = activeOperationCount > 0;
        }

        private AnimationGhost CreateGhost(Match3OperationContext operation, int value, Vector2 position)
        {
            var rootObject = CreateGhostIcon();
            if (rootObject == null)
            {
                throw new InvalidOperationException("TileTemplate Icon missing when creating ghost tile.");
            }

            var rect = rootObject.GetComponent<RectTransform>();
            var cellSize = GetCurrentCellSize();
            var iconSize = cellSize * 0.96f;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.sizeDelta = iconSize;
            rect.anchoredPosition = position;

            var icon = rootObject.GetComponent<RawImage>();
            if (icon == null)
            {
                UnityEngine.Object.Destroy(rootObject);
                return null;
            }

            icon.texture = MiniGameIconCatalog.GetMatch3Texture(value);
            icon.color = Color.white;

            var ghost = new AnimationGhost
            {
                Root = rootObject,
                Rect = rect,
                Icon = icon,
                Target = position
            };
            operation.Ghosts.Add(ghost);
            return ghost;
        }

        private GameObject CreateGhostIcon()
        {
            var template = boardRoot != null ? boardRoot.Find("TileTemplate") as RectTransform : null;
            if (template == null)
            {
                return null;
            }

            var iconTemplate = template.Find("Icon") as RectTransform;
            if (iconTemplate == null)
            {
                return null;
            }

            var ghostTransform = UnityEngine.Object.Instantiate(iconTemplate, animationLayer, false);
            ghostTransform.gameObject.SetActive(true);
            ghostTransform.name = "GhostIcon";

            var canvasGroup = ghostTransform.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = ghostTransform.gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            canvasGroup.alpha = 1f;

            var icon = ghostTransform.GetComponent<RawImage>();
            if (icon != null)
            {
                icon.raycastTarget = false;
            }

            return ghostTransform.gameObject;
        }

        private void ClearOperationGhosts(Match3OperationContext operation)
        {
            for (var i = 0; i < operation.Ghosts.Count; i++)
            {
                if (operation.Ghosts[i].Root != null)
                {
                    UnityEngine.Object.Destroy(operation.Ghosts[i].Root);
                }
            }

            operation.Ghosts.Clear();
        }

        private void CreateClearFlashOnce(MatchTileView tile, float progress)
        {
            if (progress > 0.08f || tile.ClearFlashTriggered || tile.Icon == null || tile.Icon.texture == null)
            {
                return;
            }

            tile.ClearFlashTriggered = true;
            var flash = new GameObject("ClearFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            flash.transform.SetParent(tile.Icon.rectTransform, false);
            flash.transform.SetAsLastSibling();
            var rect = flash.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one * 1.04f;
            var graphic = flash.GetComponent<RawImage>();
            graphic.texture = tile.Icon.texture;
            graphic.color = ClearFlashColor;
            graphic.raycastTarget = false;
            transientEffects.Add(flash);
            host.StartCoroutine(FadeTransientGraphic(flash, graphic, ClearFlashDuration));

            var glow = new GameObject("ClearGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            glow.transform.SetParent(tile.Icon.rectTransform, false);
            glow.transform.SetAsFirstSibling();
            var glowRect = glow.GetComponent<RectTransform>();
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = Vector2.zero;
            glowRect.offsetMax = Vector2.zero;
            glowRect.localScale = Vector3.one * 1.16f;
            var glowGraphic = glow.GetComponent<RawImage>();
            glowGraphic.texture = tile.Icon.texture;
            glowGraphic.color = ClearGlowColor;
            glowGraphic.raycastTarget = false;
            transientEffects.Add(glow);
            host.StartCoroutine(FadeTransientGraphic(glow, glowGraphic, ClearFlashDuration));
        }

        private void CreateClearShardsOnce(MatchTileView tile, Vector2Int cell, float progress)
        {
            if (progress > 0.16f || tile.ClearShardsTriggered)
            {
                return;
            }

            tile.ClearShardsTriggered = true;
            var origin = GetSlotCenter(cell.y, cell.x);
            for (var i = 0; i < ClearShardCountPerTile; i++)
            {
                CreateClearShard(origin, i);
            }
        }

        private void CreateClearShard(Vector2 origin, int index)
        {
            var shard = new GameObject("ClearShard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shard.transform.SetParent(animationLayer, false);
            shard.transform.SetAsLastSibling();
            var rect = shard.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(8f, 8f);
            rect.anchoredPosition = origin;
            rect.localScale = Vector3.one;
            var image = shard.GetComponent<Image>();
            image.color = ClearShardColor;
            image.raycastTarget = false;
            transientEffects.Add(shard);

            var angle = index * (Mathf.PI * 2f / ClearShardCountPerTile) + UnityEngine.Random.Range(-0.22f, 0.22f);
            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * UnityEngine.Random.Range(14f, 26f);
            host.StartCoroutine(AnimateClearShard(shard, image, origin, origin + offset));
        }

        private IEnumerator AnimateClearShard(GameObject shard, Image image, Vector2 start, Vector2 end)
        {
            if (shard == null || image == null)
            {
                yield break;
            }

            var rect = shard.GetComponent<RectTransform>();
            var startColor = image.color;
            var startTime = Time.realtimeSinceStartup;
            while (true)
            {
                if (shard == null || image == null || rect == null)
                {
                    yield break;
                }

                var progress = GetRealtimeProgress(startTime, ClearShardDuration);
                rect.anchoredPosition = Vector2.Lerp(start, end, progress);
                rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.35f, progress);
                var color = startColor;
                color.a = Mathf.Lerp(startColor.a, 0f, progress);
                image.color = color;
                if (progress >= 1f)
                {
                    break;
                }

                yield return null;
            }

            transientEffects.Remove(shard);
            if (shard != null)
            {
                UnityEngine.Object.Destroy(shard);
            }
        }

        private IEnumerator FadeTransientGraphic(GameObject target, Graphic graphic, float duration)
        {
            if (target == null || graphic == null)
            {
                yield break;
            }

            var startColor = graphic.color;
            var startTime = Time.realtimeSinceStartup;
            while (true)
            {
                if (target == null || graphic == null)
                {
                    yield break;
                }

                var progress = GetRealtimeProgress(startTime, duration);
                var color = startColor;
                color.a = Mathf.Lerp(startColor.a, 0f, progress);
                graphic.color = color;
                if (progress >= 1f)
                {
                    break;
                }

                yield return null;
            }

            transientEffects.Remove(target);
            if (target != null)
            {
                UnityEngine.Object.Destroy(target);
            }
        }

        private void ShowComboPopup(int comboCount)
        {
            if (animationLayer == null)
            {
                return;
            }

            var popup = new GameObject("ComboPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            popup.transform.SetParent(animationLayer, false);
            popup.transform.SetAsLastSibling();
            var rect = popup.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, boardRoot.rect.height * 0.18f);
            rect.sizeDelta = new Vector2(220f, 80f);
            rect.localScale = Vector3.one;

            var text = popup.GetComponent<TextMeshProUGUI>();
            text.text = UiTextCatalog.Format("match3.hud.combo", comboCount);
            text.font = titleText != null ? titleText.font : null;
            text.fontSize = 34f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = ComboPopupColor;
            text.raycastTarget = false;

            transientEffects.Add(popup);
            host.StartCoroutine(AnimateComboPopup(popup, text, rect.anchoredPosition));
        }

        private IEnumerator AnimateComboPopup(GameObject popup, TextMeshProUGUI text, Vector2 startPosition)
        {
            if (popup == null || text == null)
            {
                yield break;
            }

            var rect = popup.GetComponent<RectTransform>();
            var startColor = text.color;
            var startTime = Time.realtimeSinceStartup;
            while (true)
            {
                if (popup == null || text == null || rect == null)
                {
                    yield break;
                }

                var progress = GetRealtimeProgress(startTime, ComboPopupDuration);
                rect.anchoredPosition = startPosition + new Vector2(0f, ComboPopupRiseDistance * progress);
                var scale = Mathf.Lerp(0.92f, 1.08f, 1f - Mathf.Abs(progress * 2f - 1f));
                rect.localScale = Vector3.one * scale;
                var color = startColor;
                color.a = Mathf.Lerp(startColor.a, 0f, progress);
                text.color = color;
                if (progress >= 1f)
                {
                    break;
                }

                yield return null;
            }

            transientEffects.Remove(popup);
            if (popup != null)
            {
                UnityEngine.Object.Destroy(popup);
            }
        }

        private void PlayScorePulse()
        {
            if (scoreText == null)
            {
                return;
            }

            StopScorePulse();
            scorePulseRoutine = host.StartCoroutine(AnimateScorePulse());
        }

        private IEnumerator AnimateScorePulse()
        {
            var rect = scoreText.rectTransform;
            var halfDuration = Mathf.Max(0.01f, ScorePulseDuration * 0.5f);
            var elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / halfDuration);
                var scale = Mathf.Lerp(1f, ScorePulseScale, EaseOutCubic(t));
                rect.localScale = Vector3.one * scale;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / halfDuration);
                var scale = Mathf.Lerp(ScorePulseScale, 1f, EaseOutCubic(t));
                rect.localScale = Vector3.one * scale;
                yield return null;
            }

            rect.localScale = Vector3.one;
            scorePulseRoutine = null;
        }

        private void StopScorePulse()
        {
            if (scorePulseRoutine != null)
            {
                host.StopCoroutine(scorePulseRoutine);
                scorePulseRoutine = null;
            }

            if (scoreText != null)
            {
                scoreText.rectTransform.localScale = Vector3.one;
            }
        }

        private void ClearTransientEffects()
        {
            for (var i = 0; i < transientEffects.Count; i++)
            {
                if (transientEffects[i] != null)
                {
                    UnityEngine.Object.Destroy(transientEffects[i]);
                }
            }

            transientEffects.Clear();
        }

        private static void SetGhostScale(AnimationGhost[] ghosts, Vector3 scale)
        {
            for (var i = 0; i < ghosts.Length; i++)
            {
                if (ghosts[i] != null && ghosts[i].Rect != null)
                {
                    ghosts[i].Rect.localScale = scale;
                }
            }
        }

        private void SetOperationHiddenCells(Match3OperationContext operation, IList<Vector2Int> cells)
        {
            ClearOperationHiddenCells(operation);
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                hiddenCounts[cell.y, cell.x] += 1;
                operation.HiddenCells.Add(cell);
            }
        }

        private void ClearOperationHiddenCells(Match3OperationContext operation)
        {
            for (var i = 0; i < operation.HiddenCells.Count; i++)
            {
                var cell = operation.HiddenCells[i];
                hiddenCounts[cell.y, cell.x] = Mathf.Max(0, hiddenCounts[cell.y, cell.x] - 1);
            }

            operation.HiddenCells.Clear();
        }

        private void RefreshBoardLayout()
        {
            Canvas.ForceUpdateCanvases();

            var rect = boardRoot.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var spacing = boardGridLayout.spacing;
            var padding = boardGridLayout.padding;
            var availableWidth = rect.width - padding.left - padding.right - spacing.x * (Columns - 1);
            var availableHeight = rect.height - padding.top - padding.bottom - spacing.y * (Rows - 1);
            if (availableWidth <= 0f || availableHeight <= 0f)
            {
                return;
            }

            var cellSize = Mathf.Floor(Mathf.Min(availableWidth / Columns, availableHeight / Rows));
            cellSize = Mathf.Clamp(cellSize, 44f, 86f);
            boardGridLayout.cellSize = new Vector2(cellSize, cellSize);
            RefreshBoardBackgroundLayout(cellSize);
        }

        private Vector2 GetCurrentCellSize()
        {
            RefreshBoardLayout();
            return boardGridLayout.cellSize;
        }

        private static void AddUniqueCell(List<Vector2Int> cells, bool[,] marked, Vector2Int cell)
        {
            if (marked[cell.y, cell.x])
            {
                return;
            }

            marked[cell.y, cell.x] = true;
            cells.Add(cell);
        }

        private static bool AreAdjacent(Vector2Int first, Vector2Int second)
        {
            return Mathf.Abs(first.x - second.x) + Mathf.Abs(first.y - second.y) == 1;
        }

        private static float GetRealtimeProgress(float startTime, float duration)
        {
            if (duration <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01((Time.realtimeSinceStartup - startTime) / duration);
        }

        private static int ToIndex(int row, int column)
        {
            return row * Columns + column;
        }

        private void RefreshBoardBackgroundLayout(float cellSize)
        {
            if (boardFrameRoot == null || boardRoot == null)
            {
                return;
            }

            var spacing = boardGridLayout.spacing;
            var padding = boardGridLayout.padding;
            var boardWidth = cellSize * Columns + spacing.x * (Columns - 1) + padding.left + padding.right;
            var boardHeight = cellSize * Rows + spacing.y * (Rows - 1) + padding.top + padding.bottom;
            var boardCenter = boardFrameRoot.InverseTransformPoint(boardRoot.TransformPoint(boardRoot.rect.center));

            ApplyBoardPanelLayout(boardShadowRect, boardCenter, new Vector2(boardWidth + 28f, boardHeight + 28f), new Vector2(0f, -4f));
            ApplyBoardPanelLayout(boardFrameLightRect, boardCenter, new Vector2(boardWidth + 48f, boardHeight + 48f), Vector2.zero);
        }

        private static void ApplyBoardPanelLayout(RectTransform panel, Vector2 center, Vector2 size, Vector2 offset)
        {
            if (panel == null)
            {
                return;
            }

            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = size;
            panel.anchoredPosition = center + offset;
        }

        private sealed class MatchTileView
        {
            public int Row;
            public int Column;
            public Button Button;
            public RectTransform Rect;
            public RoundedRectGraphic Graphic;
            public TextMeshProUGUI Label;
            public RawImage Icon;
            public CanvasGroup CanvasGroup;
            public bool ClearFlashTriggered;
            public bool ClearShardsTriggered;
        }

        private sealed class Match3OperationContext
        {
            public readonly List<Vector2Int> LockedCells = new List<Vector2Int>();
            public readonly List<Vector2Int> HiddenCells = new List<Vector2Int>();
            public readonly List<AnimationGhost> Ghosts = new List<AnimationGhost>();
        }

        private sealed class AnimationGhost
        {
            public GameObject Root;
            public RectTransform Rect;
            public RawImage Icon;
            public Vector2 Target;
        }

        private readonly struct ColumnMove
        {
            public ColumnMove(int sourceRow, int targetRow, int value)
            {
                SourceRow = sourceRow;
                TargetRow = targetRow;
                Value = value;
            }

            public int SourceRow { get; }
            public int TargetRow { get; }
            public int Value { get; }
        }
    }

}

