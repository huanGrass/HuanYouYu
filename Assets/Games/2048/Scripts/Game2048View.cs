using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using HuanYouYu.Game2048;

namespace HuanYouYu.MiniGameHall
{
    public sealed class Game2048View : MiniGameBase
    {
        public const string GameIdConstant = "game2048";
        private static readonly int[] ChestMilestoneValues = { 512, 1024, 2048, 4096 };
        private static readonly int[] ChestMilestoneRewards = { 1, 1, 2, 3 };
        private const float CanvasReferenceWidth = 1080f;
        private const float CanvasReferenceHeight = 1920f;
        private const float LayoutScale = ((MiniGameAppController.ReferenceWidth / CanvasReferenceWidth) + (MiniGameAppController.ReferenceHeight / CanvasReferenceHeight)) * 0.5f;
        private static readonly Vector3 LayoutScaleVector = new Vector3(LayoutScale, LayoutScale, 1f);

        private static int sessionBestScore;

        private Game2048Board board;
        private Game2048BoardView boardView;
        private RectTransform topRoot;
        private RectTransform bottomRoot;
        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private Button restartButton;
        private string pendingSettlementSummary;
        private Coroutine scorePulseRoutine;

        public Game2048View(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "Game2048View", hostBehaviour, parent, onComplete, onExit)
        {
        }

        protected override void BuildOrBindSections()
        {
            AttachTopSection();
            AttachContentSection();
            AttachBottomSection();
        }

        protected override void ResetGame()
        {
            if (board == null)
            {
                board = new Game2048Board();
            }

            Shell.ClosePopup();
            pendingSettlementSummary = null;
            board.Reset();

            if (boardView != null)
            {
                boardView.SetInputEnabled(true);
                boardView.Refresh(board);
            }

            RefreshScoreLabels();
            StopScorePulse();
        }

        protected override void OnPauseRequested()
        {
            if (board == null || board.State != Game2048GameState.Playing)
            {
                return;
            }

            Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        protected override void OnBeforeDispose()
        {
            Shell.ClosePopup();

            if (boardView != null)
            {
                boardView.SwipePerformed -= HandleSwipe;
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            StopScorePulse();

            if (topRoot != null)
            {
                UnityEngine.Object.Destroy(topRoot.gameObject);
            }

            if (bottomRoot != null)
            {
                UnityEngine.Object.Destroy(bottomRoot.gameObject);
            }
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.2048.help", null);
        }

        private void AttachTopSection()
        {
            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("MiniGame2048Top"));
            topRoot = topBarRefs.Root;
            titleLabel = topBarRefs.TitleText;
            titleLabel.text = UiTextCatalog.GetOrFallback("game.2048.name", "2048");

            scoreLabel = topBarRefs.ScoreText;
            scoreLabel.text = "\u5206\u6570 0";

            Shell.AttachTop(topRoot);
        }

        private void AttachContentSection()
        {
            boardView = Game2048BoardView.Create(Shell.ContentHost, LayoutScale);
            boardView.SwipePerformed += HandleSwipe;
        }

        private void AttachBottomSection()
        {
            var bottomContainerRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("MiniGame2048Bottom"));
            bottomRoot = bottomContainerRefs.Root;
            restartButton = MiniGameShellBottomBarBuilder.CreateRestartButton(bottomContainerRefs.ActionBar).Button;

            MiniGameSfxPlayer.Attach(restartButton, MiniGameSfxType.UiTap, 0.95f);
            restartButton.onClick.AddListener(OnRestartClicked);
            Shell.AttachBottom(bottomRoot);
        }

        private void OnRestartClicked()
        {
            ResetGame();
        }

        private void HandleSwipe(Game2048MoveDirection direction)
        {
            TryApplyMove(direction, HandleMoveCompleted, out _);
        }

        private bool TryApplyMove(Game2048MoveDirection direction, Action<Game2048MoveResult> onCompleted, out Game2048MoveResult result)
        {
            if (board == null || board.State != Game2048GameState.Playing)
            {
                result = default(Game2048MoveResult);
                return false;
            }

            var previousBoard = board.Snapshot();
            result = board.TryMove(direction);
            if (!result.BoardChanged)
            {
                MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.8f);
                return false;
            }

            if (result.ScoreGained > 0)
            {
                MiniGameSfxPlayer.Play(MiniGameSfxType.MatchSuccess, 0.95f);
                PlayScorePulse();
            }
            else
            {
                MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.9f);
            }

            RefreshScoreLabels();
            boardView.SetInputEnabled(false);
            var completedResult = result;
            boardView.PlayMoveAnimation(previousBoard, completedResult, board, delegate { onCompleted?.Invoke(completedResult); });
            return true;
        }

        private void HandleMoveCompleted(Game2048MoveResult result)
        {
            if (board == null || boardView == null)
            {
                return;
            }

            if (result.State == Game2048GameState.Won)
            {
                MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
                pendingSettlementSummary = BuildSettlementSummary("2048.settlement.win");
                ShowRewardSettlement(MiniGameRewardSettlementPanelStyle.Success);
                return;
            }

            if (result.State == Game2048GameState.Lost)
            {
                MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
                pendingSettlementSummary = BuildSettlementSummary("2048.settlement.lose");
                ShowRewardSettlement(MiniGameRewardSettlementPanelStyle.Failure);
                return;
            }

            boardView.SetInputEnabled(true);
        }

        private void ShowRewardSettlement(MiniGameRewardSettlementPanelStyle style)
        {
            if (boardView != null)
            {
                boardView.SetInputEnabled(false);
            }

            var settlement = CreateSettlement();
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "Game2048SettlementPanel",
                    Style = style,
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.Retry,
                    Title = UiTextCatalog.Get(style == MiniGameRewardSettlementPanelStyle.Success ? "2048.settlement.win_title" : "2048.settlement.failure_title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("2048.label.score"), settlement.Score.ToString()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("2048.settlement.highest_tile"), GetHighestTileValue().ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                ResetGame,
                delegate { CompleteGame?.Invoke(settlement); },
                true);
        }

        private MiniGameSettlement CreateSettlement()
        {
            var score = board != null ? board.Score : 0;
            var chestCount = GetChestCountForCurrentBoard();
            return new MiniGameSettlement
            {
                Score = score,
                CoinCount = score,
                ChestCount = chestCount,
                Summary = string.IsNullOrWhiteSpace(pendingSettlementSummary)
                    ? string.Empty
                    : pendingSettlementSummary
            };
        }

        private int GetChestCountForCurrentBoard()
        {
            var highestTile = GetHighestTileValue();
            var chestCount = 0;
            for (var index = 0; index < ChestMilestoneValues.Length && index < ChestMilestoneRewards.Length; index++)
            {
                if (highestTile >= ChestMilestoneValues[index])
                {
                    chestCount += ChestMilestoneRewards[index];
                }
            }

            return chestCount;
        }

        private int GetHighestTileValue()
        {
            if (board == null)
            {
                return 0;
            }

            var snapshot = board.Snapshot();
            var highestTile = 0;
            for (var index = 0; index < snapshot.Length; index++)
            {
                if (snapshot[index] > highestTile)
                {
                    highestTile = snapshot[index];
                }
            }

            return highestTile;
        }

        private void RefreshScoreLabels()
        {
            if (board == null)
            {
                return;
            }

            if (board.Score > sessionBestScore)
            {
                sessionBestScore = board.Score;
            }

            if (scoreLabel != null)
            {
                scoreLabel.text = "\u5206\u6570 " + board.Score;
            }
        }

        private void PlayScorePulse()
        {
            if (HostBehaviour == null || scoreLabel == null)
            {
                return;
            }

            StopScorePulse();
            scorePulseRoutine = HostBehaviour.StartCoroutine(AnimateScorePulse());
        }

        private void StopScorePulse()
        {
            if (scorePulseRoutine != null && HostBehaviour != null)
            {
                HostBehaviour.StopCoroutine(scorePulseRoutine);
                scorePulseRoutine = null;
            }

            if (scoreLabel != null)
            {
                scoreLabel.rectTransform.localScale = Vector3.one;
            }
        }

        private IEnumerator AnimateScorePulse()
        {
            const float duration = 0.18f;
            const float pulseScale = 1.10f;
            var rect = scoreLabel.rectTransform;
            var halfDuration = Mathf.Max(0.01f, duration * 0.5f);
            var elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = EaseOutCubic(Mathf.Clamp01(elapsed / halfDuration));
                rect.localScale = Vector3.one * Mathf.Lerp(1f, pulseScale, progress);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = EaseOutCubic(Mathf.Clamp01(elapsed / halfDuration));
                rect.localScale = Vector3.one * Mathf.Lerp(pulseScale, 1f, progress);
                yield return null;
            }

            rect.localScale = Vector3.one;
            scorePulseRoutine = null;
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
        }

        private void ConfirmExitToHall()
        {
            if (board == null)
            {
                return;
            }

            if (boardView != null)
            {
                boardView.SetInputEnabled(false);
            }

            Shell.ClosePopup();
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            pendingSettlementSummary = BuildSettlementSummary("2048.settlement.exit");
            var settlement = CreateSettlement();
            ShowBackHallRewardSettlementPanel(
                settlement,
                "Game2048SettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("2048.label.score"), settlement.Score.ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("2048.settlement.highest_tile"), GetHighestTileValue().ToString()),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private string BuildSettlementSummary(string textKey)
        {
            var score = board != null ? board.Score : 0;
            return UiTextCatalog.Format(textKey, score, sessionBestScore, GetChestCountForCurrentBoard());
        }

        private static float EaseOutCubic(float value)
        {
            var inverse = 1f - value;
            return 1f - (inverse * inverse * inverse);
        }

    }
}
