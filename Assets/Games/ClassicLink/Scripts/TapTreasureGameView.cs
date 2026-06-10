using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 连连看玩法运行时视图与交互控制器：负责棋盘渲染、选中消除、路径表现与结算。
    /// </summary>
    public sealed class TapTreasureGameView : MiniGameBase
    {
        public const string GameIdConstant = "classic-link";
        private const int Rows = 10;
        private const int Columns = 8;
        private const int MinimumAvailablePairs = 4;
        private const int TileTypeCount = 14;
        private const int TutorialVersion = 1;
        private const string LevelResourcePath = "Levels/classic-link.levels";
        private const float MinimumTileSize = 40f;
        private const float MaximumTileSize = 148f;
        private static readonly ClassicLinkLevelDefinition[] LevelDefinitions = LoadLevelDefinitions();

        public static int LevelCount
        {
            get { return LevelDefinitions.Length; }
        }

        private const float SelectBounceScale = 1.06f;
        private const float SelectBounceDuration = 0.12f;
        private const float CancelBounceScale = 0.97f;
        private const float CancelBounceDuration = 0.09f;
        private const float HintPulseScale = 1.03f;
        private const float HintPulseCycle = 0.72f;
        private const float HintPulseMinAlpha = 0.18f;
        private const float HintPulseMaxAlpha = 0.42f;
        private const float PathLineAlpha = 0.88f;
        private const float PathFadeDuration = 0.09f;
        private const float PathSweepDuration = 0.10f;
        private const float MatchResolveDelay = 0.18f;
        private const float MatchFlashDuration = 0.12f;
        private const int MatchShardCount = 4;
        private const float MatchShardDuration = 0.18f;
        private static readonly Color NormalTileColor = new Color(0.91f, 0.93f, 0.89f);
        private static readonly Color SelectedTileColor = new Color(0.73f, 0.82f, 0.73f);
        private static readonly Color HintedTileColor = new Color(0.98f, 0.89f, 0.60f);
        private static readonly Color SelectedHighlightColor = new Color(0.84f, 0.95f, 0.84f, 0.34f);
        private static readonly Color HintHighlightColor = new Color(1f, 0.95f, 0.73f, HintPulseMinAlpha);
        private static readonly Color PathLineColor = new Color(0.43f, 0.74f, 0.64f, PathLineAlpha);
        private static readonly Color PathSweepColor = new Color(0.92f, 1f, 0.95f, 0.96f);
        private static readonly Color MatchFlashColor = new Color(1f, 1f, 1f, 0.82f);
        private static readonly Color MatchShardColor = new Color(1f, 0.98f, 0.84f, 0.92f);

        private MonoBehaviour host;
        private Action<MiniGameSettlement> completeGame;
        private Action exitToHall;
        private MiniGameShell shell;
        private GameObject root;
        private RectTransform boardArea;
        private RectTransform boardGridRect;
        private RectTransform lineLayer;
        private RectTransform boardShadowRect;
        private RectTransform boardCardRect;
        private GridLayoutGroup boardGridLayout;
        private int layoutMinRow = 1;
        private int layoutMaxRow = Rows;
        private int layoutMinColumn = 1;
        private int layoutMaxColumn = Columns;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI scoreText;
        private TileView[] tileViews;
        private readonly List<PathSegmentVisual> lineSegments = new List<PathSegmentVisual>();
        private readonly List<GameObject> transientEffects = new List<GameObject>();
        private readonly int[,] board = new int[Rows + 2, Columns + 2];
        private MiniGameLevelProgressController levelProgress;
        private MiniGameLevelSelectView levelSelectView;
        private int currentLevelIndex;

        private int score;
        private bool isBusy;
        private bool isFinished;
        private bool isPaused;
        private TileCoord? selectedTile;
        private TileCoord? hintedFirstTile;
        private TileCoord? hintedSecondTile;
        private Coroutine activeMatchRoutine;
        private Coroutine activePathSweepRoutine;
        private Coroutine activePathFadeRoutine;
        private GameObject activePathSweepDot;
        private List<PathSegmentVisual> activeFadingPathSegments;

        public TapTreasureGameView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "ClassicLinkView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        private bool TryBuildRuntimeSections()
        {
            shell = Shell;
            root = shell.Root;

            var contentRoot = CreateContentSection(shell.ContentHost);
            var bottomContainerRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("ClassicLinkBottom"));

            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("ClassicLinkTop"));
            var topBar = topBarRefs.Root;
            boardArea = contentRoot.GetComponent<RectTransform>();
            var footer = bottomContainerRefs.Root;
            shell.AttachTop(topBar);
            shell.AttachContent(boardArea);
            shell.AttachBottom(footer);

            titleText = topBarRefs.TitleText;
            scoreText = topBarRefs.ScoreText;
            boardShadowRect = boardArea.Find("BoardShadow") as RectTransform;
            boardCardRect = boardArea.Find("BoardCardFull") as RectTransform;
            var boardGrid = boardArea.Find("BoardGrid") as RectTransform;
            lineLayer = boardArea.Find("LineLayer") as RectTransform;
            boardGridRect = boardGrid;
            boardGridLayout = boardGrid != null ? boardGrid.GetComponent<GridLayoutGroup>() : null;

            var shuffleButtonRefs = MiniGameShellBottomBarBuilder.CreateShuffleButton(bottomContainerRefs.ActionBar);
            var hintButtonRefs = MiniGameShellBottomBarBuilder.CreateHintButton(bottomContainerRefs.ActionBar);
            var levelButtonRefs = MiniGameShellBottomBarBuilder.CreateLevelSelectButton(bottomContainerRefs.ActionBar);

            if (titleText == null || scoreText == null || boardArea == null || boardGrid == null || lineLayer == null || boardGridLayout == null || shuffleButtonRefs.Button == null || hintButtonRefs.Button == null || levelButtonRefs.Button == null || shuffleButtonRefs.Icon == null || hintButtonRefs.Icon == null)
            {
                UnityEngine.Object.Destroy(root);
                root = null;
                return false;
            }

            shuffleButtonRefs.Button.onClick.RemoveAllListeners();
            shuffleButtonRefs.Button.onClick.AddListener(ShuffleBoardByPlayer);
            hintButtonRefs.Button.onClick.RemoveAllListeners();
            hintButtonRefs.Button.onClick.AddListener(ShowHintByPlayer);
            levelButtonRefs.Button.onClick.RemoveAllListeners();
            levelButtonRefs.Button.onClick.AddListener(OnLevelSelectClicked);
            RefreshStaticTexts();

            var template = boardGrid.Find("TileTemplate") as RectTransform;
            if (template == null)
            {
                template = boardGrid.Find("Tile_1_1") as RectTransform;
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
            var root = CreateRectObject("ClassicLinkContent", parent);
            Stretch(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            CreatePanel(
                root.transform,
                "BoardShadow",
                new Vector2(0.04f, 0.04f),
                new Vector2(0.96f, 0.96f),
                new Vector2(0f, -5f),
                new Color(0.31f, 0.42f, 0.26f, 0.08f),
                28f);

            CreatePanel(
                root.transform,
                "BoardCardFull",
                new Vector2(0.03f, 0.05f),
                new Vector2(0.97f, 0.97f),
                Vector2.zero,
                new Color(1f, 0.97f, 0.90f, 0.68f),
                30f);

            var boardGrid = CreateRectObject("BoardGrid", root.transform);
            var boardGridRect = boardGrid.GetComponent<RectTransform>();
            Stretch(boardGridRect, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero);
            var gridLayout = boardGrid.AddComponent<GridLayoutGroup>();
            gridLayout.childAlignment = TextAnchor.MiddleCenter;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.cellSize = new Vector2(40f, 40f);
            gridLayout.spacing = new Vector2(4f, 4f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = Columns;

            CreateTileTemplate(boardGrid.transform);

            var lineLayer = CreateRectObject("LineLayer", root.transform);
            Stretch(lineLayer.GetComponent<RectTransform>(), new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero);

            return root;
        }

        private static void CreateTileTemplate(Transform parent)
        {
            var tile = new GameObject("TileTemplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic), typeof(Button));
            tile.transform.SetParent(parent, false);

            var rect = tile.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            var graphic = tile.GetComponent<RoundedRectGraphic>();
            graphic.color = NormalTileColor;
            graphic.CornerRadius = 14f;

            var button = tile.GetComponent<Button>();
            button.targetGraphic = graphic;

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(RawImage));
            icon.transform.SetParent(tile.transform, false);
            Stretch(icon.GetComponent<RectTransform>(), new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f), Vector2.zero, Vector2.zero);
            var iconImage = icon.GetComponent<RawImage>();
            iconImage.color = new Color(1f, 1f, 1f, 0.96f);
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
            titleText.text = UiTextCatalog.Get("game.classic_link.name");
        }

        /// <summary>
        /// 每帧推进运行状态。
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
            completeGame = CompleteGame;
            exitToHall = ExitToHall;

            if (!TryBuildRuntimeSections())
            {
                throw new InvalidOperationException("ClassicLink runtime sections not found or invalid.");
            }
        }

        protected override void ResetGame()
        {
            EnsureLevelProgress();
            currentLevelIndex = levelProgress.CurrentLevelIndex;
            CloseLevelSelectView();
            CloseRewardSettlementPanel();
            score = 0;
            isBusy = false;
            isFinished = false;
            isPaused = false;
            selectedTile = null;
            ClearHint();
            ClearPath();
            BuildBoard();
            RefreshBoardLayout();
            RefreshAllTiles();
            RefreshHud(UiTextCatalog.Get("classic_link.hud.initial"));
            TryStartTutorial(TutorialVersion, CreateTutorialSteps());
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.classic_link.help", null);
        }

        private MiniGameTutorialStep[] CreateTutorialSteps()
        {
            return new[]
            {
                new MiniGameTutorialStep
                {
                    ResolveTarget = delegate { return ResolveTutorialTileTarget(0); },
                    TitleKey = "classic_link.tutorial.first_tile.title",
                    MessageKey = "classic_link.tutorial.first_tile",
                    RequireTargetClick = true,
                    OnTargetClick = delegate { ClickTutorialTile(0); },
                    Padding = new Vector2(16f, 16f)
                },
                new MiniGameTutorialStep
                {
                    ResolveTarget = delegate { return ResolveTutorialTileTarget(1); },
                    TitleKey = "classic_link.tutorial.second_tile.title",
                    MessageKey = "classic_link.tutorial.second_tile",
                    RequireTargetClick = true,
                    OnTargetClick = delegate { ClickTutorialTile(1); },
                    Padding = new Vector2(16f, 16f)
                }
            };
        }

        private RectTransform ResolveTutorialTileTarget(int pairIndex)
        {
            TileCoord first;
            TileCoord second;
            if (!TryGetHintPair(out first, out second))
            {
                return boardGridRect;
            }

            var coord = pairIndex == 0 ? first : second;
            var tile = tileViews != null ? tileViews[ToIndex(coord.Row, coord.Column)] : null;
            return tile != null ? tile.Root : boardGridRect;
        }

        private void ClickTutorialTile(int pairIndex)
        {
            TileCoord first;
            TileCoord second;
            if (!TryGetHintPair(out first, out second))
            {
                return;
            }

            var coord = pairIndex == 0 ? first : second;
            HandleTileClick(coord.Row, coord.Column);
        }

        private void BuildBoard()
        {
            var cells = LevelDefinitions[currentLevelIndex].Cells;
            Array.Clear(board, 0, board.Length);
            for (var row = 1; row <= Rows; row++)
            {
                for (var column = 1; column <= Columns; column++)
                {
                    board[row, column] = cells[row - 1, column - 1];
                }
            }

            RefreshInitialLayoutBounds(cells);
        }

        private TileView[] BuildTilesFromTemplate(RectTransform template)
        {
            var boardGrid = template.parent as RectTransform;
            if (boardGrid == null)
            {
                return null;
            }

            var staleChildren = new List<Transform>();
            for (var i = 0; i < boardGrid.childCount; i++)
            {
                var child = boardGrid.GetChild(i);
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

            var result = new TileView[Rows * Columns];
            for (var row = 1; row <= Rows; row++)
            {
                for (var column = 1; column <= Columns; column++)
                {
                    var tile = UnityEngine.Object.Instantiate(template, boardGrid, false);
                    tile.gameObject.SetActive(true);
                    tile.name = "Tile_" + row + "_" + column;

                    var button = tile.GetComponent<Button>();
                    var graphic = tile.GetComponent<RoundedRectGraphic>();
                    var label = tile.GetComponentInChildren<TextMeshProUGUI>(true);
                    var icon = tile.Find("Icon")?.GetComponent<RawImage>();
                    if (button == null || graphic == null || label == null || icon == null)
                    {
                        return null;
                    }

                    var captureRow = row;
                    var captureColumn = column;
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(delegate { HandleTileClick(captureRow, captureColumn); });

                    label.gameObject.SetActive(false);
                    var highlight = CreateTileHighlight(tile);
                    result[ToIndex(row, column)] = new TileView
                    {
                        Row = row,
                        Column = column,
                        Root = tile,
                        Button = button,
                        Graphic = graphic,
                        Label = label,
                        Icon = icon,
                        IconRect = icon.rectTransform,
                        Highlight = highlight
                    };
                }
            }

            return result;
        }

        private void HandleTileClick(int row, int column)
        {
            if (isFinished || isPaused || isBusy || board[row, column] == 0)
            {
                return;
            }

            ClearHint();
            var clicked = new TileCoord(row, column);
            if (!selectedTile.HasValue)
            {
                selectedTile = clicked;
                RefreshAllTiles();
                PlaySelectionBounce(clicked);
                MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.56f, UnityEngine.Random.Range(0.98f, 1.05f));
                RefreshHud(UiTextCatalog.Get("classic_link.hud.first_selected"));
                return;
            }

            if (selectedTile.Value.Row == row && selectedTile.Value.Column == column)
            {
                selectedTile = null;
                RefreshAllTiles();
                PlayCancelBounce(clicked);
                RefreshHud(UiTextCatalog.Get("classic_link.hud.cancel_selected"));
                MiniGameSfxPlayer.Play(MiniGameSfxType.UiBack, 0.45f, 1f);
                return;
            }

            if (board[selectedTile.Value.Row, selectedTile.Value.Column] != board[row, column])
            {
                selectedTile = clicked;
                RefreshAllTiles();
                PlaySelectionBounce(clicked);
                RefreshHud(UiTextCatalog.Get("classic_link.hud.pattern_mismatch"));
                MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.40f, 1.07f);
                return;
            }

            List<TileCoord> path;
            if (!TryFindPath(selectedTile.Value, clicked, out path))
            {
                selectedTile = clicked;
                RefreshAllTiles();
                PlaySelectionBounce(clicked);
                RefreshHud(UiTextCatalog.Get("classic_link.hud.path_blocked"));
                MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.62f, 1f);
                return;
            }

            activeMatchRoutine = host.StartCoroutine(ResolveMatchRoutine(selectedTile.Value, clicked, path));
        }

        private IEnumerator ResolveMatchRoutine(TileCoord first, TileCoord second, List<TileCoord> path)
        {
            isBusy = true;
            ClearHint();
            DrawPath(path);
            RefreshHud(UiTextCatalog.Get("classic_link.hud.match_success"));
            MiniGameSfxPlayer.Play(MiniGameSfxType.MatchSuccess, 0.72f, UnityEngine.Random.Range(0.97f, 1.03f));
            PlayMatchBurst(first);
            PlayMatchBurst(second);
            yield return new WaitForSeconds(MatchResolveDelay);

            board[first.Row, first.Column] = 0;
            board[second.Row, second.Column] = 0;
            score += 100;
            selectedTile = null;
            BeginPathFadeOut();
            var boardCleared = IsBoardCleared();
            string nextHint;

            if (boardCleared)
            {
                nextHint = UiTextCatalog.Get("classic_link.hud.board_cleared");
            }
            else if (!HasAvailableMove())
            {
                ReshuffleRemainingTiles();
                nextHint = UiTextCatalog.Get("classic_link.hud.auto_shuffle");
                MiniGameSfxPlayer.Play(MiniGameSfxType.Shuffle, 0.64f, 1f);
            }
            else
            {
                nextHint = UiTextCatalog.Get("classic_link.hud.next_pair");
            }

            isBusy = false;
            RefreshAllTiles();
            RefreshHud(nextHint);

            if (boardCleared)
            {
                SettleAndReturn();
            }

            activeMatchRoutine = null;
        }

        private void ShuffleBoardByPlayer()
        {
            if (isFinished || isPaused || isBusy)
            {
                return;
            }

            ClearHint();
            ReshuffleRemainingTiles();
            selectedTile = null;
            RefreshAllTiles();
            RefreshHud(UiTextCatalog.Get("classic_link.hud.player_shuffle"));
            MiniGameSfxPlayer.Play(MiniGameSfxType.Shuffle, 0.66f, UnityEngine.Random.Range(0.96f, 1.04f));
        }

        private void ShowHintByPlayer()
        {
            if (isFinished || isPaused || isBusy)
            {
                return;
            }

            selectedTile = null;
            ClearPath();
            ClearHint();

            TileCoord first;
            TileCoord second;
            if (!TryGetHintPair(out first, out second))
            {
                ReshuffleRemainingTiles();
            }

            if (!TryGetHintPair(out first, out second))
            {
                RefreshAllTiles();
                return;
            }

            hintedFirstTile = first;
            hintedSecondTile = second;
            RefreshAllTiles();
            StartHintPulseForTile(first);
            StartHintPulseForTile(second);
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.52f, 1.06f);
        }

        private void ConfirmExitToHall()
        {
            if (isFinished)
            {
                return;
            }

            shell.ClosePopup();
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
            var didClearBoard = IsBoardCleared();
            var coinCount = finalScore / 10;
            var chestCount = didClearBoard ? 1 : 0;
            var settlement = new MiniGameSettlement
            {
                Score = finalScore,
                CoinCount = coinCount,
                ChestCount = chestCount,
                Summary = didClearBoard
                    ? UiTextCatalog.Format("classic_link.settlement.win", finalScore, coinCount, chestCount)
                    : UiTextCatalog.Format("classic_link.settlement.exit", finalScore, coinCount)
            };

            if (didClearBoard)
            {
                EnsureLevelProgress();
                levelProgress.UnlockNext();
                ShowWinSettlement(settlement);
                return;
            }

            ShowBackHallRewardSettlementPanel(
                settlement,
                "ClassicLinkSettlementPanel",
                MiniGameSettlementInfoRow.CreateLevel(currentLevelIndex + 1),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("classic_link.settlement.score"), score.ToString()),
                delegate { completeGame(settlement); });
        }

        private void OnLevelSelectClicked()
        {
            EnsureLevelProgress();
            StopActiveMatchResolution();
            shell.ClosePopup();
            CloseRewardSettlementPanel();
            CloseLevelSelectView();
            levelSelectView = MiniGameLevelSelectView.Create(
                shell.PopupHost,
                titleText == null ? null : titleText.font,
                LevelDefinitions.Length,
                levelProgress.CurrentLevelIndex,
                levelProgress.UnlockedLevelCount,
                "ClassicLinkLevelSelectPanel",
                "ClassicLinkLevelButton_",
                SelectLevel,
                CloseLevelSelectView);
        }

        private void SelectLevel(int index)
        {
            EnsureLevelProgress();
            if (!levelProgress.Select(index))
            {
                return;
            }

            CloseLevelSelectView();
            ResetGame();
        }

        private void LoadNextLevel(MiniGameSettlement settlement)
        {
            EnsureLevelProgress();
            if (!levelProgress.GoNext())
            {
                completeGame(settlement);
                return;
            }

            GrantSettlementReward(settlement);
            ResetGame();
        }

        private void ShowWinSettlement(MiniGameSettlement settlement)
        {
            if (settlement == null)
            {
                return;
            }

            var level = LevelDefinitions[currentLevelIndex];
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "ClassicLinkSettlementPanel",
                    Title = UiTextCatalog.Get("classic_link.settlement.title"),
                    PrimaryInfo = MiniGameSettlementInfoRow.CreateLevel(currentLevelIndex + 1),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("classic_link.settlement.score"), score.ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.NextLevel,
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                delegate { LoadNextLevel(settlement); },
                delegate
                {
                    SaveNextLevelForReturn();
                    completeGame(settlement);
                },
                false);
        }

        private void SaveNextLevelForReturn()
        {
            EnsureLevelProgress();
            levelProgress.SaveNextAsCurrent();
        }

        private void CloseLevelSelectView()
        {
            if (levelSelectView != null)
            {
                levelSelectView.Dispose();
                levelSelectView = null;
            }
        }

        private void StopActiveMatchResolution()
        {
            if (activeMatchRoutine != null)
            {
                host.StopCoroutine(activeMatchRoutine);
                activeMatchRoutine = null;
            }

            isBusy = false;
            selectedTile = null;
            ClearHint();
            ClearPath();
            ClearAllTransientEffects();
            RefreshAllTiles();
        }

        private void EnsureLevelProgress()
        {
            if (levelProgress == null)
            {
                levelProgress = new MiniGameLevelProgressController(HostBehaviour, GameIdConstant, LevelDefinitions.Length);
            }
        }

        private bool IsBoardCleared()
        {
            for (var row = 1; row <= Rows; row++)
            {
                for (var column = 1; column <= Columns; column++)
                {
                    if (board[row, column] != 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void ReshuffleRemainingTiles()
        {
            ClearHint();
            ClassicLinkBoardUtility.ReshuffleRemainingTiles(board, Rows, Columns, MinimumAvailablePairs);
            RefreshAllTiles();
        }

        private bool TryGetHintPair(out TileCoord first, out TileCoord second)
        {
            for (var firstRow = 1; firstRow <= Rows; firstRow++)
            {
                for (var firstColumn = 1; firstColumn <= Columns; firstColumn++)
                {
                    var value = board[firstRow, firstColumn];
                    if (value == 0)
                    {
                        continue;
                    }

                    for (var secondRow = firstRow; secondRow <= Rows; secondRow++)
                    {
                        var secondColumnStart = secondRow == firstRow ? firstColumn + 1 : 1;
                        for (var secondColumn = secondColumnStart; secondColumn <= Columns; secondColumn++)
                        {
                            if (board[secondRow, secondColumn] != value)
                            {
                                continue;
                            }

                            List<TileCoord> path;
                            if (TryFindPath(new TileCoord(firstRow, firstColumn), new TileCoord(secondRow, secondColumn), out path))
                            {
                                first = new TileCoord(firstRow, firstColumn);
                                second = new TileCoord(secondRow, secondColumn);
                                return true;
                            }
                        }
                    }
                }
            }

            first = default(TileCoord);
            second = default(TileCoord);
            return false;
        }

        private bool HasAvailableMove()
        {
            return ClassicLinkBoardUtility.CountAvailablePairs(board, Rows, Columns) > 0;
        }

        private bool TryFindPath(TileCoord start, TileCoord target, out List<TileCoord> path)
        {
            List<Vector2Int> rawPath;
            if (!ClassicLinkBoardUtility.TryFindPath(
                    board,
                    Rows,
                    Columns,
                    new Vector2Int(start.Column, start.Row),
                    new Vector2Int(target.Column, target.Row),
                    out rawPath))
            {
                path = null;
                return false;
            }

            path = new List<TileCoord>(rawPath.Count);
            for (var i = 0; i < rawPath.Count; i++)
            {
                path.Add(new TileCoord(rawPath[i].y, rawPath[i].x));
            }

            return true;
        }

        private void RefreshAllTiles()
        {
            RefreshBoardLayout();
            for (var row = 1; row <= Rows; row++)
            {
                for (var column = 1; column <= Columns; column++)
                {
                    RefreshTile(tileViews[ToIndex(row, column)]);
                }
            }
        }

        private void RefreshTile(TileView tile)
        {
            var value = board[tile.Row, tile.Column];
            var isSelected = selectedTile.HasValue &&
                             selectedTile.Value.Row == tile.Row &&
                             selectedTile.Value.Column == tile.Column;
            var isHinted = IsHintedTile(tile.Row, tile.Column);

            tile.Button.interactable = !isBusy && value != 0;
            tile.Graphic.raycastTarget = value != 0;
            tile.Label.fontSize = Mathf.Clamp(boardGridLayout.cellSize.y * 0.44f, 22f, 34f);
            tile.Graphic.CornerRadius = Mathf.Clamp(boardGridLayout.cellSize.x * 0.26f, 14f, 20f);
            tile.Highlight.CornerRadius = tile.Graphic.CornerRadius;
            tile.Highlight.raycastTarget = false;

            if (value == 0)
            {
                tile.Graphic.color = new Color(0f, 0f, 0f, 0f);
                tile.Icon.texture = null;
                tile.Icon.color = new Color(0f, 0f, 0f, 0f);
                tile.Highlight.color = new Color(0f, 0f, 0f, 0f);
                ResetTileVisualScale(tile);
                return;
            }

            tile.Graphic.color = isSelected
                ? SelectedTileColor
                : isHinted ? HintedTileColor : NormalTileColor;
            tile.Icon.texture = MiniGameIconCatalog.GetClassicLinkTexture(value);
            tile.Icon.color = isSelected || isHinted ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0.96f);
            tile.Highlight.color = isSelected ? SelectedHighlightColor : isHinted ? HintHighlightColor : new Color(0f, 0f, 0f, 0f);
            if (!isHinted && !isSelected)
            {
                ResetTileVisualScale(tile);
            }
        }

        private bool IsHintedTile(int row, int column)
        {
            return hintedFirstTile.HasValue &&
                   hintedSecondTile.HasValue &&
                   ((hintedFirstTile.Value.Row == row && hintedFirstTile.Value.Column == column) ||
                    (hintedSecondTile.Value.Row == row && hintedSecondTile.Value.Column == column));
        }

        private void DrawPath(List<TileCoord> path)
        {
            ClearPath();
            if (path == null || path.Count < 2)
            {
                return;
            }

            for (var i = 0; i < path.Count - 1; i++)
            {
                CreateLineSegment(GetPointPosition(path[i]), GetPointPosition(path[i + 1]));
            }

            StartPathSweep(path);
        }

        private void ClearPath()
        {
            StopPathAnimations();
            for (var i = 0; i < lineSegments.Count; i++)
            {
                if (lineSegments[i] != null && lineSegments[i].Root != null)
                {
                    UnityEngine.Object.Destroy(lineSegments[i].Root);
                }
            }

            lineSegments.Clear();
        }

        private void BeginPathFadeOut()
        {
            StopPathSweep();
            if (lineSegments.Count == 0)
            {
                return;
            }

            var fadingSegments = new List<PathSegmentVisual>(lineSegments);
            lineSegments.Clear();
            if (activePathFadeRoutine != null)
            {
                host.StopCoroutine(activePathFadeRoutine);
            }

            DestroyPathSegments(activeFadingPathSegments);
            activeFadingPathSegments = fadingSegments;
            activePathFadeRoutine = host.StartCoroutine(FadeOutPathRoutine(fadingSegments));
        }

        private void CreateLineSegment(Vector2 start, Vector2 end)
        {
            var segment = new GameObject("Line", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            segment.transform.SetParent(lineLayer, false);

            var rect = segment.GetComponent<RectTransform>();
            var graphic = segment.GetComponent<RoundedRectGraphic>();
            graphic.color = PathLineColor;
            graphic.CornerRadius = 6f;
            graphic.raycastTarget = false;

            var delta = end - start;
            var length = delta.magnitude;
            rect.sizeDelta = new Vector2(length, 12f);
            rect.anchoredPosition = (start + end) * 0.5f;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            lineSegments.Add(new PathSegmentVisual
            {
                Root = segment,
                Graphic = graphic
            });
        }

        private Vector2 GetPointPosition(TileCoord tile)
        {
            Canvas.ForceUpdateCanvases();

            var clampedRow = Mathf.Clamp(tile.Row, layoutMinRow, layoutMaxRow);
            var clampedColumn = Mathf.Clamp(tile.Column, layoutMinColumn, layoutMaxColumn);
            var anchor = GetTileCenter(clampedRow, clampedColumn);
            var stepX = GetHorizontalStep();
            var stepY = GetVerticalStep();
            var point = new Vector2(
                anchor.x + (tile.Column - clampedColumn) * stepX,
                anchor.y + (tile.Row - clampedRow) * stepY);

            var bounds = lineLayer.rect;
            const float edgeInset = 6f;
            point.x = Mathf.Clamp(point.x, bounds.xMin + edgeInset, bounds.xMax - edgeInset);
            point.y = Mathf.Clamp(point.y, bounds.yMin + edgeInset, bounds.yMax - edgeInset);
            return point;
        }

        private Vector2 GetTileCenter(int row, int column)
        {
            var tileRect = tileViews[ToIndex(row, column)].Button.GetComponent<RectTransform>();
            return (Vector2)lineLayer.InverseTransformPoint(tileRect.TransformPoint(tileRect.rect.center));
        }

        private float GetHorizontalStep()
        {
            return GetTileCenter(1, 2).x - GetTileCenter(1, 1).x;
        }

        private float GetVerticalStep()
        {
            return GetTileCenter(2, 1).y - GetTileCenter(1, 1).y;
        }

        private void RefreshBoardLayout()
        {
            Canvas.ForceUpdateCanvases();

            if (boardGridRect == null || boardGridLayout == null)
            {
                return;
            }

            var rect = boardGridRect.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var spacing = boardGridLayout.spacing;
            var padding = boardGridLayout.padding;
            var layoutColumns = Mathf.Max(1, layoutMaxColumn - layoutMinColumn + 1);
            var layoutRows = Mathf.Max(1, layoutMaxRow - layoutMinRow + 1);
            var availableWidth = rect.width - padding.left - padding.right - spacing.x * (layoutColumns - 1);
            var availableHeight = rect.height - padding.top - padding.bottom - spacing.y * (layoutRows - 1);
            if (availableWidth <= 0f || availableHeight <= 0f)
            {
                return;
            }

            var cellSize = Mathf.Floor(Mathf.Min(availableWidth / layoutColumns, availableHeight / layoutRows));
            cellSize = Mathf.Clamp(cellSize, MinimumTileSize, MaximumTileSize);
            boardGridLayout.cellSize = new Vector2(cellSize, cellSize);
            boardGridLayout.enabled = false;
            ApplyTileLayout(cellSize, layoutRows, layoutColumns, spacing, padding);
            RefreshBoardBackgroundLayout(cellSize, layoutRows, layoutColumns);
        }

        private void ApplyTileLayout(float cellSize, int layoutRows, int layoutColumns, Vector2 spacing, RectOffset padding)
        {
            if (tileViews == null)
            {
                return;
            }

            var boardWidth = cellSize * layoutColumns + spacing.x * (layoutColumns - 1);
            var boardHeight = cellSize * layoutRows + spacing.y * (layoutRows - 1);
            var startX = -boardWidth * 0.5f + cellSize * 0.5f;
            var startY = boardHeight * 0.5f - cellSize * 0.5f;
            var offsetX = (padding.left - padding.right) * 0.5f;
            var offsetY = (padding.bottom - padding.top) * 0.5f;

            for (var row = 1; row <= Rows; row++)
            {
                for (var column = 1; column <= Columns; column++)
                {
                    var tile = tileViews[ToIndex(row, column)];
                    if (tile == null || tile.Root == null)
                    {
                        continue;
                    }

                    tile.Root.anchorMin = new Vector2(0.5f, 0.5f);
                    tile.Root.anchorMax = new Vector2(0.5f, 0.5f);
                    tile.Root.pivot = new Vector2(0.5f, 0.5f);
                    tile.Root.sizeDelta = new Vector2(cellSize, cellSize);

                    var layoutColumn = column - layoutMinColumn;
                    var layoutRow = row - layoutMinRow;
                    tile.Root.anchoredPosition = new Vector2(
                        startX + layoutColumn * (cellSize + spacing.x) + offsetX,
                        startY - layoutRow * (cellSize + spacing.y) + offsetY);
                }
            }
        }

        private void RefreshBoardBackgroundLayout(float cellSize, int layoutRows, int layoutColumns)
        {
            if (boardArea == null || boardGridRect == null)
            {
                return;
            }

            var spacing = boardGridLayout.spacing;
            var padding = boardGridLayout.padding;
            var boardWidth = cellSize * layoutColumns + spacing.x * (layoutColumns - 1) + padding.left + padding.right;
            var boardHeight = cellSize * layoutRows + spacing.y * (layoutRows - 1) + padding.top + padding.bottom;
            var boardCenter = boardArea.InverseTransformPoint(boardGridRect.TransformPoint(boardGridRect.rect.center));

            ApplyBoardPanelLayout(boardShadowRect, boardCenter, new Vector2(boardWidth + 28f, boardHeight + 28f), new Vector2(0f, -5f));
            ApplyBoardPanelLayout(boardCardRect, boardCenter, new Vector2(boardWidth + 48f, boardHeight + 48f), Vector2.zero);
        }

        private void RefreshInitialLayoutBounds(int[,] cells)
        {
            var minRow = Rows;
            var maxRow = 1;
            var minColumn = Columns;
            var maxColumn = 1;
            var hasTile = false;

            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    if (cells[row, column] == 0)
                    {
                        continue;
                    }

                    hasTile = true;
                    minRow = Mathf.Min(minRow, row + 1);
                    maxRow = Mathf.Max(maxRow, row + 1);
                    minColumn = Mathf.Min(minColumn, column + 1);
                    maxColumn = Mathf.Max(maxColumn, column + 1);
                }
            }

            layoutMinRow = hasTile ? minRow : 1;
            layoutMaxRow = hasTile ? maxRow : Rows;
            layoutMinColumn = hasTile ? minColumn : 1;
            layoutMaxColumn = hasTile ? maxColumn : Columns;
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

        private void RefreshHud(string message)
        {
            scoreText.text = UiTextCatalog.Format("classic_link.hud.score", score);
        }

        protected override void OnPauseRequested()
        {
            OpenPausePopup();
        }

        protected override void OnBeforeDispose()
        {
            CloseLevelSelectView();
            CloseRewardSettlementPanel();
            ClearPath();
            ClearAllTransientEffects();
            StopTileRoutines();
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
            ClearPath();
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
            StopHintPulse(hintedFirstTile);
            StopHintPulse(hintedSecondTile);
            hintedFirstTile = null;
            hintedSecondTile = null;
        }

        private RoundedRectGraphic CreateTileHighlight(RectTransform tile)
        {
            var highlight = new GameObject("Highlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            highlight.transform.SetParent(tile, false);
            highlight.transform.SetAsFirstSibling();
            var rect = highlight.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(3f, 3f);
            rect.offsetMax = new Vector2(-3f, -3f);

            var graphic = highlight.GetComponent<RoundedRectGraphic>();
            graphic.color = new Color(0f, 0f, 0f, 0f);
            graphic.raycastTarget = false;
            return graphic;
        }

        private void PlaySelectionBounce(TileCoord coord)
        {
            PlayTileBounce(coord, SelectBounceScale, SelectBounceDuration);
        }

        private void PlayCancelBounce(TileCoord coord)
        {
            PlayTileBounce(coord, CancelBounceScale, CancelBounceDuration);
        }

        private void PlayTileBounce(TileCoord coord, float peakScale, float duration)
        {
            var tile = tileViews[ToIndex(coord.Row, coord.Column)];
            if (tile == null || tile.Root == null)
            {
                return;
            }

            StopScaleRoutine(tile);
            tile.ScaleRoutine = host.StartCoroutine(AnimateTileBounceRoutine(tile, peakScale, duration));
        }

        private IEnumerator AnimateTileBounceRoutine(TileView tile, float peakScale, float duration)
        {
            var halfDuration = Mathf.Max(0.01f, duration * 0.5f);
            var upElapsed = 0f;
            while (upElapsed < halfDuration)
            {
                upElapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(upElapsed / halfDuration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                SetTileScale(tile, Mathf.Lerp(1f, peakScale, eased));
                yield return null;
            }

            var downElapsed = 0f;
            while (downElapsed < halfDuration)
            {
                downElapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(downElapsed / halfDuration);
                var eased = 1f - Mathf.Pow(1f - t, 2f);
                SetTileScale(tile, Mathf.Lerp(peakScale, 1f, eased));
                yield return null;
            }

            ResetTileVisualScale(tile);
            tile.ScaleRoutine = null;
        }

        private void StartHintPulseForTile(TileCoord coord)
        {
            var tile = tileViews[ToIndex(coord.Row, coord.Column)];
            if (tile == null)
            {
                return;
            }

            StopHintRoutine(tile);
            tile.HintPulseRoutine = host.StartCoroutine(HintPulseRoutine(tile));
        }

        private IEnumerator HintPulseRoutine(TileView tile)
        {
            var elapsed = 0f;
            while (IsHintedTile(tile.Row, tile.Column) && !isPaused && !isFinished)
            {
                elapsed += Time.unscaledDeltaTime;
                var cycle = Mathf.Repeat(elapsed / HintPulseCycle, 1f);
                var pulse = 0.5f - 0.5f * Mathf.Cos(cycle * Mathf.PI * 2f);
                var alpha = Mathf.Lerp(HintPulseMinAlpha, HintPulseMaxAlpha, pulse);
                var scale = Mathf.Lerp(1f, HintPulseScale, pulse);
                if (!IsSelectedTile(tile.Row, tile.Column))
                {
                    var color = HintHighlightColor;
                    color.a = alpha;
                    tile.Highlight.color = color;
                    SetTileScale(tile, scale);
                }

                yield return null;
            }

            if (!IsSelectedTile(tile.Row, tile.Column))
            {
                tile.Highlight.color = new Color(0f, 0f, 0f, 0f);
                ResetTileVisualScale(tile);
            }

            tile.HintPulseRoutine = null;
        }

        private bool IsSelectedTile(int row, int column)
        {
            return selectedTile.HasValue &&
                   selectedTile.Value.Row == row &&
                   selectedTile.Value.Column == column;
        }

        private void StartPathSweep(List<TileCoord> path)
        {
            StopPathSweep();
            if (path == null || path.Count < 2)
            {
                return;
            }

            var points = new List<Vector2>(path.Count);
            for (var i = 0; i < path.Count; i++)
            {
                points.Add(GetPointPosition(path[i]));
            }

            activePathSweepRoutine = host.StartCoroutine(PathSweepRoutine(points));
        }

        private IEnumerator PathSweepRoutine(List<Vector2> points)
        {
            activePathSweepDot = CreateSweepDot();
            var sweepRect = activePathSweepDot.GetComponent<RectTransform>();
            var totalLength = 0f;
            for (var i = 0; i < points.Count - 1; i++)
            {
                totalLength += Vector2.Distance(points[i], points[i + 1]);
            }

            if (totalLength <= 0.01f)
            {
                if (activePathSweepDot != null)
                {
                    UnityEngine.Object.Destroy(activePathSweepDot);
                    activePathSweepDot = null;
                }

                activePathSweepRoutine = null;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < PathSweepDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var distance = Mathf.Lerp(0f, totalLength, Mathf.Clamp01(elapsed / PathSweepDuration));
                sweepRect.anchoredPosition = EvaluatePathPosition(points, distance);
                yield return null;
            }

            sweepRect.anchoredPosition = points[points.Count - 1];
            if (activePathSweepDot != null)
            {
                UnityEngine.Object.Destroy(activePathSweepDot);
                activePathSweepDot = null;
            }

            activePathSweepRoutine = null;
        }

        private GameObject CreateSweepDot()
        {
            var dot = new GameObject("PathSweepDot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dot.transform.SetParent(lineLayer, false);
            dot.transform.SetAsLastSibling();
            var rect = dot.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(16f, 16f);
            var image = dot.GetComponent<Image>();
            image.color = PathSweepColor;
            image.raycastTarget = false;
            return dot;
        }

        private static Vector2 EvaluatePathPosition(IList<Vector2> points, float distance)
        {
            var remaining = distance;
            for (var i = 0; i < points.Count - 1; i++)
            {
                var segmentLength = Vector2.Distance(points[i], points[i + 1]);
                if (remaining <= segmentLength || i == points.Count - 2)
                {
                    var t = segmentLength <= 0.001f ? 1f : Mathf.Clamp01(remaining / segmentLength);
                    return Vector2.Lerp(points[i], points[i + 1], t);
                }

                remaining -= segmentLength;
            }

            return points[points.Count - 1];
        }

        private IEnumerator FadeOutPathRoutine(List<PathSegmentVisual> fadingSegments)
        {
            var elapsed = 0f;
            while (elapsed < PathFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var alpha = Mathf.Lerp(PathLineAlpha, 0f, Mathf.Clamp01(elapsed / PathFadeDuration));
                for (var i = 0; i < fadingSegments.Count; i++)
                {
                    if (fadingSegments[i] == null || fadingSegments[i].Graphic == null)
                    {
                        continue;
                    }

                    var color = fadingSegments[i].Graphic.color;
                    color.a = alpha;
                    fadingSegments[i].Graphic.color = color;
                }

                yield return null;
            }

            for (var i = 0; i < fadingSegments.Count; i++)
            {
                if (fadingSegments[i] != null && fadingSegments[i].Root != null)
                {
                    UnityEngine.Object.Destroy(fadingSegments[i].Root);
                }
            }

            if (ReferenceEquals(activeFadingPathSegments, fadingSegments))
            {
                activeFadingPathSegments = null;
            }

            activePathFadeRoutine = null;
        }

        private void PlayMatchBurst(TileCoord coord)
        {
            var tile = tileViews[ToIndex(coord.Row, coord.Column)];
            if (tile == null || tile.Root == null)
            {
                return;
            }

            CreateMatchFlash(tile);
            for (var i = 0; i < MatchShardCount; i++)
            {
                CreateMatchShard(coord, i);
            }
        }

        private void CreateMatchFlash(TileView tile)
        {
            var flash = new GameObject("MatchFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            flash.transform.SetParent(tile.Root, false);
            flash.transform.SetAsLastSibling();
            var rect = flash.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, -2f);
            var graphic = flash.GetComponent<RoundedRectGraphic>();
            graphic.color = MatchFlashColor;
            graphic.CornerRadius = tile.Graphic.CornerRadius;
            graphic.raycastTarget = false;
            transientEffects.Add(flash);
            host.StartCoroutine(FadeAndDestroyGraphicRoutine(flash, graphic, MatchFlashDuration));
        }

        private void CreateMatchShard(TileCoord coord, int shardIndex)
        {
            var shard = new GameObject("MatchShard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shard.transform.SetParent(lineLayer, false);
            shard.transform.SetAsLastSibling();
            var rect = shard.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(8f, 8f);
            rect.anchoredPosition = GetTileCenter(coord.Row, coord.Column);
            rect.localRotation = Quaternion.Euler(0f, 0f, shardIndex * (360f / MatchShardCount));

            var image = shard.GetComponent<Image>();
            image.color = MatchShardColor;
            image.raycastTarget = false;
            transientEffects.Add(shard);

            var angle = shardIndex * (Mathf.PI * 2f / MatchShardCount) + UnityEngine.Random.Range(-0.18f, 0.18f);
            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * UnityEngine.Random.Range(14f, 24f);
            host.StartCoroutine(AnimateShardRoutine(shard, image, rect.anchoredPosition, rect.anchoredPosition + offset));
        }

        private IEnumerator FadeAndDestroyGraphicRoutine(GameObject target, Graphic graphic, float duration)
        {
            if (target == null || graphic == null)
            {
                yield break;
            }

            var startColor = graphic.color;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (target == null || graphic == null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                var color = startColor;
                color.a = Mathf.Lerp(startColor.a, 0f, Mathf.Clamp01(elapsed / duration));
                graphic.color = color;
                yield return null;
            }

            transientEffects.Remove(target);
            if (target != null)
            {
                UnityEngine.Object.Destroy(target);
            }
        }

        private IEnumerator AnimateShardRoutine(GameObject target, Image image, Vector2 start, Vector2 end)
        {
            if (target == null || image == null)
            {
                yield break;
            }

            var rect = target.GetComponent<RectTransform>();
            var elapsed = 0f;
            while (elapsed < MatchShardDuration)
            {
                if (target == null || image == null || rect == null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / MatchShardDuration);
                rect.anchoredPosition = Vector2.Lerp(start, end, t);
                rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, t);
                var color = MatchShardColor;
                color.a = Mathf.Lerp(MatchShardColor.a, 0f, t);
                image.color = color;
                yield return null;
            }

            transientEffects.Remove(target);
            if (target != null)
            {
                UnityEngine.Object.Destroy(target);
            }
        }

        private void SetTileScale(TileView tile, float scale)
        {
            if (tile.IconRect != null)
            {
                tile.IconRect.localScale = Vector3.one * scale;
            }

            if (tile.Highlight != null)
            {
                tile.Highlight.rectTransform.localScale = Vector3.one * scale;
            }
        }

        private void ResetTileVisualScale(TileView tile)
        {
            SetTileScale(tile, 1f);
        }

        private void StopHintPulse(TileCoord? coord)
        {
            if (!coord.HasValue)
            {
                return;
            }

            StopHintRoutine(tileViews[ToIndex(coord.Value.Row, coord.Value.Column)]);
        }

        private void StopHintRoutine(TileView tile)
        {
            if (tile == null || tile.HintPulseRoutine == null)
            {
                return;
            }

            host.StopCoroutine(tile.HintPulseRoutine);
            tile.HintPulseRoutine = null;
            if (!IsSelectedTile(tile.Row, tile.Column))
            {
                tile.Highlight.color = new Color(0f, 0f, 0f, 0f);
                ResetTileVisualScale(tile);
            }
        }

        private void StopScaleRoutine(TileView tile)
        {
            if (tile == null || tile.ScaleRoutine == null)
            {
                return;
            }

            host.StopCoroutine(tile.ScaleRoutine);
            tile.ScaleRoutine = null;
            if (!IsHintedTile(tile.Row, tile.Column))
            {
                ResetTileVisualScale(tile);
            }
        }

        private void StopPathAnimations()
        {
            StopPathSweep();
            if (activePathFadeRoutine != null)
            {
                host.StopCoroutine(activePathFadeRoutine);
                activePathFadeRoutine = null;
            }

            DestroyPathSegments(activeFadingPathSegments);
            activeFadingPathSegments = null;
        }

        private void StopPathSweep()
        {
            if (activePathSweepRoutine != null)
            {
                host.StopCoroutine(activePathSweepRoutine);
                activePathSweepRoutine = null;
            }

            if (activePathSweepDot != null)
            {
                UnityEngine.Object.Destroy(activePathSweepDot);
                activePathSweepDot = null;
            }
        }

        private void ClearAllTransientEffects()
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

        private static void DestroyPathSegments(List<PathSegmentVisual> segments)
        {
            if (segments == null)
            {
                return;
            }

            for (var i = 0; i < segments.Count; i++)
            {
                if (segments[i] != null && segments[i].Root != null)
                {
                    UnityEngine.Object.Destroy(segments[i].Root);
                }
            }
        }

        private void StopTileRoutines()
        {
            if (tileViews == null)
            {
                return;
            }

            for (var i = 0; i < tileViews.Length; i++)
            {
                StopHintRoutine(tileViews[i]);
                StopScaleRoutine(tileViews[i]);
            }
        }

        private static int ToIndex(int row, int column)
        {
            return (row - 1) * Columns + (column - 1);
        }

        private readonly struct TileCoord
        {
            public TileCoord(int row, int column)
            {
                Row = row;
                Column = column;
            }

            public int Row { get; }
            public int Column { get; }
        }

        private sealed class TileView
        {
            public int Row;
            public int Column;
            public RectTransform Root;
            public Button Button;
            public RoundedRectGraphic Graphic;
            public TextMeshProUGUI Label;
            public RawImage Icon;
            public RectTransform IconRect;
            public RoundedRectGraphic Highlight;
            public Coroutine ScaleRoutine;
            public Coroutine HintPulseRoutine;
        }

        private sealed class PathSegmentVisual
        {
            public GameObject Root;
            public RoundedRectGraphic Graphic;
        }

        private sealed class ClassicLinkLevelDefinition
        {
            public ClassicLinkLevelDefinition(int[,] cells)
            {
                Cells = cells;
            }

            public int[,] Cells { get; }
        }

        private static ClassicLinkLevelDefinition[] LoadLevelDefinitions()
        {
            var asset = Resources.Load<TextAsset>(LevelResourcePath);
            if (asset == null)
            {
                throw new InvalidOperationException("未找到连连看关卡配置: Resources/" + LevelResourcePath);
            }

            ClassicLinkLevelCatalog catalog;
            try
            {
                catalog = JsonUtility.FromJson<ClassicLinkLevelCatalog>(asset.text);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("解析连连看关卡配置失败: " + exception.Message, exception);
            }

            if (catalog == null || catalog.levels == null || catalog.levels.Length == 0)
            {
                throw new InvalidOperationException("连连看关卡配置为空: Resources/" + LevelResourcePath);
            }

            var levels = new ClassicLinkLevelDefinition[catalog.levels.Length];
            for (var i = 0; i < catalog.levels.Length; i++)
            {
                levels[i] = new ClassicLinkLevelDefinition(ParseLevelCells(catalog.levels[i], i));
            }

            return levels;
        }

        private static int[,] ParseLevelCells(ClassicLinkLevelEntry entry, int levelIndex)
        {
            if (entry == null || entry.rows == null || entry.rows.Length != Rows)
            {
                throw new InvalidOperationException("连连看关卡行数错误: " + (levelIndex + 1));
            }

            var cells = new int[Rows, Columns];
            var valueCounts = new int[TileTypeCount + 1];
            for (var row = 0; row < Rows; row++)
            {
                var rowData = entry.rows[row];
                if (rowData == null || rowData.cells == null || rowData.cells.Length != Columns)
                {
                    throw new InvalidOperationException("连连看关卡列数错误: " + (levelIndex + 1) + "-" + (row + 1));
                }

                for (var column = 0; column < Columns; column++)
                {
                    var value = rowData.cells[column];
                    if (value < 0 || value > TileTypeCount)
                    {
                        throw new InvalidOperationException("连连看关卡图标值越界: " + (levelIndex + 1));
                    }

                    cells[row, column] = value;
                    if (value != 0)
                    {
                        valueCounts[value] += 1;
                    }
                }
            }

            var nonEmptyTileCount = 0;
            for (var value = 1; value < valueCounts.Length; value++)
            {
                nonEmptyTileCount += valueCounts[value];
                if (valueCounts[value] % 2 != 0)
                {
                    throw new InvalidOperationException("连连看关卡图标数量必须成对: " + (levelIndex + 1));
                }
            }

            if (nonEmptyTileCount <= 0 || nonEmptyTileCount % 2 != 0)
            {
                throw new InvalidOperationException("连连看关卡非空格数量必须为正偶数: " + (levelIndex + 1));
            }

            var boardProbe = new int[Rows + 2, Columns + 2];
            for (var row = 1; row <= Rows; row++)
            {
                for (var column = 1; column <= Columns; column++)
                {
                    boardProbe[row, column] = cells[row - 1, column - 1];
                }
            }

            if (ClassicLinkBoardUtility.CountAvailablePairs(boardProbe, Rows, Columns) < 1)
            {
                throw new InvalidOperationException("连连看关卡初始可消除对不足: " + (levelIndex + 1));
            }

            return cells;
        }

        [Serializable]
        private sealed class ClassicLinkLevelCatalog
        {
            public ClassicLinkLevelEntry[] levels;
        }

        [Serializable]
        private sealed class ClassicLinkLevelEntry
        {
            public ClassicLinkLevelRow[] rows;
        }

        [Serializable]
        private sealed class ClassicLinkLevelRow
        {
            public int[] cells;
        }
    }
}

