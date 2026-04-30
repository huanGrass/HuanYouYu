using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 贪吃蛇运行时视图：负责移动、食物、碰撞、暂停与结算。
    /// </summary>
    public sealed class SnakeGameView : MiniGameBase
    {
        public const string GameIdConstant = "snake";
        private const string ContentPrefabResourcePath = "SnakeContent";

        private const int Rows = 18;
        private const int Columns = 14;
        private const int InitialSnakeLength = 3;
        private const float BaseMoveInterval = 0.42f;
        private const float MinMoveInterval = 0.16f;
        private const float MaxMoveInterval = 0.60f;
        private const float ManualSpeedStep = 0.04f;
        private const float BottomTrayInset = 192f;
        private const float BottomTrayLift = 40f;
        private const float BoardSurfacePadding = 18f;
        private const float HeadPulseDuration = 0.14f;
        private const float FoodPulseCycle = 0.92f;
        private const float EdgeFlashDuration = 0.18f;
        private const float EatFlashDuration = 0.20f;
        private const float EatShardDuration = 0.24f;
        private const int EatShardCount = 6;
        private const float CollisionFlashDuration = 0.10f;
        private const float CollisionSettlementDelay = 0.10f;
        private const float WinTrailStepDelay = 0.03f;
        private const float WinFlashDuration = 0.12f;
        private const float WinSettlementDelay = 0.08f;
        private static readonly Color EmptyCellColor = new Color(1f, 1f, 1f, 0f);
        private static readonly Color SnakeHeadColor = new Color(0.27f, 0.58f, 0.30f, 1f);
        private static readonly Color SnakeBodyColor = new Color(0.49f, 0.67f, 0.30f, 1f);
        private static readonly Color FoodColor = new Color(0.98f, 0.63f, 0.16f, 1f);
        private static readonly Color SnakeFaceColor = new Color(0.12f, 0.20f, 0.10f, 0.96f);
        private static readonly Color EdgeFlashColor = new Color(1f, 0.95f, 0.72f, 0.82f);
        private static readonly Color EatFlashColor = new Color(1f, 0.93f, 0.72f, 0.86f);
        private static readonly Color CollisionFlashColor = new Color(1f, 0.55f, 0.28f, 0.88f);
        private static readonly Color WinFlashColor = new Color(1f, 0.96f, 0.74f, 0.78f);
        private static readonly Vector3 EmptyCellScale = Vector3.one * 0.52f;
        private static readonly Vector3 SnakeBodyScale = Vector3.one * 0.72f;
        private static readonly Vector3 SnakeHeadScale = Vector3.one * 0.82f;
        private static readonly Vector3 FoodScale = Vector3.one * 0.58f;

        private Action<MiniGameSettlement> completeGame;
        private Action exitToHall;
        private readonly List<Vector2Int> snakeSegments = new List<Vector2Int>();
        private readonly int[,] board = new int[Rows, Columns];

        private MiniGameShell shell;
        private GameObject root;
        private RectTransform boardRoot;
        private RectTransform boardSurface;
        private RectTransform boardPlayfield;
        private RectTransform boardAnimationLayer;
        private GridLayoutGroup boardGridLayout;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI scoreText;
        private TextMeshProUGUI lengthText;
        private TextMeshProUGUI speedText;
        private SnakeCellView[] cellViews;
        private readonly List<GameObject> transientEffects = new List<GameObject>();

        private SnakeDirection currentDirection = SnakeDirection.Right;
        private SnakeDirection? queuedDirection;
        private Vector2Int foodCell;
        private float moveTimer;
        private float currentMoveInterval = BaseMoveInterval;
        private int score;
        private int foodCount;
        private bool isFinished;
        private bool isPaused;
        private Coroutine foodPulseRoutine;
        private Coroutine settlementRoutine;

        public SnakeGameView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "SnakeView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        /// <summary>
        /// 每帧推进蛇的移动计时。
        /// </summary>
        public override void Tick(float deltaTime)
        {
            if (isFinished || isPaused)
            {
                return;
            }

            moveTimer += deltaTime;
            while (moveTimer >= currentMoveInterval && !isFinished && !isPaused)
            {
                moveTimer -= currentMoveInterval;
                StepSnake();
            }
        }

        protected override MiniGameShellLayout CreateShellLayout()
        {
            return new MiniGameShellLayout(
                MiniGameShellLayout.DefaultTopInset,
                BottomTrayInset,
                MiniGameShellBottomMode.DefaultSlot);
        }

        protected override void BuildOrBindSections()
        {
            completeGame = CompleteGame;
            exitToHall = ExitToHall;
            shell = Shell;
            root = shell.Root;

            var topRoot = CreateTopSection(shell.TopHost);
            var contentRoot = CreateSection(ContentPrefabResourcePath, shell.ContentHost, "SnakeContent", BuildContentSection);
            var bottomRoot = CreateRuntimeSection(shell.BottomHost, "SnakeBottom", BuildBottomSection);

            shell.AttachTop(topRoot.transform);
            shell.AttachContent(contentRoot.transform);
            shell.AttachBottom(bottomRoot.transform);

            BindSectionReferences(topRoot, contentRoot, bottomRoot);
        }

        private GameObject CreateSection(string resourcePath, Transform parent, string instanceName, Action<Transform> buildSection)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            GameObject instance;
            if (prefab != null)
            {
                instance = UnityEngine.Object.Instantiate(prefab, parent, false);
                instance.name = instanceName;
            }
            else
            {
                instance = new GameObject(instanceName, typeof(RectTransform));
                instance.transform.SetParent(parent, false);
            }

            var rect = instance.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = instance.AddComponent<RectTransform>();
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;

            ClearChildren(instance.transform);
            buildSection(instance.transform);
            return instance;
        }

        private GameObject CreateTopSection(Transform parent)
        {
            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                parent,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("SnakeTop"));
            var header = topBarRefs.Root.Find("Header") as RectTransform;
            if (header == null)
            {
                throw new InvalidOperationException("Snake top header was not created.");
            }

            var headerLayout = header.GetComponent<VerticalLayoutGroup>();
            if (headerLayout != null)
            {
                headerLayout.padding = new RectOffset(22, 22, 14, 12);
            }

            topBarRefs.TitleText.fontSize = 34f;
            topBarRefs.TitleText.text = UiTextCatalog.Get("game.snake.name");

            topBarRefs.ScoreText.fontSize = 23f;
            topBarRefs.ScoreText.text = UiTextCatalog.Format("snake.hud.score", 0);

            var length = CreateText("Length", UiTextCatalog.Format("snake.hud.length", 3), 20, FontStyles.Bold, TextAlignmentOptions.Center);
            length.transform.SetParent(header, false);
            length.color = new Color(0.46f, 0.33f, 0.17f, 1f);

            return topBarRefs.Root.gameObject;
        }

        private static GameObject CreateRuntimeSection(Transform parent, string instanceName, Action<Transform> buildSection)
        {
            var instance = new GameObject(instanceName, typeof(RectTransform));
            instance.transform.SetParent(parent, false);

            var rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;

            buildSection(instance.transform);
            return instance;
        }

        private void BindSectionReferences(GameObject topRoot, GameObject contentRoot, GameObject bottomRoot)
        {
            titleText = topRoot.transform.Find("Header/Title")?.GetComponent<TextMeshProUGUI>();
            scoreText = topRoot.transform.Find("Header/Score")?.GetComponent<TextMeshProUGUI>();
            lengthText = topRoot.transform.Find("Header/Length")?.GetComponent<TextMeshProUGUI>();
            speedText = bottomRoot.transform.Find("ActionTray/SpeedDisplay/Value")?.GetComponent<TextMeshProUGUI>();
            boardSurface = contentRoot.transform.Find("BoardFrame/BoardSurface") as RectTransform;
            boardPlayfield = contentRoot.transform.Find("BoardFrame/BoardSurface/BoardPlayfield") as RectTransform;
            boardRoot = contentRoot.transform.Find("BoardFrame/BoardSurface/BoardPlayfield/BoardGrid") as RectTransform;
            boardAnimationLayer = contentRoot.transform.Find("BoardFrame/BoardSurface/BoardAnimationLayer") as RectTransform;
            boardGridLayout = boardRoot != null ? boardRoot.GetComponent<GridLayoutGroup>() : null;

            if (titleText == null || scoreText == null || lengthText == null || speedText == null || boardSurface == null || boardPlayfield == null || boardRoot == null || boardAnimationLayer == null || boardGridLayout == null)
            {
                throw new InvalidOperationException("Snake section structure is incomplete.");
            }

            cellViews = BuildCellsFromTemplate();
            RefreshStaticTexts();
        }

        private void BuildContentSection(Transform rootTransform)
        {
            var shadow = CreatePanel(
                "BoardShadow",
                rootTransform,
                new Vector2(0.10f, 0.08f),
                new Vector2(0.90f, 0.94f),
                new Vector2(0f, -4f),
                new Color(0.31f, 0.42f, 0.26f, 0.09f),
                30f);
            shadow.transform.SetAsFirstSibling();

            var frame = CreatePanel(
                "BoardFrame",
                rootTransform,
                new Vector2(0.08f, 0.06f),
                new Vector2(0.92f, 0.96f),
                Vector2.zero,
                new Color(1f, 0.985f, 0.94f, 0.18f),
                32f);

            var surface = CreatePanel(
                "BoardSurface",
                frame.transform,
                new Vector2(0.05f, 0.05f),
                new Vector2(0.95f, 0.95f),
                Vector2.zero,
                new Color(0.78f, 0.85f, 0.73f, 0.82f),
                26f);
            surface.GetComponent<RoundedRectGraphic>().raycastTarget = false;

            var playfield = CreatePanel(
                "BoardPlayfield",
                surface.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Color(0.92f, 0.96f, 0.87f, 0.72f),
                24f);
            var playfieldRect = playfield.GetComponent<RectTransform>();
            playfieldRect.anchorMin = new Vector2(0.5f, 0.5f);
            playfieldRect.anchorMax = new Vector2(0.5f, 0.5f);
            playfieldRect.pivot = new Vector2(0.5f, 0.5f);
            playfieldRect.sizeDelta = new Vector2(420f, 540f);
            playfield.GetComponent<RoundedRectGraphic>().raycastTarget = false;

            var animationLayer = new GameObject("BoardAnimationLayer", typeof(RectTransform));
            animationLayer.transform.SetParent(surface.transform, false);
            var animationLayerRect = animationLayer.GetComponent<RectTransform>();
            Stretch(animationLayerRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var boardGrid = new GameObject("BoardGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            boardGrid.transform.SetParent(playfield.transform, false);
            var gridRect = boardGrid.GetComponent<RectTransform>();
            Stretch(gridRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var gridLayout = boardGrid.GetComponent<GridLayoutGroup>();
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = Columns;
            gridLayout.spacing = Vector2.zero;
            gridLayout.padding = new RectOffset(0, 0, 0, 0);
            gridLayout.childAlignment = TextAnchor.MiddleCenter;

            var template = new GameObject("SnakeCellTemplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic), typeof(LayoutElement));
            template.transform.SetParent(boardGrid.transform, false);
            template.SetActive(false);
            template.GetComponent<RectTransform>().sizeDelta = new Vector2(44f, 44f);
            var templateGraphic = template.GetComponent<RoundedRectGraphic>();
            templateGraphic.color = EmptyCellColor;
            templateGraphic.CornerRadius = 12f;
            var layoutElement = template.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 44f;
            layoutElement.preferredHeight = 44f;
        }

        private void BuildBottomSection(Transform rootTransform)
        {
            var trayShadow = CreatePanel(
                "TrayShadow",
                rootTransform,
                new Vector2(0.24f, 0.06f),
                new Vector2(0.80f, 0.94f),
                new Vector2(0f, -4f),
                new Color(0.31f, 0.42f, 0.26f, 0.10f),
                64f);
            trayShadow.transform.SetAsFirstSibling();

            var tray = CreatePanel(
                "ActionTray",
                rootTransform,
                new Vector2(0.12f, 0.02f),
                new Vector2(0.88f, 0.98f),
                new Vector2(0f, BottomTrayLift),
                new Color(1f, 0.98f, 0.92f, 0.66f),
                60f);

            var directionPadConfig = MiniGameDirectionPadBuilder.Config.Default;
            directionPadConfig.UpAction = delegate { RequestDirection(SnakeDirection.Up); };
            directionPadConfig.DownAction = delegate { RequestDirection(SnakeDirection.Down); };
            directionPadConfig.LeftAction = delegate { RequestDirection(SnakeDirection.Left); };
            directionPadConfig.RightAction = delegate { RequestDirection(SnakeDirection.Right); };
            MiniGameDirectionPadBuilder.Create(tray.transform, directionPadConfig);

            var speedPanel = new GameObject("SpeedPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            speedPanel.transform.SetParent(tray.transform, false);
            var speedPanelRect = speedPanel.GetComponent<RectTransform>();
            Stretch(speedPanelRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-114f, -88f), new Vector2(-18f, 88f));

            var speedPanelGraphic = speedPanel.GetComponent<RoundedRectGraphic>();
            speedPanelGraphic.color = new Color(1f, 1f, 1f, 0.30f);
            speedPanelGraphic.CornerRadius = 34f;
            speedPanelGraphic.raycastTarget = false;

            var speedDisplay = new GameObject("SpeedDisplay", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            speedDisplay.transform.SetParent(tray.transform, false);
            var speedDisplayRect = speedDisplay.GetComponent<RectTransform>();
            Stretch(speedDisplayRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(64f, -34f), new Vector2(152f, 34f));

            var speedDisplayGraphic = speedDisplay.GetComponent<RoundedRectGraphic>();
            speedDisplayGraphic.color = new Color(1f, 1f, 1f, 0.52f);
            speedDisplayGraphic.CornerRadius = 26f;
            speedDisplayGraphic.raycastTarget = false;

            var speedValue = CreateText("Value", "1.0x", 20, FontStyles.Bold, TextAlignmentOptions.Center);
            speedValue.transform.SetParent(speedDisplay.transform, false);
            speedValue.color = new Color(0.31f, 0.42f, 0.26f, 1f);
            Stretch(speedValue.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            CreateSpeedButton(speedPanel.transform, "FasterButton", "+", new Vector2(0f, 40f), -ManualSpeedStep);
            CreateSpeedButton(speedPanel.transform, "SlowerButton", "-", new Vector2(0f, -40f), ManualSpeedStep);
        }

        private Button CreateSpeedButton(Transform parent, string name, string label, Vector2 anchoredPosition, float delta)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(72f, 72f);
            rect.anchoredPosition = anchoredPosition;

            var graphic = buttonObject.GetComponent<RoundedRectGraphic>();
            graphic.color = new Color(1f, 1f, 1f, 0.95f);
            graphic.CornerRadius = 36f;

            var text = CreateText("Label", label, 28, FontStyles.Bold, TextAlignmentOptions.Center);
            text.transform.SetParent(buttonObject.transform, false);
            text.color = new Color(0.31f, 0.42f, 0.26f, 1f);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = graphic;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(delegate { AdjustSpeed(delta); });
            MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.9f);
            return button;
        }

        private SnakeCellView[] BuildCellsFromTemplate()
        {
            var template = boardRoot.Find("SnakeCellTemplate") as RectTransform;
            if (template == null)
            {
                throw new InvalidOperationException("SnakeCellTemplate not found.");
            }

            var result = new SnakeCellView[Rows * Columns];
            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    var cell = UnityEngine.Object.Instantiate(template, boardRoot, false);
                    cell.gameObject.SetActive(true);
                    cell.name = "SnakeCell_" + row + "_" + column;
                    var faceRoot = CreateHeadFaceRoot(cell);
                    result[(row * Columns) + column] = new SnakeCellView
                    {
                        Row = row,
                        Column = column,
                        Rect = cell,
                        Graphic = cell.GetComponent<RoundedRectGraphic>(),
                        FaceRoot = faceRoot,
                        LeftEye = CreateFacePart(faceRoot, "LeftEye", new Vector2(-8f, 8f), new Vector2(8f, 10f), 4f),
                        RightEye = CreateFacePart(faceRoot, "RightEye", new Vector2(8f, 8f), new Vector2(8f, 10f), 4f)
                    };
                }
            }

            template.gameObject.SetActive(false);
            return result;
        }

        protected override void ResetGame()
        {
            StopFoodPulse();
            StopSettlementRoutine();
            StopAllCellPulseRoutines();
            ClearTransientEffects();
            isFinished = false;
            isPaused = false;
            score = 0;
            foodCount = 0;
            moveTimer = 0f;
            currentMoveInterval = BaseMoveInterval;
            currentDirection = SnakeDirection.Right;
            queuedDirection = null;
            snakeSegments.Clear();
            ClearBoard();

            var startRow = Rows / 2;
            var startColumn = Mathf.Max(2, Columns / 2 - 1);
            for (var i = 0; i < InitialSnakeLength; i++)
            {
                var cell = new Vector2Int(startColumn + i, startRow);
                snakeSegments.Add(cell);
                board[cell.y, cell.x] = 1;
            }

            if (!SpawnFood())
            {
                HandleWin();
                return;
            }

            RefreshAllCells();
            RefreshHud();
            RestartFoodPulse();
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.snake.help", null);
        }

        private void StepSnake()
        {
            if (isFinished || isPaused)
            {
                return;
            }

            if (queuedDirection.HasValue && !SnakeBoardUtility.IsOpposite(currentDirection, queuedDirection.Value))
            {
                currentDirection = queuedDirection.Value;
            }

            queuedDirection = null;

            var head = snakeSegments[snakeSegments.Count - 1];
            var candidateHead = SnakeBoardUtility.Step(head, currentDirection);
            var nextHead = WrapPosition(candidateHead);

            var willEat = nextHead == foodCell;
            var tail = snakeSegments[0];
            var hitsBody = board[nextHead.y, nextHead.x] == 1 && (willEat || nextHead != tail);
            if (hitsBody)
            {
                HandleCollision("snake.settlement.self", nextHead);
                return;
            }

            if (!willEat)
            {
                board[tail.y, tail.x] = 0;
                snakeSegments.RemoveAt(0);
                PlayTailGhost(tail);
            }

            snakeSegments.Add(nextHead);
            board[nextHead.y, nextHead.x] = 1;

            if (candidateHead != nextHead)
            {
                PlayWrapFlash(head, nextHead);
            }

            if (willEat)
            {
                score += 10;
                foodCount += 1;
                MiniGameSfxPlayer.Play(MiniGameSfxType.MatchSuccess, 0.55f, UnityEngine.Random.Range(0.96f, 1.04f));
                PlayEatBurst(nextHead);
                if (!SpawnFood())
                {
                    HandleWin();
                    return;
                }

                RestartFoodPulse();
            }

            RefreshAllCells();
            RefreshHud();
            PlayHeadPulse(nextHead);
        }

        private void HandleCollision(string reasonKey, Vector2Int collisionCell)
        {
            if (isFinished)
            {
                return;
            }

            isFinished = true;
            isPaused = true;
            MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.76f, 1f);
            StopFoodPulse();
            settlementRoutine = HostBehaviour.StartCoroutine(PlayCollisionSettlementRoutine(reasonKey, collisionCell));
        }

        private void HandleWin()
        {
            if (isFinished)
            {
                return;
            }

            isFinished = true;
            isPaused = true;
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.82f, 1f);
            StopFoodPulse();
            settlementRoutine = HostBehaviour.StartCoroutine(PlayWinSettlementRoutine());
        }

        private void ShowSettlement(string reasonKey)
        {
            var finalScore = score;
            var coinCount = foodCount * 5;
            var settlement = new MiniGameSettlement
            {
                Score = finalScore,
                CoinCount = coinCount,
                ChestCount = coinCount / 120,
                Summary = UiTextCatalog.Format(reasonKey, finalScore, snakeSegments.Count, foodCount, coinCount, coinCount / 120)
            };

            var isWin = string.Equals(reasonKey, "snake.settlement.win", StringComparison.Ordinal);
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "SnakeSettlementPanel",
                    Style = isWin ? MiniGameRewardSettlementPanelStyle.Success : MiniGameRewardSettlementPanelStyle.Failure,
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.Retry,
                    Title = UiTextCatalog.Get(isWin ? "snake.settlement.win_title" : "snake.settlement.failure_title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("snake.settlement.score"), finalScore.ToString()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("snake.settlement.length"), snakeSegments.Count.ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                ResetGame,
                delegate { completeGame(settlement); },
                true);
        }

        private void ShowExitSettlement()
        {
            var finalScore = score;
            var coinCount = foodCount * 5;
            var summary = UiTextCatalog.Format("snake.settlement.exit", finalScore, snakeSegments.Count, foodCount, coinCount, coinCount / 120);

            var settlement = new MiniGameSettlement
            {
                Score = finalScore,
                CoinCount = coinCount,
                ChestCount = coinCount / 120,
                Summary = summary
            };

            ShowBackHallRewardSettlementPanel(
                settlement,
                "SnakeSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("snake.settlement.score"), finalScore.ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("snake.settlement.length"), snakeSegments.Count.ToString()),
                delegate { completeGame(settlement); });
        }

        private bool SpawnFood()
        {
            var emptyCells = new List<Vector2Int>();
            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    if (board[row, column] == 0)
                    {
                        emptyCells.Add(new Vector2Int(column, row));
                    }
                }
            }

            if (emptyCells.Count == 0)
            {
                return false;
            }

            foodCell = emptyCells[UnityEngine.Random.Range(0, emptyCells.Count)];
            board[foodCell.y, foodCell.x] = 2;
            return true;
        }

        private void RefreshAllCells()
        {
            RefreshBoardLayout();
            for (var i = 0; i < cellViews.Length; i++)
            {
                var cell = cellViews[i];
                var value = board[cell.Row, cell.Column];
                var isHead = snakeSegments.Count > 0 && snakeSegments[snakeSegments.Count - 1].x == cell.Column && snakeSegments[snakeSegments.Count - 1].y == cell.Row;

                cell.Graphic.color = value == 1
                    ? isHead ? SnakeHeadColor : SnakeBodyColor
                    : value == 2
                        ? FoodColor
                        : EmptyCellColor;

                cell.Rect.localScale = value == 1
                    ? isHead ? SnakeHeadScale : SnakeBodyScale
                    : value == 2
                        ? FoodScale
                        : EmptyCellScale;

                var showHeadFace = value == 1 && isHead;
                if (cell.FaceRoot != null)
                {
                    cell.FaceRoot.gameObject.SetActive(showHeadFace);
                    if (showHeadFace)
                    {
                        cell.FaceRoot.localRotation = Quaternion.Euler(0f, 0f, GetHeadRotation(currentDirection));
                    }
                }

                if (cell.LeftEye != null)
                {
                    cell.LeftEye.gameObject.SetActive(showHeadFace);
                }

                if (cell.RightEye != null)
                {
                    cell.RightEye.gameObject.SetActive(showHeadFace);
                }
            }
        }

        private void RefreshHud()
        {
            if (scoreText != null)
            {
                scoreText.text = UiTextCatalog.Format("snake.hud.score", score);
            }

            if (lengthText != null)
            {
                lengthText.text = UiTextCatalog.Format("snake.hud.length", snakeSegments.Count);
            }

            if (speedText != null)
            {
                speedText.text = GetSpeedDisplayText();
            }
        }

        private void RefreshStaticTexts()
        {
            if (titleText != null)
            {
                titleText.text = UiTextCatalog.Get("game.snake.name");
            }
        }

        private void RefreshBoardLayout()
        {
            Canvas.ForceUpdateCanvases();
            if (boardRoot == null || boardGridLayout == null)
            {
                return;
            }

            var surfaceRect = boardSurface.rect;
            if (surfaceRect.width <= 0f || surfaceRect.height <= 0f)
            {
                return;
            }

            var maxBoardWidth = surfaceRect.width - (BoardSurfacePadding * 2f);
            var maxBoardHeight = surfaceRect.height - (BoardSurfacePadding * 2f);
            if (maxBoardWidth <= 0f || maxBoardHeight <= 0f)
            {
                return;
            }

            var rect = boardRoot.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                rect = new Rect(0f, 0f, maxBoardWidth, maxBoardHeight);
            }

            var spacing = boardGridLayout.spacing;
            var padding = boardGridLayout.padding;
            var availableWidth = maxBoardWidth - padding.left - padding.right - spacing.x * (Columns - 1);
            var availableHeight = maxBoardHeight - padding.top - padding.bottom - spacing.y * (Rows - 1);
            if (availableWidth <= 0f || availableHeight <= 0f)
            {
                return;
            }

            var cellSize = Mathf.Floor(Mathf.Min(availableWidth / Columns, availableHeight / Rows));
            cellSize = Mathf.Clamp(cellSize, 28f, 70f);
            boardGridLayout.cellSize = new Vector2(cellSize, cellSize);
            boardPlayfield.sizeDelta = new Vector2(
                (cellSize * Columns) + padding.left + padding.right,
                (cellSize * Rows) + padding.top + padding.bottom);
        }

        protected override void OnPauseRequested()
        {
            OpenPausePopup();
        }

        protected override void OnBeforeDispose()
        {
            StopFoodPulse();
            StopSettlementRoutine();
            StopAllCellPulseRoutines();
            ClearTransientEffects();
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
            queuedDirection = null;
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
            RefreshHud();
        }

        private void ConfirmExitToHall()
        {
            if (isFinished)
            {
                return;
            }

            isFinished = true;
            isPaused = true;
            shell.ClosePopup();
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            ShowExitSettlement();
        }

        private void RequestDirection(SnakeDirection direction)
        {
            if (isFinished || isPaused)
            {
                return;
            }

            if (direction == currentDirection || (queuedDirection.HasValue && queuedDirection.Value == direction))
            {
                return;
            }

            if (snakeSegments.Count > 1 && SnakeBoardUtility.IsOpposite(currentDirection, direction))
            {
                return;
            }

            queuedDirection = direction;
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.56f, 1.03f);
        }

        private void AdjustSpeed(float delta)
        {
            if (isFinished || isPaused)
            {
                return;
            }

            currentMoveInterval = Mathf.Clamp(currentMoveInterval + delta, MinMoveInterval, MaxMoveInterval);
            RefreshHud();
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.56f, 1.03f);
        }

        private string GetSpeedDisplayText()
        {
            var multiplier = BaseMoveInterval / currentMoveInterval;
            return multiplier.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "x";
        }

        private void ClearBoard()
        {
            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    board[row, column] = 0;
                }
            }
        }

        private static Vector2Int WrapPosition(Vector2Int cell)
        {
            var wrappedX = (cell.x + Columns) % Columns;
            var wrappedY = (cell.y + Rows) % Rows;
            return new Vector2Int(wrappedX, wrappedY);
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offset, Color color, float cornerRadius)
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
            return panel;
        }

        private static TextMeshProUGUI CreateText(string name, string content, int fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = MiniGameFontProvider.DefaultFont;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void ClearChildren(Transform root)
        {
            var children = new List<GameObject>();
            for (var i = 0; i < root.childCount; i++)
            {
                children.Add(root.GetChild(i).gameObject);
            }

            for (var i = 0; i < children.Count; i++)
            {
                UnityEngine.Object.Destroy(children[i]);
            }
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static RectTransform CreateHeadFaceRoot(RectTransform parent)
        {
            var faceRootObject = new GameObject("FaceRoot", typeof(RectTransform));
            faceRootObject.transform.SetParent(parent, false);
            var faceRoot = faceRootObject.GetComponent<RectTransform>();
            faceRoot.anchorMin = new Vector2(0.5f, 0.5f);
            faceRoot.anchorMax = new Vector2(0.5f, 0.5f);
            faceRoot.pivot = new Vector2(0.5f, 0.5f);
            faceRoot.sizeDelta = new Vector2(28f, 28f);
            faceRoot.anchoredPosition = Vector2.zero;
            faceRoot.gameObject.SetActive(false);
            return faceRoot;
        }

        private static RoundedRectGraphic CreateFacePart(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, float cornerRadius)
        {
            var partObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            partObject.transform.SetParent(parent, false);

            var rect = partObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            var graphic = partObject.GetComponent<RoundedRectGraphic>();
            graphic.color = SnakeFaceColor;
            graphic.CornerRadius = cornerRadius;
            graphic.raycastTarget = false;
            partObject.SetActive(false);
            return graphic;
        }

        private static float GetHeadRotation(SnakeDirection direction)
        {
            switch (direction)
            {
                case SnakeDirection.Up:
                    return 90f;
                case SnakeDirection.Left:
                    return 180f;
                case SnakeDirection.Down:
                    return 270f;
                default:
                    return 0f;
            }
        }

        private void PlayHeadPulse(Vector2Int cell)
        {
            var cellView = GetCellView(cell);
            if (cellView == null || cellView.Rect == null || HostBehaviour == null)
            {
                return;
            }

            StopPulseRoutine(cellView);
            cellView.PulseRoutine = HostBehaviour.StartCoroutine(AnimateCellScalePulse(cellView, SnakeHeadScale, 1.18f, HeadPulseDuration));
        }

        private void RestartFoodPulse()
        {
            StopFoodPulse();
            var cellView = GetCellView(foodCell);
            if (cellView == null || cellView.Rect == null || HostBehaviour == null)
            {
                return;
            }

            foodPulseRoutine = HostBehaviour.StartCoroutine(AnimateFoodPulse(cellView));
        }

        private void StopFoodPulse()
        {
            if (foodPulseRoutine != null && HostBehaviour != null)
            {
                HostBehaviour.StopCoroutine(foodPulseRoutine);
                foodPulseRoutine = null;
            }
        }

        private void StopSettlementRoutine()
        {
            if (settlementRoutine != null && HostBehaviour != null)
            {
                HostBehaviour.StopCoroutine(settlementRoutine);
                settlementRoutine = null;
            }
        }

        private void StopAllCellPulseRoutines()
        {
            if (cellViews == null)
            {
                return;
            }

            for (var i = 0; i < cellViews.Length; i++)
            {
                StopPulseRoutine(cellViews[i]);
            }
        }

        private void StopPulseRoutine(SnakeCellView cell)
        {
            if (cell == null || cell.PulseRoutine == null || HostBehaviour == null)
            {
                return;
            }

            HostBehaviour.StopCoroutine(cell.PulseRoutine);
            cell.PulseRoutine = null;
        }

        private SnakeCellView GetCellView(Vector2Int cell)
        {
            if (cellViews == null || cell.x < 0 || cell.x >= Columns || cell.y < 0 || cell.y >= Rows)
            {
                return null;
            }

            return cellViews[(cell.y * Columns) + cell.x];
        }

        private IEnumerator AnimateCellScalePulse(SnakeCellView cell, Vector3 baseScale, float peakMultiplier, float duration)
        {
            if (cell == null || cell.Rect == null)
            {
                yield break;
            }

            var peakScale = baseScale * peakMultiplier;
            var halfDuration = duration * 0.5f;
            var elapsed = 0f;
            while (elapsed < halfDuration)
            {
                if (cell.Rect == null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / halfDuration);
                cell.Rect.localScale = Vector3.LerpUnclamped(baseScale, peakScale, progress);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                if (cell.Rect == null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / halfDuration);
                cell.Rect.localScale = Vector3.LerpUnclamped(peakScale, baseScale, progress);
                yield return null;
            }

            if (cell.Rect != null)
            {
                cell.Rect.localScale = baseScale;
            }

            cell.PulseRoutine = null;
        }

        private IEnumerator AnimateFoodPulse(SnakeCellView cell)
        {
            while (!isFinished && cell != null && cell.Rect != null)
            {
                var cycle = Mathf.Repeat(Time.unscaledTime / FoodPulseCycle, 1f);
                var wave = 0.5f - 0.5f * Mathf.Cos(cycle * Mathf.PI * 2f);
                cell.Rect.localScale = Vector3.LerpUnclamped(FoodScale, FoodScale * 1.12f, wave);
                yield return null;
            }

            foodPulseRoutine = null;
        }

        private void PlayTailGhost(Vector2Int cell)
        {
            var ghost = CreateCellEffectGraphic("SnakeTailGhost", cell, SnakeBodyColor, 12f, SnakeBodyScale);
            if (ghost == null || HostBehaviour == null)
            {
                return;
            }

            HostBehaviour.StartCoroutine(AnimateFadeScaleAndDestroy(ghost, SnakeBodyScale, SnakeBodyScale * 0.62f, 0.18f));
        }

        private void PlayEatBurst(Vector2Int cell)
        {
            var flash = CreateCellEffectGraphic("SnakeEatFlash", cell, EatFlashColor, 16f, FoodScale * 1.12f);
            if (flash != null && HostBehaviour != null)
            {
                HostBehaviour.StartCoroutine(AnimateFadeScaleAndDestroy(flash, flash.localScale, flash.localScale * 1.34f, EatFlashDuration));
            }

            for (var i = 0; i < EatShardCount; i++)
            {
                CreateEatShard(cell, i);
            }
        }

        private void CreateEatShard(Vector2Int cell, int shardIndex)
        {
            if (boardAnimationLayer == null || HostBehaviour == null)
            {
                return;
            }

            var shard = new GameObject("SnakeEatShard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shard.transform.SetParent(boardAnimationLayer, false);
            shard.transform.SetAsLastSibling();
            TrackTransientEffect(shard);

            var rect = shard.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(8f, 8f);
            rect.anchoredPosition = GetCellCenter(cell);
            rect.localRotation = Quaternion.Euler(0f, 0f, shardIndex * (360f / EatShardCount));

            var image = shard.GetComponent<Image>();
            image.color = FoodColor;
            image.raycastTarget = false;

            var angle = shardIndex * (Mathf.PI * 2f / EatShardCount) + UnityEngine.Random.Range(-0.12f, 0.12f);
            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * UnityEngine.Random.Range(14f, 24f);
            HostBehaviour.StartCoroutine(AnimateShardRoutine(shard, image, rect.anchoredPosition, rect.anchoredPosition + offset));
        }

        private void PlayWrapFlash(Vector2Int fromHead, Vector2Int toHead)
        {
            if (fromHead.x != toHead.x)
            {
                var exitAnchor = new Vector2(fromHead.x > toHead.x ? 1f : 0f, 0.5f);
                var enterAnchor = new Vector2(fromHead.x > toHead.x ? 0f : 1f, 0.5f);
                CreateEdgeFlash(exitAnchor, new Vector2(18f, boardPlayfield.rect.height * 0.22f));
                CreateEdgeFlash(enterAnchor, new Vector2(18f, boardPlayfield.rect.height * 0.22f));
            }

            if (fromHead.y != toHead.y)
            {
                var exitAnchor = new Vector2(0.5f, fromHead.y > toHead.y ? 1f : 0f);
                var enterAnchor = new Vector2(0.5f, fromHead.y > toHead.y ? 0f : 1f);
                CreateEdgeFlash(exitAnchor, new Vector2(boardPlayfield.rect.width * 0.22f, 18f));
                CreateEdgeFlash(enterAnchor, new Vector2(boardPlayfield.rect.width * 0.22f, 18f));
            }
        }

        private void CreateEdgeFlash(Vector2 anchor, Vector2 size)
        {
            if (boardAnimationLayer == null || HostBehaviour == null)
            {
                return;
            }

            var flash = new GameObject("SnakeEdgeFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            flash.transform.SetParent(boardAnimationLayer, false);
            flash.transform.SetAsLastSibling();
            TrackTransientEffect(flash);

            var rect = flash.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            var graphic = flash.GetComponent<RoundedRectGraphic>();
            graphic.color = EdgeFlashColor;
            graphic.CornerRadius = Mathf.Min(size.x, size.y) * 0.5f;
            graphic.raycastTarget = false;
            HostBehaviour.StartCoroutine(FadeAndDestroyGraphicRoutine(flash, graphic, EdgeFlashDuration));
        }

        private IEnumerator PlayCollisionSettlementRoutine(string reasonKey, Vector2Int collisionCell)
        {
            var flashTargets = new List<Vector2Int> { collisionCell };
            for (var i = snakeSegments.Count - 1; i >= 0 && flashTargets.Count < 3; i--)
            {
                if (!flashTargets.Contains(snakeSegments[i]))
                {
                    flashTargets.Add(snakeSegments[i]);
                }
            }

            for (var i = 0; i < flashTargets.Count; i++)
            {
                var flash = CreateCellEffectGraphic("SnakeCollisionFlash", flashTargets[i], CollisionFlashColor, 14f, SnakeHeadScale * 1.08f);
                if (flash != null && HostBehaviour != null)
                {
                    HostBehaviour.StartCoroutine(AnimateFadeScaleAndDestroy(flash, flash.localScale, flash.localScale * 1.08f, CollisionFlashDuration));
                }
            }

            yield return WaitForUnscaledSeconds(CollisionSettlementDelay);
            settlementRoutine = null;
            ShowSettlement(reasonKey);
        }

        private IEnumerator PlayWinSettlementRoutine()
        {
            for (var i = 0; i < snakeSegments.Count; i++)
            {
                var flash = CreateCellEffectGraphic("SnakeWinFlash", snakeSegments[i], WinFlashColor, 14f, SnakeHeadScale * 1.06f);
                if (flash != null && HostBehaviour != null)
                {
                    HostBehaviour.StartCoroutine(AnimateFadeScaleAndDestroy(flash, flash.localScale, flash.localScale * 1.10f, WinFlashDuration));
                }

                yield return WaitForUnscaledSeconds(WinTrailStepDelay);
            }

            yield return WaitForUnscaledSeconds(WinSettlementDelay);
            settlementRoutine = null;
            ShowSettlement("snake.settlement.win");
        }

        private RectTransform CreateCellEffectGraphic(string name, Vector2Int cell, Color color, float cornerRadius, Vector3 scale)
        {
            if (boardAnimationLayer == null)
            {
                return null;
            }

            var effect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            effect.transform.SetParent(boardAnimationLayer, false);
            effect.transform.SetAsLastSibling();
            TrackTransientEffect(effect);

            var rect = effect.GetComponent<RectTransform>();
            var targetCell = GetCellView(cell);
            rect.sizeDelta = targetCell != null && targetCell.Rect != null ? targetCell.Rect.rect.size : new Vector2(32f, 32f);
            rect.anchoredPosition = GetCellCenter(cell);
            rect.localScale = scale;

            var graphic = effect.GetComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = cornerRadius;
            graphic.raycastTarget = false;
            return rect;
        }

        private Vector2 GetCellCenter(Vector2Int cell)
        {
            var targetCell = GetCellView(cell);
            if (targetCell == null || targetCell.Rect == null || boardAnimationLayer == null)
            {
                return Vector2.zero;
            }

            return boardAnimationLayer.InverseTransformPoint(targetCell.Rect.TransformPoint(targetCell.Rect.rect.center));
        }

        private void TrackTransientEffect(GameObject effect)
        {
            if (effect != null)
            {
                transientEffects.Add(effect);
            }
        }

        private void ClearTransientEffects()
        {
            for (var i = transientEffects.Count - 1; i >= 0; i--)
            {
                if (transientEffects[i] != null)
                {
                    UnityEngine.Object.Destroy(transientEffects[i]);
                }
            }

            transientEffects.Clear();
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
                var progress = Mathf.Clamp01(elapsed / duration);
                var color = startColor;
                color.a = Mathf.Lerp(startColor.a, 0f, progress);
                graphic.color = color;
                yield return null;
            }

            DestroyTransientEffect(target);
        }

        private IEnumerator AnimateFadeScaleAndDestroy(RectTransform rect, Vector3 startScale, Vector3 endScale, float duration)
        {
            if (rect == null)
            {
                yield break;
            }

            var graphic = rect.GetComponent<Graphic>();
            if (graphic == null)
            {
                yield break;
            }

            var target = rect.gameObject;
            var startColor = graphic.color;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (rect == null || graphic == null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                rect.localScale = Vector3.LerpUnclamped(startScale, endScale, progress);
                var color = startColor;
                color.a = Mathf.Lerp(startColor.a, 0f, progress);
                graphic.color = color;
                yield return null;
            }

            DestroyTransientEffect(target);
        }

        private IEnumerator AnimateShardRoutine(GameObject target, Image image, Vector2 start, Vector2 end)
        {
            if (target == null || image == null)
            {
                yield break;
            }

            var rect = target.GetComponent<RectTransform>();
            var startColor = image.color;
            var elapsed = 0f;
            while (elapsed < EatShardDuration)
            {
                if (target == null || image == null || rect == null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / EatShardDuration);
                rect.anchoredPosition = Vector2.Lerp(start, end, progress);
                rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.34f, progress);
                var color = startColor;
                color.a = Mathf.Lerp(startColor.a, 0f, progress);
                image.color = color;
                yield return null;
            }

            DestroyTransientEffect(target);
        }

        private IEnumerator WaitForUnscaledSeconds(float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void DestroyTransientEffect(GameObject target)
        {
            transientEffects.Remove(target);
            if (target != null)
            {
                UnityEngine.Object.Destroy(target);
            }
        }

        private sealed class SnakeCellView
        {
            public int Row;
            public int Column;
            public RectTransform Rect;
            public RoundedRectGraphic Graphic;
            public RectTransform FaceRoot;
            public RoundedRectGraphic LeftEye;
            public RoundedRectGraphic RightEye;
            public Coroutine PulseRoutine;
        }
    }
}
