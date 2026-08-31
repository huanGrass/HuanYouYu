using System;
using System.Collections;
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
        private const float IntersectionPadding = 18f;
        private const float AiMoveDelaySeconds = 1f;
        private const float WinningStoneStepSeconds = 0.11f;
        private const float WinningPulseSeconds = 0.28f;
        private const float SettlementDelayAfterWinningAnimationSeconds = 1f;

        private static readonly Color BoardFrameColor = new Color32(246, 226, 176, 255);
        private static readonly Color BoardGridColor = new Color32(208, 171, 107, 255);
        private static readonly Color BoardLineColor = new Color32(136, 95, 45, 255);
        private static readonly Color CellColor = new Color32(255, 255, 255, 0);
        private static readonly Color BlackStoneColor = new Color32(46, 43, 38, 255);
        private static readonly Color WhiteStoneColor = new Color32(250, 246, 234, 255);
        private static readonly Color PreviewBlackColor = new Color32(46, 43, 38, 190);
        private static readonly Color PreviewWhiteColor = new Color32(255, 252, 242, 220);
        private static readonly Color LastMoveColor = new Color32(208, 70, 56, 255);
        private static readonly Color WinningStoneColor = new Color32(255, 196, 63, 255);

        private readonly CellView[,] cells = new CellView[BoardSize, BoardSize];

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI statusLabel;
        private Button restartButton;
        private RectTransform contentRoot;
        private GomokuBoardState boardState;
        private GomokuStone playerStone;
        private GomokuStone aiStone;
        private GomokuRoundState roundState;
        private int previewRow = -1;
        private int previewColumn = -1;
        private int lastMoveRow = -1;
        private int lastMoveColumn = -1;
        private Coroutine aiTurnCoroutine;
        private Coroutine endRoundCoroutine;

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
            CancelPendingAiTurn();
            CancelEndRoundAnimation();
            Shell.ClosePopup();
            boardState = new GomokuBoardState(BoardSize);
            boardState.Reset();
            roundState = GomokuRoundState.Ongoing;
            ClearMoveFeedback();

            var playerFirst = UnityEngine.Random.value >= 0.5f;
            playerStone = playerFirst ? GomokuStone.Black : GomokuStone.White;
            aiStone = playerStone == GomokuStone.Black ? GomokuStone.White : GomokuStone.Black;

            RefreshBoardUi();
            RefreshHud();

            if (aiStone == GomokuStone.Black)
            {
                ScheduleAiTurn();
            }
        }

        protected override void OnPauseRequested()
        {
            if (roundState != GomokuRoundState.Ongoing)
            {
                return;
            }

            CancelPendingAiTurn();
            Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        protected override void OnBeforeDispose()
        {
            CancelPendingAiTurn();
            CancelEndRoundAnimation();
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
            var boardShadow = boardFrame.AddComponent<Shadow>();
            boardShadow.effectColor = new Color32(83, 53, 24, 90);
            boardShadow.effectDistance = new Vector2(0f, -5f);

            var boardGrid = CreateUiObject("BoardGrid", boardFrameRect);
            var boardGridRect = boardGrid.GetComponent<RectTransform>();
            StretchWithPadding(boardGridRect, BoardPadding);

            var boardGridImage = boardGrid.AddComponent<Image>();
            boardGridImage.color = BoardGridColor;

            var lineRoot = CreateUiObject("IntersectionLines", boardGridRect);
            var lineRootRect = lineRoot.GetComponent<RectTransform>();
            StretchWithPadding(lineRootRect, IntersectionPadding);
            CreateIntersectionGrid(lineRootRect);

            var cellGrid = CreateUiObject("IntersectionCells", boardGridRect);
            var cellGridRect = cellGrid.GetComponent<RectTransform>();
            StretchWithPadding(cellGridRect, IntersectionPadding);
            var gridLayout = cellGrid.AddComponent<GridLayoutGroup>();
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = BoardSize;
            gridLayout.spacing = Vector2.zero;
            gridLayout.childAlignment = TextAnchor.MiddleCenter;

            var availableSize = BoardFrameSize - (BoardPadding * 2f) - (IntersectionPadding * 2f);
            var cellSize = availableSize / BoardSize;
            gridLayout.cellSize = new Vector2(cellSize, cellSize);

            for (var row = 0; row < BoardSize; row++)
            {
                for (var col = 0; col < BoardSize; col++)
                {
                    cells[row, col] = CreateCell(row, col, cellGridRect);
                }
            }
        }

        private static void CreateIntersectionGrid(RectTransform parent)
        {
            var edgeInset = 0.5f / BoardSize;
            for (var index = 0; index < BoardSize; index++)
            {
                var position = (index + 0.5f) / BoardSize;
                CreateIntersectionLine("HorizontalLine_" + index, parent, false, position, edgeInset);
                CreateIntersectionLine("VerticalLine_" + index, parent, true, position, edgeInset);
            }

            CreateStarPoint(parent, 3, 3);
            CreateStarPoint(parent, 3, 11);
            CreateStarPoint(parent, 7, 7);
            CreateStarPoint(parent, 11, 3);
            CreateStarPoint(parent, 11, 11);
        }

        private static void CreateIntersectionLine(string name, Transform parent, bool vertical, float position, float edgeInset)
        {
            var lineObject = CreateUiObject(name, parent);
            var lineRect = lineObject.GetComponent<RectTransform>();
            if (vertical)
            {
                lineRect.anchorMin = new Vector2(position, edgeInset);
                lineRect.anchorMax = new Vector2(position, 1f - edgeInset);
                lineRect.sizeDelta = new Vector2(2f, 0f);
            }
            else
            {
                lineRect.anchorMin = new Vector2(edgeInset, 1f - position);
                lineRect.anchorMax = new Vector2(1f - edgeInset, 1f - position);
                lineRect.sizeDelta = new Vector2(0f, 2f);
            }

            lineRect.anchoredPosition = Vector2.zero;
            var lineImage = lineObject.AddComponent<Image>();
            lineImage.color = BoardLineColor;
            lineImage.raycastTarget = false;
        }

        private static void CreateStarPoint(Transform parent, int row, int column)
        {
            var pointObject = CreateUiObject("StarPoint_" + row + "_" + column, parent);
            var pointRect = pointObject.GetComponent<RectTransform>();
            var x = (column + 0.5f) / BoardSize;
            var y = 1f - ((row + 0.5f) / BoardSize);
            pointRect.anchorMin = new Vector2(x, y);
            pointRect.anchorMax = new Vector2(x, y);
            pointRect.sizeDelta = new Vector2(8f, 8f);
            pointRect.anchoredPosition = Vector2.zero;
            var pointGraphic = pointObject.AddComponent<GomokuCircleGraphic>();
            pointGraphic.color = BoardLineColor;
            pointGraphic.raycastTarget = false;
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

            var stoneObject = CreateUiObject("Stone", cellObject.transform);
            var stoneRect = stoneObject.GetComponent<RectTransform>();
            StretchWithPadding(stoneRect, 3f);
            var stoneGraphic = stoneObject.AddComponent<GomokuCircleGraphic>();
            stoneGraphic.raycastTarget = false;
            stoneGraphic.enabled = false;

            var previewObject = CreateUiObject("Preview_" + row + "_" + col, cellObject.transform);
            var previewRect = previewObject.GetComponent<RectTransform>();
            StretchWithPadding(previewRect, 4f);
            var previewGraphic = previewObject.AddComponent<GomokuCircleGraphic>();
            previewGraphic.SetDashedOutline(true);
            previewGraphic.raycastTarget = false;
            previewGraphic.enabled = false;

            var markerObject = CreateUiObject("LastMoveMarker_" + row + "_" + col, cellObject.transform);
            var markerRect = markerObject.GetComponent<RectTransform>();
            StretchWithPadding(markerRect, 16f);
            var markerGraphic = markerObject.AddComponent<GomokuCircleGraphic>();
            markerGraphic.color = LastMoveColor;
            markerGraphic.raycastTarget = false;
            markerGraphic.enabled = false;

            return new CellView(cellButton, cellImage, stoneGraphic, previewGraphic, markerGraphic);
        }

        private void OnCellClicked(int row, int col)
        {
            if (roundState != GomokuRoundState.Ongoing || boardState.CurrentTurn != playerStone)
            {
                return;
            }

            if (boardState.GetStone(row, col) != GomokuStone.None)
            {
                return;
            }

            if (previewRow != row || previewColumn != col)
            {
                previewRow = row;
                previewColumn = col;
                MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.7f, 1.12f);
                RefreshBoardUi();
                return;
            }

            if (!boardState.TryPlaceStone(row, col, playerStone, out roundState))
            {
                return;
            }

            previewRow = -1;
            previewColumn = -1;
            lastMoveRow = row;
            lastMoveColumn = col;
            MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.9f, 1.05f);
            RefreshBoardUi();
            RefreshHud();

            if (roundState != GomokuRoundState.Ongoing)
            {
                EndRound();
                return;
            }

            ScheduleAiTurn();
        }

        private void ScheduleAiTurn()
        {
            CancelPendingAiTurn();
            if (roundState != GomokuRoundState.Ongoing || boardState.CurrentTurn != aiStone)
            {
                return;
            }

            aiTurnCoroutine = HostBehaviour.StartCoroutine(ExecuteAiTurnAfterDelay());
        }

        private IEnumerator ExecuteAiTurnAfterDelay()
        {
            yield return new WaitForSeconds(AiMoveDelaySeconds);
            aiTurnCoroutine = null;
            ExecuteAiTurn();
        }

        private void CancelPendingAiTurn()
        {
            if (aiTurnCoroutine == null || HostBehaviour == null)
            {
                aiTurnCoroutine = null;
                return;
            }

            HostBehaviour.StopCoroutine(aiTurnCoroutine);
            aiTurnCoroutine = null;
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
            lastMoveRow = move.Row;
            lastMoveColumn = move.Column;
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
            if (TryGetWinningLine(out var winningLine))
            {
                CancelEndRoundAnimation();
                endRoundCoroutine = HostBehaviour.StartCoroutine(AnimateWinningLineThenShowSettlement(winningLine));
                return;
            }

            ShowEndRoundSettlement();
        }

        private IEnumerator AnimateWinningLineThenShowSettlement(GomokuMove[] winningLine)
        {
            for (var index = 0; index < winningLine.Length; index++)
            {
                var move = winningLine[index];
                var stone = cells[move.Row, move.Column].Stone;
                stone.color = WinningStoneColor;
                stone.rectTransform.localScale = Vector3.one * 1.18f;
                yield return new WaitForSeconds(WinningStoneStepSeconds);
                stone.rectTransform.localScale = Vector3.one;
            }

            var elapsed = 0f;
            while (elapsed < WinningPulseSeconds)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / WinningPulseSeconds);
                var scale = 1f + (Mathf.Sin(progress * Mathf.PI) * 0.18f);
                SetWinningLineScale(winningLine, scale);
                yield return null;
            }

            SetWinningLineScale(winningLine, 1f);
            yield return new WaitForSeconds(SettlementDelayAfterWinningAnimationSeconds);
            endRoundCoroutine = null;
            ShowEndRoundSettlement();
        }

        private void ShowEndRoundSettlement()
        {
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

        private bool TryGetWinningLine(out GomokuMove[] winningLine)
        {
            winningLine = Array.Empty<GomokuMove>();
            if (boardState == null || lastMoveRow < 0 || lastMoveColumn < 0)
            {
                return false;
            }

            var winningStone = roundState == GomokuRoundState.BlackWin ? GomokuStone.Black :
                roundState == GomokuRoundState.WhiteWin ? GomokuStone.White : GomokuStone.None;
            return boardState.TryGetWinningLine(lastMoveRow, lastMoveColumn, winningStone, out winningLine);
        }

        private void SetWinningLineScale(GomokuMove[] winningLine, float scale)
        {
            if (winningLine == null)
            {
                return;
            }

            for (var index = 0; index < winningLine.Length; index++)
            {
                var move = winningLine[index];
                cells[move.Row, move.Column].Stone.rectTransform.localScale = Vector3.one * scale;
            }
        }

        private void CancelEndRoundAnimation()
        {
            if (endRoundCoroutine != null && HostBehaviour != null)
            {
                HostBehaviour.StopCoroutine(endRoundCoroutine);
            }

            endRoundCoroutine = null;
            for (var row = 0; row < BoardSize; row++)
            {
                for (var column = 0; column < BoardSize; column++)
                {
                    if (cells[row, column].Stone != null)
                    {
                        cells[row, column].Stone.rectTransform.localScale = Vector3.one;
                    }
                }
            }
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
                    cell.Preview.enabled = stone == GomokuStone.None && row == previewRow && col == previewColumn;
                    cell.Preview.color = playerStone == GomokuStone.Black ? PreviewBlackColor : PreviewWhiteColor;
                    cell.LastMoveMarker.enabled = stone != GomokuStone.None && row == lastMoveRow && col == lastMoveColumn;
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
            if (roundState == GomokuRoundState.Ongoing && boardState.CurrentTurn == aiStone)
            {
                ScheduleAiTurn();
            }
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

        private void ClearMoveFeedback()
        {
            previewRow = -1;
            previewColumn = -1;
            lastMoveRow = -1;
            lastMoveColumn = -1;
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
            public CellView(
                Button button,
                Image background,
                GomokuCircleGraphic stone,
                GomokuCircleGraphic preview,
                GomokuCircleGraphic lastMoveMarker)
            {
                Button = button;
                Background = background;
                Stone = stone;
                Preview = preview;
                LastMoveMarker = lastMoveMarker;
            }

            public Button Button { get; }

            public Image Background { get; }

            public GomokuCircleGraphic Stone { get; }

            public GomokuCircleGraphic Preview { get; }

            public GomokuCircleGraphic LastMoveMarker { get; }
        }
    }
}
