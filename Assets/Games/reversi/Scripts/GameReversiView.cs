using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed class GameReversiView : MiniGameBase
    {
        public const string GameIdConstant = "reversi";

        private const int BoardSize = 8;
        private const float AiMoveDelaySeconds = 0.42f;

        private readonly DiscState[,] board = new DiscState[BoardSize, BoardSize];
        private readonly Button[,] cellButtons = new Button[BoardSize, BoardSize];
        private readonly RoundedRectGraphic[,] cellGraphics = new RoundedRectGraphic[BoardSize, BoardSize];
        private readonly RoundedRectGraphic[,] discGraphics = new RoundedRectGraphic[BoardSize, BoardSize];
        private readonly RoundedRectGraphic[,] hintGraphics = new RoundedRectGraphic[BoardSize, BoardSize];

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI summaryLabel;
        private TextMeshProUGUI turnLabel;
        private TextMeshProUGUI statusLabel;
        private Button restartButton;
        private TextMeshProUGUI restartButtonLabel;

        private DiscState currentPlayer;
        private bool isGameOver;
        private bool aiMovePending;
        private float aiMoveCountdown;

        public GameReversiView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "GameReversiView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        private enum DiscState
        {
            Empty,
            Black,
            White
        }

        private struct MoveOption
        {
            public int Row;
            public int Column;
            public int FlipCount;
        }

        protected override void BuildOrBindSections()
        {
            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateReversiConfig("ReversiTop"));
            var bottomContainerRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("ReversiBottom"));
            var bottomRoot = bottomContainerRefs.Root.gameObject;

            titleLabel = topBarRefs.TitleText;
            summaryLabel = topBarRefs.ScoreText;
            turnLabel = topBarRefs.ExtraText;
            restartButton = MiniGameShellBottomBarBuilder.CreateRestartButton(bottomContainerRefs.ActionBar).Button;

            if (titleLabel == null || summaryLabel == null || restartButton == null)
            {
                throw new InvalidOperationException("Reversi prefab structure is incomplete.");
            }

            ConfigureRestartButton();
            BuildContentArea();

            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(OnRestartClicked);
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            isGameOver = false;
            aiMovePending = false;
            aiMoveCountdown = 0f;
            currentPlayer = DiscState.Black;

            ClearBoard();
            board[3, 3] = DiscState.White;
            board[3, 4] = DiscState.Black;
            board[4, 3] = DiscState.Black;
            board[4, 4] = DiscState.White;

            SetStatusText(Text("reversi.status.start", "你执黑先手，点击能翻转白子的格子落子。"));
            RefreshAllVisuals();
            ResolveTurnState();
        }

        public override void Tick(float deltaTime)
        {
            if (!aiMovePending || isGameOver)
            {
                return;
            }

            aiMoveCountdown -= deltaTime;
            if (aiMoveCountdown > 0f)
            {
                return;
            }

            aiMovePending = false;
            ExecuteAiTurn();
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
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.reversi.help", null);
        }

        private void ConfigureRestartButton()
        {
            var rect = restartButton.transform as RectTransform;
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(228f, 72f);
            }

            var icon = restartButton.transform.Find("Icon");
            if (icon != null)
            {
                icon.gameObject.SetActive(false);
            }

            var background = GetOrAddRoundedRectGraphic(restartButton.gameObject);

            background.color = new Color(0.95f, 0.88f, 0.66f, 1f);
            background.raycastTarget = true;
            restartButton.targetGraphic = background;

            restartButton.transition = Selectable.Transition.ColorTint;
            var colors = restartButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.98f, 0.98f, 0.98f, 1f);
            colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.7f);
            restartButton.colors = colors;

            restartButtonLabel = restartButton.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (restartButtonLabel == null)
            {
                restartButtonLabel = CreateTextLabel(
                    "Label",
                    restartButton.transform as RectTransform,
                    28f,
                    new Color(0.3f, 0.21f, 0.08f, 1f),
                    FontStyles.Bold);
                restartButtonLabel.rectTransform.offsetMin = new Vector2(16f, 10f);
                restartButtonLabel.rectTransform.offsetMax = new Vector2(-16f, -10f);
                restartButtonLabel.alignment = TextAlignmentOptions.Center;
            }

            restartButtonLabel.text = Text("common.action.restart", "重新开始");
        }
        private void BuildContentArea()
        {
            var root = CreateRect("ReversiContent", Shell.ContentHost, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var statusHost = CreateRect(
                "StatusPanel",
                root,
                new Vector2(0.08f, 0.83f),
                new Vector2(0.92f, 0.98f),
                Vector2.zero,
                Vector2.zero);

            var statusBackground = GetOrAddRoundedRectGraphic(statusHost.gameObject);
            statusBackground.color = new Color(0.97f, 0.95f, 0.87f, 0.92f);
            statusBackground.raycastTarget = false;

            statusLabel = CreateTextLabel(
                "Status",
                statusHost,
                26f,
                new Color(0.21f, 0.29f, 0.18f, 1f),
                FontStyles.Normal);
            statusLabel.rectTransform.offsetMin = new Vector2(24f, 18f);
            statusLabel.rectTransform.offsetMax = new Vector2(-24f, -18f);
            statusLabel.alignment = TextAlignmentOptions.Center;
            statusLabel.enableWordWrapping = true;

            var boardFrame = CreateRect(
                "BoardFrame",
                root,
                new Vector2(0.5f, 0.42f),
                new Vector2(0.5f, 0.42f),
                new Vector2(560f, 560f),
                Vector2.zero);

            var boardShadow = CreateRect(
                "BoardShadow",
                boardFrame,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(16f, 16f),
                new Vector2(4f, -4f));
            var boardShadowGraphic = GetOrAddRoundedRectGraphic(boardShadow.gameObject);
            boardShadowGraphic.color = new Color(0.1f, 0.18f, 0.08f, 0.18f);
            boardShadowGraphic.raycastTarget = false;

            var boardSurface = CreateRect(
                "BoardSurface",
                boardFrame,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero);
            var boardSurfaceGraphic = GetOrAddRoundedRectGraphic(boardSurface.gameObject);
            boardSurfaceGraphic.color = new Color(0.16f, 0.47f, 0.26f, 1f);
            boardSurfaceGraphic.raycastTarget = false;

            var boardGrid = boardSurface.gameObject.AddComponent<GridLayoutGroup>();
            boardGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            boardGrid.constraintCount = BoardSize;
            boardGrid.padding = new RectOffset(14, 14, 14, 14);
            boardGrid.spacing = new Vector2(4f, 4f);
            boardGrid.cellSize = new Vector2(62f, 62f);
            boardGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
            boardGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            boardGrid.childAlignment = TextAnchor.UpperLeft;

            for (var row = 0; row < BoardSize; row++)
            {
                for (var column = 0; column < BoardSize; column++)
                {
                    var rowCopy = row;
                    var columnCopy = column;

                    var cell = CreateRect(
                        "Cell_" + rowCopy + "_" + columnCopy,
                        boardSurface,
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        boardGrid.cellSize,
                        Vector2.zero);

                    var cellButton = cell.gameObject.AddComponent<Button>();
                    var cellGraphic = GetOrAddRoundedRectGraphic(cell.gameObject);
                    cellGraphic.color = new Color(0.23f, 0.57f, 0.33f, 1f);
                    cellGraphic.raycastTarget = true;
                    cellButton.targetGraphic = cellGraphic;

                    var cellColors = cellButton.colors;
                    cellColors.normalColor = Color.white;
                    cellColors.highlightedColor = new Color(0.97f, 0.97f, 0.97f, 1f);
                    cellColors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
                    cellColors.selectedColor = cellColors.highlightedColor;
                    cellColors.disabledColor = new Color(0.92f, 0.92f, 0.92f, 0.9f);
                    cellButton.colors = cellColors;
                    cellButton.onClick.AddListener(delegate { OnCellClicked(rowCopy, columnCopy); });

                    var hint = CreateRect(
                        "Hint",
                        cell,
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(14f, 14f),
                        Vector2.zero);
                    var hintGraphic = GetOrAddRoundedRectGraphic(hint.gameObject);
                    hintGraphic.color = new Color(1f, 1f, 1f, 0.38f);
                    hintGraphic.raycastTarget = false;
                    hint.gameObject.SetActive(false);

                    var disc = CreateRect(
                        "Disc",
                        cell,
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(46f, 46f),
                        Vector2.zero);
                    var discGraphic = GetOrAddRoundedRectGraphic(disc.gameObject);
                    discGraphic.color = Color.black;
                    discGraphic.raycastTarget = false;
                    disc.gameObject.SetActive(false);

                    cellButtons[rowCopy, columnCopy] = cellButton;
                    cellGraphics[rowCopy, columnCopy] = cellGraphic;
                    discGraphics[rowCopy, columnCopy] = discGraphic;
                    hintGraphics[rowCopy, columnCopy] = hintGraphic;
                }
            }
        }

        private void OnCellClicked(int row, int column)
        {
            if (isGameOver || aiMovePending || currentPlayer != DiscState.Black)
            {
                return;
            }

            if (!TryApplyMove(row, column, DiscState.Black))
            {
                SetStatusText(Text("reversi.status.invalid", "该位置当前不能落子。"));
                return;
            }

            MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.92f);
            currentPlayer = DiscState.White;
            ResolveTurnState();
        }

        private void ExecuteAiTurn()
        {
            if (isGameOver || currentPlayer != DiscState.White)
            {
                return;
            }

            var move = SelectBestAiMove();
            if (move.FlipCount <= 0)
            {
                currentPlayer = DiscState.Black;
                ResolveTurnState();
                return;
            }

            TryApplyMove(move.Row, move.Column, DiscState.White);
            MiniGameSfxPlayer.Play(MiniGameSfxType.MatchSuccess, 0.88f, 1.05f);
            currentPlayer = DiscState.Black;
            ResolveTurnState();
        }

        private void ResolveTurnState()
        {
            RefreshAllVisuals();

            if (IsBoardFull())
            {
                EndGame();
                return;
            }

            var currentMoves = GetLegalMoves(currentPlayer);
            if (currentMoves.Count > 0)
            {
                UpdateTurnStateText();
                if (currentPlayer == DiscState.White)
                {
                    ScheduleAiTurn();
                }
                else
                {
                    aiMovePending = false;
                }

                RefreshAllVisuals();
                return;
            }
            var passedPlayer = currentPlayer;
            currentPlayer = Opponent(currentPlayer);
            var nextMoves = GetLegalMoves(currentPlayer);
            if (nextMoves.Count == 0 || IsBoardFull())
            {
                EndGame();
                return;
            }

            aiMovePending = false;
            SetStatusText(
                passedPlayer == DiscState.Black
                    ? Text("reversi.status.player_pass", "你当前无合法落子，已自动跳过。")
                    : Text("reversi.status.ai_pass", "电脑无合法落子，回合回到你。"));

            UpdateTurnStateText(false);
            if (currentPlayer == DiscState.White)
            {
                ScheduleAiTurn();
            }

            RefreshAllVisuals();
        }

        private void ScheduleAiTurn()
        {
            aiMovePending = true;
            aiMoveCountdown = AiMoveDelaySeconds;
            SetStatusText(Text("reversi.status.ai_thinking", "电脑正在思考下一步。"));
        }

        private void EndGame()
        {
            isGameOver = true;
            aiMovePending = false;
            RefreshAllVisuals();
            var settlement = BuildSettlement(false);
            SetStatusText(settlement.Summary);
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.92f);

            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "ReversiSettlementPanel",
                    Style = ResolveSettlementStyle(),
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.Retry,
                    Title = UiTextCatalog.Get(ResolveSettlementTitleKey()),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("reversi.settlement.black"), CountPieces(DiscState.Black).ToString()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("reversi.settlement.white"), CountPieces(DiscState.White).ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                ResetGame,
                delegate { CompleteGame?.Invoke(settlement); },
                true);
        }

        private MiniGameRewardSettlementPanelStyle ResolveSettlementStyle()
        {
            var blackCount = CountPieces(DiscState.Black);
            var whiteCount = CountPieces(DiscState.White);
            if (blackCount > whiteCount)
            {
                return MiniGameRewardSettlementPanelStyle.Success;
            }

            if (blackCount == whiteCount)
            {
                return MiniGameRewardSettlementPanelStyle.Neutral;
            }

            return MiniGameRewardSettlementPanelStyle.Failure;
        }

        private string ResolveSettlementTitleKey()
        {
            switch (ResolveSettlementStyle())
            {
                case MiniGameRewardSettlementPanelStyle.Success:
                    return "reversi.settlement.win_title";
                case MiniGameRewardSettlementPanelStyle.Neutral:
                    return "reversi.settlement.draw_title";
                default:
                    return "reversi.settlement.failure_title";
            }
        }

        private MiniGameSettlement BuildSettlement(bool isExit)
        {
            var blackCount = CountPieces(DiscState.Black);
            var whiteCount = CountPieces(DiscState.White);
            string resultKey;
            string resultFallback;
            int chestCount;
            int coinCount;

            if (isExit)
            {
                return new MiniGameSettlement
                {
                    Score = blackCount,
                    CoinCount = 10,
                    ChestCount = 0,
                    Summary = FormatText(
                        "reversi.settlement.exit",
                        "本局已退出，获得 10 金币。当前黑棋 {0}，白棋 {1}。",
                        blackCount,
                        whiteCount)
                };
            }

            if (blackCount > whiteCount)
            {
                resultKey = "reversi.result.player_win";
                resultFallback = "你赢了。";
                chestCount = 1;
                coinCount = 60;
            }
            else if (whiteCount > blackCount)
            {
                resultKey = "reversi.result.ai_win";
                resultFallback = "电脑获胜。";
                chestCount = 0;
                coinCount = 15;
            }
            else
            {
                resultKey = "reversi.result.draw";
                resultFallback = "本局平手。";
                chestCount = 0;
                coinCount = 30;
            }

            return new MiniGameSettlement
            {
                Score = blackCount,
                CoinCount = coinCount,
                ChestCount = chestCount,
                Summary = FormatText(
                    "reversi.settlement.summary",
                    "{0} 获得 {1} 金币和 {2} 个宝箱。黑棋 {3}，白棋 {4}。",
                    Text(resultKey, resultFallback),
                    coinCount,
                    chestCount,
                    blackCount,
                    whiteCount)
            };
        }

        private void UpdateTurnStateText(bool overwriteStatus = true)
        {
            if (!overwriteStatus)
            {
                return;
            }

            SetStatusText(
                currentPlayer == DiscState.Black
                    ? Text("reversi.status.player_turn", "轮到你落子。")
                    : Text("reversi.status.ai_thinking", "电脑正在思考下一步。"));
        }

        private bool TryApplyMove(int row, int column, DiscState player)
        {
            if (board[row, column] != DiscState.Empty)
            {
                return false;
            }

            var flips = CollectFlips(row, column, player);
            if (flips.Count == 0)
            {
                return false;
            }

            board[row, column] = player;
            for (var i = 0; i < flips.Count; i++)
            {
                var flip = flips[i];
                board[flip.x, flip.y] = player;
            }

            return true;
        }

        private List<MoveOption> GetLegalMoves(DiscState player)
        {
            var moves = new List<MoveOption>();
            for (var row = 0; row < BoardSize; row++)
            {
                for (var column = 0; column < BoardSize; column++)
                {
                    if (board[row, column] != DiscState.Empty)
                    {
                        continue;
                    }

                    var flipCount = CountFlips(row, column, player);
                    if (flipCount <= 0)
                    {
                        continue;
                    }

                    moves.Add(new MoveOption
                    {
                        Row = row,
                        Column = column,
                        FlipCount = flipCount
                    });
                }
            }

            return moves;
        }

        private MoveOption SelectBestAiMove()
        {
            var moves = GetLegalMoves(DiscState.White);
            if (moves.Count == 0)
            {
                return default;
            }

            var best = moves[0];
            for (var i = 1; i < moves.Count; i++)
            {
                var candidate = moves[i];
                if (IsBetterAiMove(candidate, best))
                {
                    best = candidate;
                }
            }

            return best;
        }
        private static bool IsBetterAiMove(MoveOption candidate, MoveOption currentBest)
        {
            var candidateCorner = IsCorner(candidate.Row, candidate.Column);
            var bestCorner = IsCorner(currentBest.Row, currentBest.Column);
            if (candidateCorner != bestCorner)
            {
                return candidateCorner;
            }

            if (candidate.FlipCount != currentBest.FlipCount)
            {
                return candidate.FlipCount > currentBest.FlipCount;
            }

            if (candidate.Row != currentBest.Row)
            {
                return candidate.Row < currentBest.Row;
            }

            return candidate.Column < currentBest.Column;
        }

        private List<Vector2Int> CollectFlips(int row, int column, DiscState player)
        {
            var result = new List<Vector2Int>();
            var opponent = Opponent(player);

            for (var rowDir = -1; rowDir <= 1; rowDir++)
            {
                for (var columnDir = -1; columnDir <= 1; columnDir++)
                {
                    if (rowDir == 0 && columnDir == 0)
                    {
                        continue;
                    }

                    var buffer = new List<Vector2Int>();
                    var scanRow = row + rowDir;
                    var scanColumn = column + columnDir;

                    while (IsInside(scanRow, scanColumn) && board[scanRow, scanColumn] == opponent)
                    {
                        buffer.Add(new Vector2Int(scanRow, scanColumn));
                        scanRow += rowDir;
                        scanColumn += columnDir;
                    }

                    if (buffer.Count == 0 || !IsInside(scanRow, scanColumn) || board[scanRow, scanColumn] != player)
                    {
                        continue;
                    }

                    result.AddRange(buffer);
                }
            }

            return result;
        }

        private int CountFlips(int row, int column, DiscState player)
        {
            if (board[row, column] != DiscState.Empty)
            {
                return 0;
            }

            var total = 0;
            var opponent = Opponent(player);

            for (var rowDir = -1; rowDir <= 1; rowDir++)
            {
                for (var columnDir = -1; columnDir <= 1; columnDir++)
                {
                    if (rowDir == 0 && columnDir == 0)
                    {
                        continue;
                    }

                    var scanRow = row + rowDir;
                    var scanColumn = column + columnDir;
                    var captured = 0;

                    while (IsInside(scanRow, scanColumn) && board[scanRow, scanColumn] == opponent)
                    {
                        captured++;
                        scanRow += rowDir;
                        scanColumn += columnDir;
                    }

                    if (captured > 0 && IsInside(scanRow, scanColumn) && board[scanRow, scanColumn] == player)
                    {
                        total += captured;
                    }
                }
            }

            return total;
        }

        private void RefreshAllVisuals()
        {
            RefreshHud();
            RefreshBoardVisuals();
        }

        private void RefreshHud()
        {
            if (titleLabel != null)
            {
                titleLabel.text = Text("game.reversi.name", "黑白棋");
            }

            if (summaryLabel != null)
            {
                summaryLabel.text = FormatText(
                    "reversi.hud.counts",
                    "黑棋 {0} · 白棋 {1}",
                    CountPieces(DiscState.Black),
                    CountPieces(DiscState.White));
            }

            if (turnLabel != null)
            {
                turnLabel.text = currentPlayer == DiscState.Black
                    ? Text("reversi.hud.turn.player", "当前回合：你")
                    : Text("reversi.hud.turn.ai", "当前回合：电脑");
            }
        }

        private void RefreshBoardVisuals()
        {
            var highlightPlayerMoves = !isGameOver && !aiMovePending && currentPlayer == DiscState.Black;

            for (var row = 0; row < BoardSize; row++)
            {
                for (var column = 0; column < BoardSize; column++)
                {
                    var state = board[row, column];
                    var hasDisc = state != DiscState.Empty;

                    discGraphics[row, column].gameObject.SetActive(hasDisc);
                    if (hasDisc)
                    {
                        discGraphics[row, column].color = state == DiscState.Black
                            ? new Color(0.11f, 0.12f, 0.12f, 1f)
                            : new Color(0.96f, 0.93f, 0.84f, 1f);
                    }

                    var isLegalMove = highlightPlayerMoves && CountFlips(row, column, DiscState.Black) > 0;
                    hintGraphics[row, column].gameObject.SetActive(isLegalMove);
                    cellGraphics[row, column].color = isLegalMove
                        ? new Color(0.31f, 0.66f, 0.4f, 1f)
                        : new Color(0.23f, 0.57f, 0.33f, 1f);
                    cellButtons[row, column].interactable = !isGameOver && !aiMovePending;
                }
            }
        }

        private void ClearBoard()
        {
            for (var row = 0; row < BoardSize; row++)
            {
                for (var column = 0; column < BoardSize; column++)
                {
                    board[row, column] = DiscState.Empty;
                }
            }
        }
        private bool IsBoardFull()
        {
            for (var row = 0; row < BoardSize; row++)
            {
                for (var column = 0; column < BoardSize; column++)
                {
                    if (board[row, column] == DiscState.Empty)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private int CountPieces(DiscState target)
        {
            var count = 0;
            for (var row = 0; row < BoardSize; row++)
            {
                for (var column = 0; column < BoardSize; column++)
                {
                    if (board[row, column] == target)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static bool IsInside(int row, int column)
        {
            return row >= 0 && row < BoardSize && column >= 0 && column < BoardSize;
        }

        private static bool IsCorner(int row, int column)
        {
            return (row == 0 || row == BoardSize - 1) && (column == 0 || column == BoardSize - 1);
        }

        private static DiscState Opponent(DiscState player)
        {
            return player == DiscState.Black ? DiscState.White : DiscState.Black;
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
        }

        private void ConfirmExitToHall()
        {
            Shell.ClosePopup();
            isGameOver = true;
            aiMovePending = false;
            RefreshAllVisuals();
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.92f);

            var settlement = BuildSettlement(true);
            ShowBackHallRewardSettlementPanel(
                settlement,
                "ReversiSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("reversi.settlement.black"), CountPieces(DiscState.Black).ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("reversi.settlement.white"), CountPieces(DiscState.White).ToString()),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private void OnRestartClicked()
        {
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            ResetGame();
        }

        private void SetStatusText(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 sizeDelta,
            Vector2 anchoredPosition)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPosition;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static RoundedRectGraphic GetOrAddRoundedRectGraphic(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            if (target.GetComponent<CanvasRenderer>() == null)
            {
                target.AddComponent<CanvasRenderer>();
            }

            var graphic = target.GetComponent<RoundedRectGraphic>();
            if (graphic == null)
            {
                graphic = target.AddComponent<RoundedRectGraphic>();
            }

            return graphic;
        }

        private static TextMeshProUGUI CreateTextLabel(
            string name,
            RectTransform parent,
            float fontSize,
            Color color,
            FontStyles fontStyle)
        {
            var rect = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.text = string.Empty;
            text.enableAutoSizing = false;
            text.font = MiniGameFontProvider.DefaultFont;
            return text;
        }

        private static string Text(string key, string fallback)
        {
            return UiTextCatalog.GetOrFallback(key, fallback);
        }

        private static string FormatText(string key, string fallbackFormat, params object[] args)
        {
            var format = UiTextCatalog.GetOrFallback(key, fallbackFormat);
            if (args == null || args.Length == 0)
            {
                return format;
            }

            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return fallbackFormat;
            }
        }
    }
}


