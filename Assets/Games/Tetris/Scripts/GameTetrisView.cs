using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed class GameTetrisView : MiniGameBase
    {
        public const string GameIdConstant = "tetris";

        private const int BoardWidth = 10;
        private const int BoardHeight = 20;
        private const int PreviewSize = 4;
        private const int PointsPerSoftDrop = 1;
        private const int PointsPerHardDrop = 2;
        private const float BottomLayoutInset = 244f;
        private const float HorizontalRepeatStartDelay = 0.18f;
        private const float HorizontalRepeatInterval = 0.07f;

        private static readonly Color EmptyCellColor = new Color(1f, 1f, 1f, 0f);
        private static readonly Color32 BoardColor = new Color32(18, 24, 31, 255);
        private static readonly Color PreviewPanelColor = new Color(0.05f, 0.08f, 0.12f, 0.75f);
        private static readonly Color PreviewPanelBorderColor = new Color(0.88f, 0.92f, 1f, 0.92f);
        private static Sprite blockCellSprite;
        private static readonly Color32[] PieceColors =
        {
            new Color32(93, 210, 255, 255),
            new Color32(60, 119, 255, 255),
            new Color32(255, 169, 58, 255),
            new Color32(255, 221, 65, 255),
            new Color32(86, 213, 112, 255),
            new Color32(177, 111, 255, 255),
            new Color32(255, 92, 102, 255)
        };

        private static readonly Vector2Int[][] Shapes =
        {
            new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(3, 1) },
            new[] { new Vector2Int(0, 2), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1) },
            new[] { new Vector2Int(2, 2), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1) },
            new[] { new Vector2Int(1, 2), new Vector2Int(2, 2), new Vector2Int(1, 1), new Vector2Int(2, 1) },
            new[] { new Vector2Int(1, 2), new Vector2Int(2, 2), new Vector2Int(0, 1), new Vector2Int(1, 1) },
            new[] { new Vector2Int(1, 2), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1) },
            new[] { new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(1, 1), new Vector2Int(2, 1) }
        };

        private readonly int[,] lockedCells = new int[BoardWidth, BoardHeight];
        private readonly TetrisCellView[,] cellGraphics = new TetrisCellView[BoardWidth, BoardHeight];
        private readonly TetrisCellView[,] previewGraphics = new TetrisCellView[PreviewSize, PreviewSize];

        private TextMeshProUGUI titleText;
        private TextMeshProUGUI scoreText;
        private TextMeshProUGUI linesText;
        private TextMeshProUGUI levelText;
        private Button restartButton;
        private int currentPiece;
        private int nextPiece;
        private int rotation;
        private Vector2Int piecePosition;
        private float fallTimer;
        private float keyboardHorizontalRepeatTimer;
        private int keyboardHorizontalDirection;
        private int score;
        private int clearedLineCount;
        private bool isPaused;
        private bool isGameOver;

        public GameTetrisView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "GameTetrisView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        public override void Tick(float deltaTime)
        {
            if (isPaused || isGameOver)
            {
                return;
            }

            HandleKeyboardInput(deltaTime);
            fallTimer += Mathf.Max(0f, deltaTime);
            if (fallTimer >= GetFallInterval())
            {
                fallTimer = 0f;
                StepDown(false);
            }
        }

        protected override void BuildOrBindSections()
        {
            Shell.ConfigureBottomMode(MiniGameShellBottomMode.DefaultSlot, BottomLayoutInset);
            BuildTop();
            BuildBoard();
            BuildBottom();
        }

        protected override void ResetGame()
        {
            StartNewGame();
        }

        protected override void OnPauseRequested()
        {
            if (isGameOver)
            {
                return;
            }

            isPaused = true;
            Shell.ShowPausePopup(ResumeGame, ConfirmExitToHall);
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.tetris.help", null);
        }

        private void BuildTop()
        {
            var refs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("TetrisTop"));
            titleText = refs.TitleText;
            scoreText = refs.ScoreText;

            linesText = CreateOverlayText(
                refs.Root,
                "Lines",
                new Vector2(0.18f, 0.16f),
                new Vector2(0.18f, 0.16f),
                new Vector2(0f, 0f),
                new Vector2(28f, 4f),
                new Vector2(190f, 34f),
                TextAlignmentOptions.Left,
                20f,
                new Color32(76, 91, 104, 255),
                UiTextCatalog.Format("tetris.hud.lines", 0));
            levelText = CreateOverlayText(
                refs.Root,
                "Level",
                new Vector2(0.82f, 0.16f),
                new Vector2(0.82f, 0.16f),
                new Vector2(1f, 0f),
                new Vector2(-28f, 4f),
                new Vector2(190f, 34f),
                TextAlignmentOptions.Right,
                20f,
                new Color32(76, 91, 104, 255),
                UiTextCatalog.Format("tetris.hud.level", 1));
        }

        private void BuildBoard()
        {
            var host = new GameObject("TetrisBoardHost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var hostRect = host.GetComponent<RectTransform>();
            hostRect.SetParent(Shell.ContentHost, false);
            hostRect.anchorMin = new Vector2(0.5f, 0.5f);
            hostRect.anchorMax = new Vector2(0.5f, 0.5f);
            hostRect.pivot = new Vector2(0.5f, 0.5f);
            hostRect.anchoredPosition = new Vector2(-54f, 6f);
            hostRect.sizeDelta = new Vector2(400f, 800f);
            var hostGraphic = host.GetComponent<Image>();
            hostGraphic.color = BoardColor;
            hostGraphic.raycastTarget = false;

            var grid = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            var gridRect = grid.GetComponent<RectTransform>();
            gridRect.SetParent(hostRect, false);
            Stretch(gridRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var layout = grid.GetComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = BoardWidth;
            layout.cellSize = new Vector2(40f, 40f);
            layout.spacing = Vector2.zero;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;

            for (var y = BoardHeight - 1; y >= 0; y--)
            {
                for (var x = 0; x < BoardWidth; x++)
                {
                    cellGraphics[x, y] = CreateCell("Cell_" + x + "_" + y, gridRect, 0f);
                }
            }

            var side = new GameObject("TetrisSidePanel", typeof(RectTransform));
            var sideRect = side.GetComponent<RectTransform>();
            sideRect.SetParent(Shell.ContentHost, false);
            sideRect.anchorMin = new Vector2(0.5f, 0.5f);
            sideRect.anchorMax = new Vector2(0.5f, 0.5f);
            sideRect.pivot = new Vector2(0.5f, 0.5f);
            sideRect.anchoredPosition = new Vector2(254f, 256f);
            sideRect.sizeDelta = new Vector2(160f, 210f);

            CreatePanel(sideRect, "PreviewBorder", new Vector2(0f, 0f), new Vector2(168f, 178f), PreviewPanelBorderColor);
            CreatePanel(sideRect, "PreviewPanel", new Vector2(0f, 0f), new Vector2(160f, 170f), PreviewPanelColor);
            CreateOverlayText(
                sideRect,
                "PreviewLabel",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 56f),
                new Vector2(140f, 32f),
                TextAlignmentOptions.Center,
                22f,
                new Color(0.85f, 0.85f, 0.9f, 1f),
                UiTextCatalog.Get("tetris.preview.next"));
            var previewGrid = new GameObject("PreviewGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            var previewRect = previewGrid.GetComponent<RectTransform>();
            previewRect.SetParent(sideRect, false);
            previewRect.anchorMin = new Vector2(0.5f, 0.5f);
            previewRect.anchorMax = new Vector2(0.5f, 0.5f);
            previewRect.pivot = new Vector2(0.5f, 0.5f);
            previewRect.anchoredPosition = new Vector2(0f, -18f);
            previewRect.sizeDelta = new Vector2(112f, 112f);
            var previewLayout = previewGrid.GetComponent<GridLayoutGroup>();
            previewLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            previewLayout.constraintCount = PreviewSize;
            previewLayout.cellSize = new Vector2(28f, 28f);
            previewLayout.spacing = Vector2.zero;
            previewLayout.childAlignment = TextAnchor.MiddleCenter;

            for (var y = PreviewSize - 1; y >= 0; y--)
            {
                for (var x = 0; x < PreviewSize; x++)
                {
                    previewGraphics[x, y] = CreateCell("Preview_" + x + "_" + y, previewRect, 0f);
                }
            }
        }

        private void BuildBottom()
        {
            var root = new GameObject("TetrisBottom", typeof(RectTransform));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(Shell.BottomHost, false);
            Stretch(rootRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var directionPadConfig = MiniGameDirectionPadBuilder.Config.Default;
            directionPadConfig.AnchorMin = new Vector2(0.5f, 0.5f);
            directionPadConfig.AnchorMax = new Vector2(0.5f, 0.5f);
            directionPadConfig.OffsetMin = new Vector2(-230f, -100f);
            directionPadConfig.OffsetMax = new Vector2(-14f, 116f);
            directionPadConfig.RingColor = new Color(1f, 0.98f, 0.92f, 0.36f);
            directionPadConfig.ButtonColor = new Color(246f / 255f, 203f / 255f, 86f / 255f, 1f);
            directionPadConfig.ArrowColor = new Color(22f / 255f, 30f / 255f, 36f / 255f, 1f);
            directionPadConfig.UpAction = RotateCurrentPiece;
            directionPadConfig.DownAction = SoftDropOneCell;
            directionPadConfig.LeftAction = null;
            directionPadConfig.RightAction = null;
            var directionPad = MiniGameDirectionPadBuilder.Create(rootRect, directionPadConfig);
            AttachHoldRepeat(directionPad.LeftButton, MoveLeft);
            AttachHoldRepeat(directionPad.RightButton, MoveRight);

            var rotateButton = CreateControlButton(rootRect, "RotateButton", UiTextCatalog.Get("tetris.action.rotate"), RotateCurrentPiece);
            var rotateRect = rotateButton.GetComponent<RectTransform>();
            rotateRect.anchorMin = new Vector2(0.5f, 0.5f);
            rotateRect.anchorMax = new Vector2(0.5f, 0.5f);
            rotateRect.pivot = new Vector2(0.5f, 0.5f);
            rotateRect.anchoredPosition = new Vector2(130f, 56f);
            rotateRect.sizeDelta = new Vector2(128f, 66f);

            var hardDropButton = CreateControlButton(rootRect, "HardDropButton", UiTextCatalog.Get("tetris.action.hard_drop"), HardDrop);
            var hardDropRect = hardDropButton.GetComponent<RectTransform>();
            hardDropRect.anchorMin = new Vector2(0.5f, 0.5f);
            hardDropRect.anchorMax = new Vector2(0.5f, 0.5f);
            hardDropRect.pivot = new Vector2(0.5f, 0.5f);
            hardDropRect.anchoredPosition = new Vector2(130f, -36f);
            hardDropRect.sizeDelta = new Vector2(128f, 66f);

            restartButton = CreateControlButton(rootRect, "RestartButton", UiTextCatalog.Get("common.action.restart"), StartNewGame);
            var restartRect = restartButton.GetComponent<RectTransform>();
            restartRect.anchorMin = new Vector2(1f, 0.5f);
            restartRect.anchorMax = new Vector2(1f, 0.5f);
            restartRect.pivot = new Vector2(1f, 0.5f);
            restartRect.anchoredPosition = new Vector2(-22f, 12f);
            restartRect.sizeDelta = new Vector2(148f, 74f);
            restartButton.gameObject.SetActive(false);
        }

        private void StartNewGame()
        {
            Array.Clear(lockedCells, 0, lockedCells.Length);
            score = 0;
            clearedLineCount = 0;
            isPaused = false;
            isGameOver = false;
            fallTimer = 0f;
            currentPiece = UnityEngine.Random.Range(0, Shapes.Length);
            nextPiece = UnityEngine.Random.Range(0, Shapes.Length);
            SpawnPiece();
            Shell.ClosePopup();
            if (restartButton != null)
            {
                restartButton.gameObject.SetActive(false);
            }

            UpdateHud();
            RefreshBoard();
        }

        private void SpawnPiece()
        {
            currentPiece = nextPiece;
            nextPiece = UnityEngine.Random.Range(0, Shapes.Length);
            rotation = 0;
            piecePosition = new Vector2Int(3, BoardHeight - 3);
            if (!CanPlace(currentPiece, rotation, piecePosition))
            {
                GameOver();
                return;
            }

            RefreshPreview();
        }

        private void MoveLeft()
        {
            TryMove(new Vector2Int(-1, 0));
        }

        private void MoveRight()
        {
            TryMove(new Vector2Int(1, 0));
        }

        private void TryMove(Vector2Int delta)
        {
            if (isPaused || isGameOver)
            {
                return;
            }

            var nextPosition = piecePosition + delta;
            if (!CanPlace(currentPiece, rotation, nextPosition))
            {
                return;
            }

            piecePosition = nextPosition;
            MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.55f);
            RefreshBoard();
        }

        private void RotateCurrentPiece()
        {
            if (isPaused || isGameOver)
            {
                return;
            }

            var nextRotation = (rotation + 1) % 4;
            var kicks = new[]
            {
                Vector2Int.zero,
                new Vector2Int(-1, 0),
                new Vector2Int(1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(-2, 0),
                new Vector2Int(2, 0)
            };
            for (var i = 0; i < kicks.Length; i++)
            {
                var testPosition = piecePosition + kicks[i];
                if (!CanPlace(currentPiece, nextRotation, testPosition))
                {
                    continue;
                }

                rotation = nextRotation;
                piecePosition = testPosition;
                MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.62f);
                RefreshBoard();
                return;
            }
        }

        private void HardDrop()
        {
            if (isPaused || isGameOver)
            {
                return;
            }

            var distance = 0;
            while (CanPlace(currentPiece, rotation, piecePosition + Vector2Int.down))
            {
                piecePosition += Vector2Int.down;
                distance += 1;
            }

            score += distance * PointsPerHardDrop;
            LockCurrentPiece();
            MiniGameSfxPlayer.Play(MiniGameSfxType.Combo, 0.72f);
        }

        private void SoftDropOneCell()
        {
            if (isPaused || isGameOver)
            {
                return;
            }

            StepDown(true);
        }

        private void StepDown(bool softDrop)
        {
            if (CanPlace(currentPiece, rotation, piecePosition + Vector2Int.down))
            {
                piecePosition += Vector2Int.down;
                if (softDrop)
                {
                    score += PointsPerSoftDrop;
                    UpdateHud();
                }

                RefreshBoard();
                return;
            }

            LockCurrentPiece();
        }

        private void LockCurrentPiece()
        {
            var blocks = GetBlocks(currentPiece, rotation, piecePosition);
            for (var i = 0; i < blocks.Length; i++)
            {
                var block = blocks[i];
                if (block.x >= 0 && block.x < BoardWidth && block.y >= 0 && block.y < BoardHeight)
                {
                    lockedCells[block.x, block.y] = currentPiece + 1;
                }
            }

            var cleared = ClearCompletedLines();
            if (cleared > 0)
            {
                clearedLineCount += cleared;
                score += GetLineScore(cleared) * GetLevel();
                MiniGameSfxPlayer.Play(cleared >= 4 ? MiniGameSfxType.Combo : MiniGameSfxType.MatchSuccess, 0.78f);
            }
            else
            {
                MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.48f);
            }

            SpawnPiece();
            UpdateHud();
            RefreshBoard();
        }

        private int ClearCompletedLines()
        {
            var cleared = 0;
            for (var y = 0; y < BoardHeight; y++)
            {
                var full = true;
                for (var x = 0; x < BoardWidth; x++)
                {
                    if (lockedCells[x, y] != 0)
                    {
                        continue;
                    }

                    full = false;
                    break;
                }

                if (!full)
                {
                    continue;
                }

                cleared += 1;
                for (var pullY = y; pullY < BoardHeight - 1; pullY++)
                {
                    for (var x = 0; x < BoardWidth; x++)
                    {
                        lockedCells[x, pullY] = lockedCells[x, pullY + 1];
                    }
                }

                for (var x = 0; x < BoardWidth; x++)
                {
                    lockedCells[x, BoardHeight - 1] = 0;
                }

                y -= 1;
            }

            return cleared;
        }

        private void GameOver()
        {
            isGameOver = true;
            isPaused = false;
            if (restartButton != null)
            {
                restartButton.gameObject.SetActive(true);
            }

            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            var settlement = BuildSettlement();
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "TetrisSettlementPanel",
                    Style = MiniGameRewardSettlementPanelStyle.Failure,
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.Retry,
                    Title = UiTextCatalog.Get("tetris.settlement.failure_title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("tetris.settlement.score"), score.ToString()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("tetris.settlement.lines"), clearedLineCount.ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                StartNewGame,
                delegate { CompleteGame?.Invoke(settlement); },
                true);
        }

        private void ResumeGame()
        {
            isPaused = false;
            Shell.ClosePopup();
        }

        private void ConfirmExitToHall()
        {
            Shell.ClosePopup();
            isPaused = true;
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            var settlement = BuildSettlement();
            ShowBackHallRewardSettlementPanel(
                settlement,
                "TetrisSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("tetris.settlement.score"), score.ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("tetris.settlement.lines"), clearedLineCount.ToString()),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private MiniGameSettlement BuildSettlement()
        {
            return new MiniGameSettlement
            {
                Score = score,
                CoinCount = GetCoinCount(),
                ChestCount = GetChestCount(),
                Summary = isGameOver
                    ? UiTextCatalog.Format("tetris.settlement.game_over", score, clearedLineCount, GetCoinCount(), GetChestCount())
                    : UiTextCatalog.Format("tetris.settlement.exit", score, clearedLineCount, GetCoinCount())
            };
        }

        private void HandleKeyboardInput(float deltaTime)
        {
            var horizontalDirection = 0;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A))
            {
                horizontalDirection -= 1;
            }

            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D))
            {
                horizontalDirection += 1;
            }

            ProcessKeyboardHorizontalMove(Mathf.Clamp(horizontalDirection, -1, 1), deltaTime);

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                RotateCurrentPiece();
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                HardDrop();
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                SoftDropOneCell();
            }
        }

        private void ProcessKeyboardHorizontalMove(int direction, float deltaTime)
        {
            if (direction == 0)
            {
                keyboardHorizontalDirection = 0;
                keyboardHorizontalRepeatTimer = 0f;
                return;
            }

            if (keyboardHorizontalDirection != direction)
            {
                keyboardHorizontalDirection = direction;
                keyboardHorizontalRepeatTimer = HorizontalRepeatStartDelay;
                TryMove(new Vector2Int(direction, 0));
                return;
            }

            keyboardHorizontalRepeatTimer -= Mathf.Max(0f, deltaTime);
            while (keyboardHorizontalRepeatTimer <= 0f)
            {
                TryMove(new Vector2Int(direction, 0));
                keyboardHorizontalRepeatTimer += HorizontalRepeatInterval;
            }
        }

        private bool CanPlace(int piece, int pieceRotation, Vector2Int position)
        {
            var blocks = GetBlocks(piece, pieceRotation, position);
            for (var i = 0; i < blocks.Length; i++)
            {
                var block = blocks[i];
                if (block.x < 0 || block.x >= BoardWidth || block.y < 0)
                {
                    return false;
                }

                if (block.y >= BoardHeight)
                {
                    continue;
                }

                if (lockedCells[block.x, block.y] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector2Int[] GetBlocks(int piece, int pieceRotation, Vector2Int position)
        {
            var shape = Shapes[Mathf.Clamp(piece, 0, Shapes.Length - 1)];
            var blocks = new Vector2Int[shape.Length];
            for (var i = 0; i < shape.Length; i++)
            {
                var point = shape[i];
                for (var turn = 0; turn < pieceRotation; turn++)
                {
                    point = new Vector2Int(3 - point.y, point.x);
                }

                blocks[i] = position + point;
            }

            return blocks;
        }

        private void RefreshBoard()
        {
            for (var y = 0; y < BoardHeight; y++)
            {
                for (var x = 0; x < BoardWidth; x++)
                {
                    var value = lockedCells[x, y];
                    if (value == 0)
                    {
                        cellGraphics[x, y].SetEmpty();
                    }
                    else
                    {
                        cellGraphics[x, y].SetSolid(PieceColors[value - 1]);
                    }
                }
            }

            if (!isGameOver)
            {
                var ghostPosition = GetGhostPosition();
                var ghostBlocks = GetBlocks(currentPiece, rotation, ghostPosition);
                for (var i = 0; i < ghostBlocks.Length; i++)
                {
                    var block = ghostBlocks[i];
                    if (block.x >= 0 && block.x < BoardWidth && block.y >= 0 && block.y < BoardHeight && lockedCells[block.x, block.y] == 0)
                    {
                        cellGraphics[block.x, block.y].SetGhost(PieceColors[currentPiece]);
                    }
                }

                var blocks = GetBlocks(currentPiece, rotation, piecePosition);
                for (var i = 0; i < blocks.Length; i++)
                {
                    var block = blocks[i];
                    if (block.x >= 0 && block.x < BoardWidth && block.y >= 0 && block.y < BoardHeight)
                    {
                        cellGraphics[block.x, block.y].SetSolid(PieceColors[currentPiece]);
                    }
                }
            }
        }

        private void RefreshPreview()
        {
            for (var y = 0; y < PreviewSize; y++)
            {
                for (var x = 0; x < PreviewSize; x++)
                {
                    previewGraphics[x, y].SetEmpty();
                }
            }

            var blocks = GetPreviewBlocks(nextPiece);
            for (var i = 0; i < blocks.Length; i++)
            {
                var block = blocks[i];
                if (block.x >= 0 && block.x < PreviewSize && block.y >= 0 && block.y < PreviewSize)
                {
                    previewGraphics[block.x, block.y].SetSolid(PieceColors[nextPiece]);
                }
            }
        }

        private Vector2Int GetGhostPosition()
        {
            var position = piecePosition;
            while (CanPlace(currentPiece, rotation, position + Vector2Int.down))
            {
                position += Vector2Int.down;
            }

            return position;
        }

        private static Vector2Int[] GetPreviewBlocks(int piece)
        {
            var source = GetBlocks(piece, 0, Vector2Int.zero);
            var minX = int.MaxValue;
            var maxX = int.MinValue;
            var minY = int.MaxValue;
            var maxY = int.MinValue;
            for (var i = 0; i < source.Length; i++)
            {
                minX = Mathf.Min(minX, source[i].x);
                maxX = Mathf.Max(maxX, source[i].x);
                minY = Mathf.Min(minY, source[i].y);
                maxY = Mathf.Max(maxY, source[i].y);
            }

            var shapeWidth = maxX - minX + 1;
            var shapeHeight = maxY - minY + 1;
            var offset = new Vector2Int(
                Mathf.FloorToInt((PreviewSize - shapeWidth) * 0.5f) - minX,
                Mathf.FloorToInt((PreviewSize - shapeHeight) * 0.5f) - minY);

            var blocks = new Vector2Int[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                blocks[i] = source[i] + offset;
            }

            return blocks;
        }

        private void UpdateHud()
        {
            if (titleText != null)
            {
                titleText.text = UiTextCatalog.Get("game.tetris.name");
            }

            if (scoreText != null)
            {
                scoreText.text = UiTextCatalog.Format("tetris.hud.score", score);
            }

            if (linesText != null)
            {
                linesText.text = UiTextCatalog.Format("tetris.hud.lines", clearedLineCount);
            }

            if (levelText != null)
            {
                levelText.text = UiTextCatalog.Format("tetris.hud.level", GetLevel());
            }
        }

        private int GetLevel()
        {
            return Mathf.Clamp(1 + (clearedLineCount / 8), 1, 12);
        }

        private float GetFallInterval()
        {
            return Mathf.Max(0.12f, 0.82f - ((GetLevel() - 1) * 0.055f));
        }

        private int GetCoinCount()
        {
            return Mathf.Max(0, score / 100);
        }

        private int GetChestCount()
        {
            return clearedLineCount >= 12 ? 1 : 0;
        }

        private static int GetLineScore(int lines)
        {
            switch (lines)
            {
                case 1:
                    return 100;
                case 2:
                    return 300;
                case 3:
                    return 500;
                default:
                    return 800;
            }
        }

        private static TetrisCellView CreateCell(string name, Transform parent, float radius)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.localScale = Vector3.one;

            var image = go.GetComponent<Image>();
            image.sprite = GetBlockCellSprite();
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = EmptyCellColor;
            image.raycastTarget = false;
            return new TetrisCellView(image);
        }

        private static Sprite GetBlockCellSprite()
        {
            if (blockCellSprite != null)
            {
                return blockCellSprite;
            }

            blockCellSprite = Resources.Load<Sprite>("GeneratedSprites/block_cell_default");
            if (blockCellSprite != null)
            {
                return blockCellSprite;
            }

            var sprites = Resources.LoadAll<Sprite>("GeneratedSprites/block_cell_default");
            if (sprites != null && sprites.Length > 0)
            {
                blockCellSprite = sprites[0];
            }

            return blockCellSprite;
        }

        private static TextMeshProUGUI CreateOverlayText(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size,
            TextAlignmentOptions alignment,
            float fontSize,
            Color color,
            string text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.alignment = alignment;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.color = color;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            return label;
        }

        private static void CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var graphic = go.GetComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = 20f;
            graphic.raycastTarget = false;
        }

        private static Button CreateControlButton(Transform parent, string name, string label, Action action)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic), typeof(Button), typeof(LayoutElement));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(92f, 82f);

            var layout = go.GetComponent<LayoutElement>();
            layout.preferredWidth = 92f;
            layout.preferredHeight = 82f;

            var graphic = go.GetComponent<RoundedRectGraphic>();
            graphic.color = new Color32(246, 203, 86, 255);
            graphic.CornerRadius = 18f;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.SetParent(rect, false);
            Stretch(textRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = label.Length > 2 ? 23f : 34f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color32(22, 30, 36, 255);
            text.enableWordWrapping = false;
            text.raycastTarget = false;

            var button = go.GetComponent<Button>();
            button.targetGraphic = graphic;
            button.onClick.AddListener(delegate
            {
                MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.8f);
                action?.Invoke();
            });
            return button;
        }

        private static void AttachHoldRepeat(Button button, Action action)
        {
            if (button == null)
            {
                return;
            }

            var repeat = button.gameObject.AddComponent<HoldRepeatButton>();
            repeat.Configure(action, HorizontalRepeatStartDelay, HorizontalRepeatInterval);
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private sealed class TetrisCellView
        {
            private readonly Image image;

            public TetrisCellView(Image image)
            {
                this.image = image;
                SetEmpty();
            }

            public void SetEmpty()
            {
                image.color = EmptyCellColor;
            }

            public void SetSolid(Color color)
            {
                image.color = color;
            }

            public void SetGhost(Color color)
            {
                image.color = WithAlpha(color, 0.24f);
            }

            private static Color WithAlpha(Color color, float alpha)
            {
                color.a = alpha;
                return color;
            }
        }

        private sealed class HoldRepeatButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            private Action action;
            private float startDelay;
            private float repeatInterval;
            private float repeatTimer;
            private bool isPressed;

            public void Configure(Action repeatAction, float initialDelay, float interval)
            {
                action = repeatAction;
                startDelay = Mathf.Max(0f, initialDelay);
                repeatInterval = Mathf.Max(0.01f, interval);
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                isPressed = true;
                repeatTimer = startDelay;
                action?.Invoke();
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                isPressed = false;
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                isPressed = false;
            }

            private void Update()
            {
                if (!isPressed)
                {
                    return;
                }

                repeatTimer -= Time.unscaledDeltaTime;
                while (repeatTimer <= 0f)
                {
                    action?.Invoke();
                    repeatTimer += repeatInterval;
                }
            }
        }
    }
}
