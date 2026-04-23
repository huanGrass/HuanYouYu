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
            new BreakoutLevelDefinition("breakout.level.classic", "11111111", "11111111", "11111111", "11111111", "11111111"),
            new BreakoutLevelDefinition("breakout.level.hollow_box", "11111111", "10000001", "10000001", "10000001", "11111111"),
            new BreakoutLevelDefinition("breakout.level.cross", "00111100", "00111100", "11111111", "00111100", "00111100"),
            new BreakoutLevelDefinition("breakout.level.stairs", "10000000", "11000000", "11100000", "11110000", "11111000"),
            new BreakoutLevelDefinition("breakout.level.twin_towers", "11000011", "11000011", "11100111", "01111110", "00111100"),
            new BreakoutLevelDefinition("breakout.level.waves", "11001100", "11100110", "01111100", "00111110", "00110011"),
            new BreakoutLevelDefinition("breakout.level.arrow", "00011000", "00111100", "01111110", "11111111", "00111100"),
            new BreakoutLevelDefinition("breakout.level.diamond", "00011000", "00111100", "01111110", "00111100", "00011000"),
            new BreakoutLevelDefinition("breakout.level.spiral_turn", "11110000", "10011100", "11000110", "00111001", "00001111"),
            new BreakoutLevelDefinition("breakout.level.sawtooth", "10101010", "01100110", "00111100", "01100110", "10101010"),
            new BreakoutLevelDefinition("breakout.level.double_ring", "01111110", "01000010", "01100110", "01000010", "01111110"),
            new BreakoutLevelDefinition("breakout.level.spire", "00011000", "00111100", "00111100", "01111110", "11111111"),
            new BreakoutLevelDefinition("breakout.level.wall", "11111111", "10111101", "11111111", "10111101", "11111111"),
            new BreakoutLevelDefinition("breakout.level.diagonal", "10000001", "01000010", "00100100", "01000010", "10000001"),
            new BreakoutLevelDefinition("breakout.level.funnel", "11000011", "01100110", "00111100", "00111100", "00011000"),
            new BreakoutLevelDefinition("breakout.level.bridge", "11111111", "00011000", "11111111", "00011000", "11111111"),
            new BreakoutLevelDefinition("breakout.level.wings", "11000011", "11100111", "01111110", "00111100", "00100100"),
            new BreakoutLevelDefinition("breakout.level.hive", "01100110", "11111111", "01111110", "11111111", "01100110"),
            new BreakoutLevelDefinition("breakout.level.spiral", "11111111", "00000001", "01111101", "01000101", "01111111"),
            new BreakoutLevelDefinition("breakout.level.arch", "00111100", "01100110", "11000011", "11111111", "11000011")
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

            Shell.SetPauseButtonVisible(true);

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
            return ("game.breakout.help", "game.breakout.credits");
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
                UiTextCatalog.Get("breakout.action.restart"),
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
            Shell.ShowSettlementPopup(
                UiTextCatalog.Format("breakout.settlement.exit", score, brokenBrickCount, brokenBrickCount * 2),
                ConfirmSettlement);
        }

        private void ConfirmSettlement()
        {
            Shell.ClosePopup();
            CompleteGame?.Invoke(BuildSettlement());
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
                UiTextCatalog.Get("breakout.action.restart"),
                true,
                true);
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            Shell.ShowSettlementPopup(
                UiTextCatalog.Format("breakout.settlement.lose", score, brokenBrickCount),
                ConfirmSettlement);
        }

        private void OnBoardCleared()
        {
            if (state != BreakoutGameState.Playing)
            {
                return;
            }

            state = BreakoutGameState.Won;
            hud.SetAction(
                UiTextCatalog.Get("breakout.action.restart"),
                true,
                true);
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            Shell.ShowSettlementPopup(
                UiTextCatalog.Format("breakout.settlement.win", score, lives, brokenBrickCount, brokenBrickCount * 2, 1),
                ConfirmSettlement);
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
