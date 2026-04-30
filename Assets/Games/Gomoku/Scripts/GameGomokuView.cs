using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed class GameGomokuView : MiniGameBase
    {
        public const string GameIdConstant = "gomoku";

        private const string ContentPrefabResourcePath = "GomokuContent";
        private const int BoardSize = 15;
        private const float ContentPadding = 24f;
        private const float BoardFrameSize = 660f;
        private const float BoardPadding = 12f;
        private const float BoardSpacing = 2f;

        private static readonly Color BoardFrameColor = new Color32(246, 226, 176, 255);
        private static readonly Color BoardGridColor = new Color32(208, 171, 107, 255);
        private static readonly Color BoardLineColor = new Color32(136, 95, 45, 255);
        private static readonly Color CellColor = new Color32(255, 255, 255, 0);
        private static readonly Color BlackStoneColor = new Color32(46, 43, 38, 255);
        private static readonly Color WhiteStoneColor = new Color32(250, 246, 234, 255);

        private readonly CellView[,] cells = new CellView[BoardSize, BoardSize];

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI statusLabel;
        private Button restartButton;
        private RectTransform contentRoot;
        private GomokuBoardState boardState;
        private GomokuStone playerStone;
        private GomokuStone aiStone;
        private GomokuRoundState roundState;

        public GameGomokuView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "GameGomokuView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        protected override void BuildOrBindSections()
        {
            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("GomokuTop"));
            var contentObject = LoadRequiredSectionPrefab(ContentPrefabResourcePath, Shell.ContentHost, "GomokuContent");
            var bottomContainerRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("GomokuBottom"));
            var bottomRoot = bottomContainerRefs.Root.gameObject;

            titleLabel = topBarRefs.TitleText;
            statusLabel = topBarRefs.ScoreText;
            restartButton = MiniGameShellBottomBarBuilder.CreateRestartButton(bottomContainerRefs.ActionBar).Button;
            contentRoot = contentObject.GetComponent<RectTransform>();

            if (restartButton != null)
            {
                restartButton.gameObject.name = "RestartButton";
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (titleLabel == null || statusLabel == null || restartButton == null || contentRoot == null)
            {
                throw new InvalidOperationException("Gomoku prefab structure is incomplete.");
            }

            StretchToFill(contentRoot, ContentPadding);
            BuildBoardUi();
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            boardState = new GomokuBoardState(BoardSize);
            boardState.Reset();
            roundState = GomokuRoundState.Ongoing;

            var playerFirst = UnityEngine.Random.value >= 0.5f;
            playerStone = playerFirst ? GomokuStone.Black : GomokuStone.White;
            aiStone = playerStone == GomokuStone.Black ? GomokuStone.White : GomokuStone.Black;

            RefreshBoardUi();
            RefreshHud();

            if (aiStone == GomokuStone.Black)
            {
                ExecuteAiTurn();
            }
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

            for (var row = 0; row < BoardSize; row++)
            {
                for (var col = 0; col < BoardSize; col++)
                {
                    var button = cells[row, col].Button;
                    if (button != null)
                    {
                        button.onClick.RemoveAllListeners();
                    }
                }
            }
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.gomoku.help", null);
        }

        private void BuildBoardUi()
        {
            for (var i = contentRoot.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(contentRoot.GetChild(i).gameObject);
            }

            var boardFrame = CreateUiObject("BoardFrame", contentRoot);
            var boardFrameRect = boardFrame.GetComponent<RectTransform>();
            StretchToCenter(boardFrameRect, BoardFrameSize, BoardFrameSize);

            var boardFrameImage = boardFrame.AddComponent<Image>();
            boardFrameImage.color = BoardFrameColor;

            var boardGrid = CreateUiObject("BoardGrid", boardFrameRect);
            var boardGridRect = boardGrid.GetComponent<RectTransform>();
            StretchWithPadding(boardGridRect, BoardPadding);

            var boardGridImage = boardGrid.AddComponent<Image>();
            boardGridImage.color = BoardGridColor;

            var gridLayout = boardGrid.AddComponent<GridLayoutGroup>();
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = BoardSize;
            gridLayout.spacing = new Vector2(BoardSpacing, BoardSpacing);
            gridLayout.childAlignment = TextAnchor.MiddleCenter;

            var availableSize = BoardFrameSize - (BoardPadding * 2f) - (BoardSpacing * (BoardSize - 1));
            var cellSize = Mathf.FloorToInt(availableSize / BoardSize);
            gridLayout.cellSize = new Vector2(cellSize, cellSize);

            for (var row = 0; row < BoardSize; row++)
            {
                for (var col = 0; col < BoardSize; col++)
                {
                    cells[row, col] = CreateCell(row, col, boardGridRect);
                }
            }
        }

        private CellView CreateCell(int row, int col, Transform parent)
        {
            var cellObject = CreateUiObject("Cell_" + row + "_" + col, parent);
            var cellImage = cellObject.AddComponent<Image>();
            cellImage.color = CellColor;

            var cellButton = cellObject.AddComponent<Button>();
            cellButton.targetGraphic = cellImage;
            var capturedRow = row;
            var capturedCol = col;
            cellButton.onClick.AddListener(delegate { OnCellClicked(capturedRow, capturedCol); });

            CreateGridLine("TopLine", cellObject.transform, Vector2.up, row == 0);
            CreateGridLine("BottomLine", cellObject.transform, Vector2.down, true);
            CreateGridLine("LeftLine", cellObject.transform, Vector2.left, col == 0);
            CreateGridLine("RightLine", cellObject.transform, Vector2.right, true);

            var stoneObject = CreateUiObject("Stone", cellObject.transform);
            var stoneRect = stoneObject.GetComponent<RectTransform>();
            StretchWithPadding(stoneRect, 4f);
            var stoneGraphic = stoneObject.AddComponent<GomokuCircleGraphic>();
            stoneGraphic.enabled = false;

            return new CellView(cellButton, cellImage, stoneGraphic);
        }

        private static void CreateGridLine(string name, Transform parent, Vector2 direction, bool visible)
        {
            if (!visible)
            {
                return;
            }

            var lineObject = CreateUiObject(name, parent);
            var lineRect = lineObject.GetComponent<RectTransform>();
            const float thickness = 2f;

            if (direction == Vector2.up)
            {
                lineRect.anchorMin = new Vector2(0f, 1f);
                lineRect.anchorMax = new Vector2(1f, 1f);
                lineRect.pivot = new Vector2(0.5f, 1f);
                lineRect.sizeDelta = new Vector2(0f, thickness);
                lineRect.anchoredPosition = Vector2.zero;
            }
            else if (direction == Vector2.down)
            {
                lineRect.anchorMin = new Vector2(0f, 0f);
                lineRect.anchorMax = new Vector2(1f, 0f);
                lineRect.pivot = new Vector2(0.5f, 0f);
                lineRect.sizeDelta = new Vector2(0f, thickness);
                lineRect.anchoredPosition = Vector2.zero;
            }
            else if (direction == Vector2.left)
            {
                lineRect.anchorMin = new Vector2(0f, 0f);
                lineRect.anchorMax = new Vector2(0f, 1f);
                lineRect.pivot = new Vector2(0f, 0.5f);
                lineRect.sizeDelta = new Vector2(thickness, 0f);
                lineRect.anchoredPosition = Vector2.zero;
            }
            else
            {
                lineRect.anchorMin = new Vector2(1f, 0f);
                lineRect.anchorMax = new Vector2(1f, 1f);
                lineRect.pivot = new Vector2(1f, 0.5f);
                lineRect.sizeDelta = new Vector2(thickness, 0f);
                lineRect.anchoredPosition = Vector2.zero;
            }

            var lineImage = lineObject.AddComponent<Image>();
            lineImage.color = BoardLineColor;
            lineImage.raycastTarget = false;
        }

        private void OnCellClicked(int row, int col)
        {
            if (roundState != GomokuRoundState.Ongoing || boardState.CurrentTurn != playerStone)
            {
                return;
            }

            if (!boardState.TryPlaceStone(row, col, playerStone, out roundState))
            {
                return;
            }

            MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.9f, 1.05f);
            RefreshBoardUi();
            RefreshHud();

            if (roundState != GomokuRoundState.Ongoing)
            {
                EndRound();
                return;
            }

            ExecuteAiTurn();
        }

        private void ExecuteAiTurn()
        {
            if (roundState != GomokuRoundState.Ongoing || boardState.CurrentTurn != aiStone)
            {
                return;
            }

            var move = GomokuAi.ChooseMove(boardState, aiStone, playerStone);
            if (!move.IsValid)
            {
                roundState = GomokuRoundState.Draw;
                RefreshHud();
                EndRound();
                return;
            }

            boardState.TryPlaceStone(move.Row, move.Column, aiStone, out roundState);
            MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.82f, 0.94f);
            RefreshBoardUi();
            RefreshHud();

            if (roundState != GomokuRoundState.Ongoing)
            {
                EndRound();
            }
        }

        private void EndRound()
        {
            if (roundState == GomokuRoundState.Ongoing)
            {
                return;
            }

            RefreshBoardUi();
            RefreshHud();
            PlayEndRoundSfx();
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.92f, 1f);
            var settlement = BuildSettlement();
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "GomokuSettlementPanel",
                    Style = ResolveSettlementStyle(),
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.Retry,
                    Title = UiTextCatalog.Get(ResolveSettlementTitleKey()),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("gomoku.settlement.stones"), CountStone(playerStone).ToString()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("gomoku.settlement.result"), UiTextCatalog.Get(GetRoundStatusKey())),
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
            if ((roundState == GomokuRoundState.BlackWin && playerStone == GomokuStone.Black) ||
                (roundState == GomokuRoundState.WhiteWin && playerStone == GomokuStone.White))
            {
                return MiniGameRewardSettlementPanelStyle.Success;
            }

            if (roundState == GomokuRoundState.Draw)
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
                    return "gomoku.settlement.win_title";
                case MiniGameRewardSettlementPanelStyle.Neutral:
                    return "gomoku.settlement.draw_title";
                default:
                    return "gomoku.settlement.failure_title";
            }
        }

        private void PlayEndRoundSfx()
        {
            if (roundState == GomokuRoundState.Draw)
            {
                MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.75f);
                return;
            }

            var playerWon =
                (roundState == GomokuRoundState.BlackWin && playerStone == GomokuStone.Black) ||
                (roundState == GomokuRoundState.WhiteWin && playerStone == GomokuStone.White);

            MiniGameSfxPlayer.Play(playerWon ? MiniGameSfxType.MatchSuccess : MiniGameSfxType.MatchFail, 0.85f);
        }

        private void RefreshBoardUi()
        {
            for (var row = 0; row < BoardSize; row++)
            {
                for (var col = 0; col < BoardSize; col++)
                {
                    var stone = boardState.GetStone(row, col);
                    var cell = cells[row, col];
                    cell.Button.interactable = roundState == GomokuRoundState.Ongoing && stone == GomokuStone.None && boardState.CurrentTurn == playerStone;
                    cell.Stone.enabled = stone != GomokuStone.None;
                    if (stone == GomokuStone.Black)
                    {
                        cell.Stone.color = BlackStoneColor;
                    }
                    else if (stone == GomokuStone.White)
                    {
                        cell.Stone.color = WhiteStoneColor;
                    }
                }
            }
        }

        private void RefreshHud()
        {
            if (titleLabel != null)
            {
                titleLabel.text = UiTextCatalog.Get("game.gomoku.name");
            }

            if (statusLabel != null)
            {
                statusLabel.text = BuildStatusText();
            }
        }

        private string BuildStatusText()
        {
            var prefix = UiTextCatalog.Get("gomoku.hud.status");
            var statusText = UiTextCatalog.Get(GetRoundStatusKey());
            return prefix + " " + statusText;
        }

        private string GetRoundStatusKey()
        {
            switch (roundState)
            {
                case GomokuRoundState.BlackWin:
                    return playerStone == GomokuStone.Black ? "gomoku.status.player_win" : "gomoku.status.ai_win";
                case GomokuRoundState.WhiteWin:
                    return playerStone == GomokuStone.White ? "gomoku.status.player_win" : "gomoku.status.ai_win";
                case GomokuRoundState.Draw:
                    return "gomoku.status.draw";
                default:
                    return boardState.CurrentTurn == playerStone ? "gomoku.status.player_turn" : "gomoku.status.ai_turn";
            }
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
        }

        private void ConfirmExitToHall()
        {
            if (roundState != GomokuRoundState.Ongoing)
            {
                return;
            }

            RefreshHud();
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.92f, 1f);
            var settlement = BuildExitSettlement();
            ShowBackHallRewardSettlementPanel(
                settlement,
                "GomokuSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("gomoku.settlement.stones"), CountStone(playerStone).ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("gomoku.settlement.result"), UiTextCatalog.Get(GetRoundStatusKey())),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private MiniGameSettlement BuildSettlement()
        {
            var playerWon =
                (roundState == GomokuRoundState.BlackWin && playerStone == GomokuStone.Black) ||
                (roundState == GomokuRoundState.WhiteWin && playerStone == GomokuStone.White);
            var aiWon =
                (roundState == GomokuRoundState.BlackWin && aiStone == GomokuStone.Black) ||
                (roundState == GomokuRoundState.WhiteWin && aiStone == GomokuStone.White);
            var playerStoneCount = CountStone(playerStone);

            if (playerWon)
            {
                return new MiniGameSettlement
                {
                    Score = playerStoneCount,
                    CoinCount = 60,
                    ChestCount = 1,
                    Summary = UiTextCatalog.Format("gomoku.settlement.win", playerStoneCount, 60, 1)
                };
            }

            if (aiWon)
            {
                return new MiniGameSettlement
                {
                    Score = playerStoneCount,
                    CoinCount = 15,
                    ChestCount = 0,
                    Summary = UiTextCatalog.Format("gomoku.settlement.lose", playerStoneCount, 15)
                };
            }

            return new MiniGameSettlement
            {
                Score = playerStoneCount,
                CoinCount = 30,
                ChestCount = 0,
                Summary = UiTextCatalog.Format("gomoku.settlement.draw", playerStoneCount, 30)
            };
        }

        private MiniGameSettlement BuildExitSettlement()
        {
            var playerStoneCount = CountStone(playerStone);
            return new MiniGameSettlement
            {
                Score = playerStoneCount,
                CoinCount = 10,
                ChestCount = 0,
                Summary = UiTextCatalog.Format("gomoku.settlement.exit", playerStoneCount, 10)
            };
        }

        private int CountStone(GomokuStone stone)
        {
            var count = 0;
            if (boardState == null)
            {
                return count;
            }

            for (var row = 0; row < BoardSize; row++)
            {
                for (var col = 0; col < BoardSize; col++)
                {
                    if (boardState.GetStone(row, col) == stone)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private void OnRestartClicked()
        {
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            ResetGame();
        }

        private static GameObject LoadRequiredSectionPrefab(string resourcePath, Transform parent, string instanceName)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                throw new InvalidOperationException("Section prefab not found at Resources/" + resourcePath);
            }

            var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.name = instanceName;
            return instance;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void StretchToFill(RectTransform rectTransform, float padding)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(padding, padding);
            rectTransform.offsetMax = new Vector2(-padding, -padding);
        }

        private static void StretchToCenter(RectTransform rectTransform, float width, float height)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(width, height);
            rectTransform.anchoredPosition = new Vector2(0f, 10f);
        }

        private static void StretchWithPadding(RectTransform rectTransform, float padding)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(padding, padding);
            rectTransform.offsetMax = new Vector2(-padding, -padding);
        }

        private readonly struct CellView
        {
            public CellView(Button button, Image background, GomokuCircleGraphic stone)
            {
                Button = button;
                Background = background;
                Stone = stone;
            }

            public Button Button { get; }

            public Image Background { get; }

            public GomokuCircleGraphic Stone { get; }
        }
    }
}
