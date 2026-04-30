using System;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    public sealed class GameBreakoutView : MiniGameBase
    {
        public const string GameIdConstant = "breakout";

        private const int InitialLives = 3;
        private const int PointsPerBrick = 100;
        private static readonly BreakoutLevelDefinition[] LevelPool =
        {
            new BreakoutLevelDefinition("breakout.level.classic", "11111111111", "11111111111", "11111111111", "11111111111", "11111111111", "11111111111", "11111111111", "11111111111", "11111111111", "11111111111", "11111111111", "11111111111", "11111111111", "11111111111", "11111111111"),
            new BreakoutLevelDefinition("breakout.level.hollow_box", "11111111111", "10000000001", "10000000001", "10001110001", "10001010001", "10001110001", "10000000001", "10000000001", "10001110001", "10001010001", "10001110001", "10000000001", "10000000001", "10000000001", "11111111111"),
            new BreakoutLevelDefinition("breakout.level.cross", "00001110000", "00001110000", "00001110000", "00001110000", "11111111111", "11111111111", "11111111111", "00001110000", "00001110000", "00001110000", "11111111111", "11111111111", "00001110000", "00001110000", "00001110000"),
            new BreakoutLevelDefinition("breakout.level.stairs", "10000000000", "11000000000", "11100000000", "11110000000", "11111000000", "11111100000", "11111110000", "11111111000", "11111111100", "11111111110", "11111111111", "01111111111", "00111111111", "00011111111", "00001111111"),
            new BreakoutLevelDefinition("breakout.level.twin_towers", "11000000011", "11000000011", "11100000111", "11100000111", "11110001111", "11110001111", "01111111110", "01111111110", "00111111100", "00111011100", "00110001100", "11100000111", "11100000111", "11000000011", "11000000011"),
            new BreakoutLevelDefinition("breakout.level.waves", "11001100110", "11100110011", "01110011001", "00111100110", "00011110011", "00111100110", "01110011001", "11100110011", "11001100110", "10011100111", "00111110011", "01110011100", "11100110011", "11001100110", "10011001100"),
            new BreakoutLevelDefinition("breakout.level.arrow", "00000100000", "00001110000", "00011111000", "00111111100", "01111111110", "11111111111", "00011111000", "00011111000", "00011111000", "00011111000", "00011111000", "00011111000", "00011111000", "00011111000", "00011111000"),
            new BreakoutLevelDefinition("breakout.level.diamond", "00000100000", "00001110000", "00011111000", "00111111100", "01111111110", "11111111111", "01111111110", "00111111100", "00011111000", "00001110000", "00000100000", "00001110000", "00011111000", "00001110000", "00000100000"),
            new BreakoutLevelDefinition("breakout.level.spiral_turn", "11111100000", "10000111000", "10110001100", "10111100110", "10000110010", "11110111010", "00010100010", "01110101110", "01000101000", "01011101111", "01011000001", "01001111101", "01100000101", "00111110101", "00000011111"),
            new BreakoutLevelDefinition("breakout.level.sawtooth", "10101010101", "01010101010", "11100111001", "01110011100", "00111110011", "00011111000", "00111111100", "01111111110", "11100111001", "01010101010", "10101010101", "11100111001", "01110011100", "00111110011", "00011111000"),
            new BreakoutLevelDefinition("breakout.level.double_ring", "01111111110", "01000000010", "01011111010", "01010001010", "01010101010", "01010001010", "01011111010", "01000000010", "01111111110", "00011111000", "00010001000", "00011111000", "00000000000", "00111111100", "00100000100"),
            new BreakoutLevelDefinition("breakout.level.spire", "00000100000", "00001110000", "00011111000", "00011111000", "00111111100", "00111111100", "01111111110", "01111111110", "11111111111", "11111111111", "00111111100", "00111111100", "01111111110", "11111111111", "11111111111"),
            new BreakoutLevelDefinition("breakout.level.wall", "11111111111", "10111111101", "11111111111", "11100100111", "11111111111", "10111111101", "11111111111", "11100100111", "11111111111", "10111111101", "11111111111", "11100100111", "11111111111", "10111111101", "11111111111"),
            new BreakoutLevelDefinition("breakout.level.diagonal", "10000000001", "01000000010", "00100000100", "00010001000", "00001010000", "00000100000", "00001010000", "00010001000", "00100000100", "01000000010", "10000000001", "11000000011", "01100000110", "00110001100", "00011111000"),
            new BreakoutLevelDefinition("breakout.level.funnel", "11100000111", "11110001111", "01111011110", "00111111100", "00011111000", "00001110000", "00000100000", "00001110000", "00011111000", "00111111100", "01111011110", "11110001111", "11100000111", "11000000011", "10000000001"),
            new BreakoutLevelDefinition("breakout.level.bridge", "11111111111", "00001110000", "11111111111", "10001110001", "11111111111", "00001110000", "11111111111", "10001110001", "11111111111", "00001110000", "11111111111", "10001110001", "11111111111", "00001110000", "11111111111"),
            new BreakoutLevelDefinition("breakout.level.wings", "11000000011", "11100000111", "11110001111", "11111011111", "01111111110", "00111111100", "00011111000", "00001110000", "00111111100", "01111111110", "11111011111", "11110001111", "11100000111", "11000000011", "10000000001"),
            new BreakoutLevelDefinition("breakout.level.hive", "01100110011", "11111111111", "01111111110", "11111111111", "11001100110", "11111111111", "01111111110", "11111111111", "11001100110", "11111111111", "01111111110", "11111111111", "11001100110", "11111111111", "01100110011"),
            new BreakoutLevelDefinition("breakout.level.spiral", "11111111111", "00000000001", "01111111101", "01000000101", "01011110101", "01010010101", "01010110101", "01010000101", "01011111101", "01000000000", "01111111110", "00000000010", "11111111010", "10000001010", "11111111110"),
            new BreakoutLevelDefinition("breakout.level.arch", "00011111000", "00110001100", "01100000110", "11000000011", "11000000011", "11000000011", "11111111111", "11000000011", "11000000011", "11000000011", "11111111111", "01100000110", "00110001100", "00011111000", "00001110000")
        };

        private BreakoutGameState state;
        private BreakoutGameState resumeState;
        private BreakoutBoard board;
        private BreakoutHud hud;
        private BreakoutInput input;
        private int score;
        private int lives;
        private int brokenBrickCount;
        private int currentLevelIndex = -1;
        private BreakoutLevelDefinition currentLevel;

        public GameBreakoutView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "GameBreakoutView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        public override void Tick(float deltaTime)
        {
            if (hud == null || board == null || input == null)
            {
                return;
            }

            board.TickVisualEffects(deltaTime);

            var snapshot = input.Sample(deltaTime);
            if (snapshot.HasPointer)
            {
                board.SetPaddlePosition(snapshot.PointerBoardX);
            }
            else if (Mathf.Abs(snapshot.KeyboardDelta) > 0.01f)
            {
                board.MovePaddle(snapshot.KeyboardDelta);
            }

            if (snapshot.LaunchRequested && state == BreakoutGameState.ReadyToLaunch)
            {
                LaunchBall();
            }

            if (state == BreakoutGameState.ReadyToLaunch)
            {
                board.SyncAttachedBall();
                return;
            }

            if (state != BreakoutGameState.Playing)
            {
                return;
            }

            board.Tick(deltaTime);
        }

        protected override void BuildOrBindSections()
        {
            hud = new BreakoutHud(Shell.TopHost, Shell.BottomHost);
            board = new BreakoutBoard(Shell.ContentHost);
            input = new BreakoutInput(board.BoardRect);

            hud.ActionRequested += OnActionRequested;
            board.BrickBroken += OnBrickBroken;
            board.BallLost += OnBallLost;
            board.BoardCleared += OnBoardCleared;
        }

        protected override void ResetGame()
        {
            StartNewGame();
        }

        protected override void OnPauseRequested()
        {
            if (state != BreakoutGameState.ReadyToLaunch && state != BreakoutGameState.Playing)
            {
                return;
            }

            resumeState = state;
            state = BreakoutGameState.Paused;
            Shell.ShowPausePopup(ResumeGame, ConfirmExitToHall);
        }

        protected override void OnBeforeDispose()
        {
            if (hud != null)
            {
                hud.ActionRequested -= OnActionRequested;
            }

            if (board != null)
            {
                board.BrickBroken -= OnBrickBroken;
                board.BallLost -= OnBallLost;
                board.BoardCleared -= OnBoardCleared;
            }

            Shell.ClosePopup();

            if (board != null)
            {
                board.Dispose();
                board = null;
            }

            if (hud != null)
            {
                hud.Dispose();
                hud = null;
            }
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.breakout.help", null);
        }

        private void StartNewGame()
        {
            SelectRandomLevel();
            score = 0;
            lives = InitialLives;
            brokenBrickCount = 0;
            state = BreakoutGameState.ReadyToLaunch;
            resumeState = BreakoutGameState.ReadyToLaunch;

            Shell.ClosePopup();
            board.SetLevel(currentLevel);
            board.ResetBoard();
            board.SyncAttachedBall();

            hud.SetTitle(UiTextCatalog.Get("game.breakout.name"));
            hud.SetLevel(GetCurrentLevelName());
            hud.SetScore(score);
            hud.SetLives(lives);
            hud.SetAction(
                UiTextCatalog.Get("breakout.action.launch"),
                true,
                true);
        }

        private void SelectRandomLevel()
        {
            if (LevelPool.Length == 0)
            {
                throw new InvalidOperationException("Breakout level pool is empty.");
            }

            if (LevelPool.Length == 1)
            {
                currentLevel = LevelPool[0];
                currentLevelIndex = 0;
                return;
            }

            var maxExclusive = currentLevelIndex >= 0 ? LevelPool.Length - 1 : LevelPool.Length;
            var nextIndex = UnityEngine.Random.Range(0, maxExclusive);
            if (currentLevelIndex >= 0 && nextIndex >= currentLevelIndex)
            {
                nextIndex += 1;
            }

            currentLevelIndex = nextIndex;
            currentLevel = LevelPool[currentLevelIndex];
        }

        private string GetCurrentLevelName()
        {
            if (currentLevel == null)
            {
                return UiTextCatalog.Get("breakout.level.classic");
            }

            return UiTextCatalog.Get(currentLevel.NameKey);
        }

        private void LaunchBall()
        {
            if (state != BreakoutGameState.ReadyToLaunch)
            {
                return;
            }

            board.LaunchBall();
            state = BreakoutGameState.Playing;
            hud.SetAction(
                UiTextCatalog.Get("common.action.restart"),
                true,
                true);
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
        }

        private void ResumeGame()
        {
            Shell.ClosePopup();
            state = resumeState;
        }

        private void ConfirmExitToHall()
        {
            if (state != BreakoutGameState.ReadyToLaunch && state != BreakoutGameState.Playing && state != BreakoutGameState.Paused)
            {
                return;
            }

            resumeState = state;
            state = BreakoutGameState.Paused;
            Shell.ClosePopup();
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            var settlement = BuildSettlement();
            ShowBackHallRewardSettlementPanel(
                settlement,
                "BreakoutSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("breakout.settlement.score"), score.ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("breakout.settlement.bricks"), brokenBrickCount.ToString()),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private void OnActionRequested()
        {
            if (state == BreakoutGameState.ReadyToLaunch)
            {
                LaunchBall();
                return;
            }

            if (state == BreakoutGameState.Playing || state == BreakoutGameState.Won || state == BreakoutGameState.Lost)
            {
                StartNewGame();
            }
        }

        private void OnBrickBroken()
        {
            brokenBrickCount += 1;
            score += PointsPerBrick;
            hud.SetScore(score);
        }

        private void OnBallLost()
        {
            if (state != BreakoutGameState.Playing)
            {
                return;
            }

            lives -= 1;
            hud.SetLives(lives);
            MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.9f);

            if (lives > 0)
            {
                state = BreakoutGameState.ReadyToLaunch;
                board.AttachBallToPaddle();
                board.SyncAttachedBall();
                hud.SetAction(
                    UiTextCatalog.Get("breakout.action.launch"),
                    true,
                    true);
                return;
            }

            state = BreakoutGameState.Lost;
            hud.SetAction(
                UiTextCatalog.Get("common.action.restart"),
                true,
                true);
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            ShowRoundSettlementPanel(BuildSettlement(), MiniGameRewardSettlementPanelStyle.Failure);
        }

        private void OnBoardCleared()
        {
            if (state != BreakoutGameState.Playing)
            {
                return;
            }

            state = BreakoutGameState.Won;
            hud.SetAction(
                UiTextCatalog.Get("common.action.restart"),
                true,
                true);
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            ShowRoundSettlementPanel(BuildSettlement(), MiniGameRewardSettlementPanelStyle.Success);
        }

        private void ShowRoundSettlementPanel(MiniGameSettlement settlement, MiniGameRewardSettlementPanelStyle style)
        {
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "BreakoutSettlementPanel",
                    Style = style,
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.Retry,
                    Title = UiTextCatalog.Get(style == MiniGameRewardSettlementPanelStyle.Success ? "breakout.settlement.win_title" : "breakout.settlement.failure_title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("breakout.settlement.score"), score.ToString()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(
                        UiTextCatalog.Get(style == MiniGameRewardSettlementPanelStyle.Success ? "breakout.settlement.lives" : "breakout.settlement.bricks"),
                        (style == MiniGameRewardSettlementPanelStyle.Success ? lives : brokenBrickCount).ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                StartNewGame,
                delegate { CompleteGame?.Invoke(settlement); },
                true);
        }

        private MiniGameSettlement BuildSettlement()
        {
            var coinCount = brokenBrickCount * 2;
            var chestCount = state == BreakoutGameState.Won ? 1 : 0;
            var summary = state == BreakoutGameState.Won
                ? UiTextCatalog.Format("breakout.settlement.win", score, lives, brokenBrickCount, coinCount, chestCount)
                : state == BreakoutGameState.Lost
                    ? UiTextCatalog.Format("breakout.settlement.lose", score, brokenBrickCount, coinCount)
                    : UiTextCatalog.Format("breakout.settlement.exit", score, brokenBrickCount, coinCount);

            return new MiniGameSettlement
            {
                Score = score,
                CoinCount = coinCount,
                ChestCount = chestCount,
                Summary = summary
            };
        }

        private enum BreakoutGameState
        {
            ReadyToLaunch,
            Playing,
            Paused,
            Won,
            Lost
        }
    }

    internal enum BreakoutPowerUpType
    {
        SplitCurrentBalls,
        ExtraServeBalls
    }

    internal sealed class BreakoutLevelDefinition
    {
        public BreakoutLevelDefinition(string nameKey, params string[] rows)
        {
            NameKey = nameKey ?? string.Empty;
            Rows = rows ?? Array.Empty<string>();
        }

        public string NameKey { get; }

        public string[] Rows { get; }
    }
}
