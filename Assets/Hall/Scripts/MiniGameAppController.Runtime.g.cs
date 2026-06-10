using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    public sealed partial class MiniGameAppController
    {
        private MiniGameBase CreateGameRuntime(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                return null;
            }

            switch (gameId)
            {
                case TapTreasureGameView.GameIdConstant:
                    return new TapTreasureGameView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case Match3GameView.GameIdConstant:
                    return new Match3GameView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case SnakeGameView.GameIdConstant:
                    return new SnakeGameView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case GameTetrisView.GameIdConstant:
                    return new GameTetrisView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case Game2048View.GameIdConstant:
                    return new Game2048View(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case WatermelonMergeGameView.GameIdConstant:
                    return new WatermelonMergeGameView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case WaterSortGameView.GameIdConstant:
                    return new WaterSortGameView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case MemoryFlipGameView.GameIdConstant:
                    return new MemoryFlipGameView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case GameMinesweeperView.GameIdConstant:
                    return new GameMinesweeperView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case GameSudokuView.GameIdConstant:
                    return new GameSudokuView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case NonogramGameView.GameIdConstant:
                    return new NonogramGameView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case GameGomokuView.GameIdConstant:
                    return new GameGomokuView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case GameReversiView.GameIdConstant:
                    return new GameReversiView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case GameBreakoutView.GameIdConstant:
                    return new GameBreakoutView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case GameGoldMinerView.GameIdConstant:
                    return new GameGoldMinerView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case GameNeedleHitView.GameIdConstant:
                    return new GameNeedleHitView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case GameStardewAIView.GameIdConstant:
                    return new GameStardewAIView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case GameControlPointView.GameIdConstant:
                    return new GameControlPointView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case MiniGameWhacAMoleGameView.GameIdConstant:
                    return new MiniGameWhacAMoleGameView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case MiniGameJumpJumpGameView.GameIdConstant:
                    return new MiniGameJumpJumpGameView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case MiniGameLightsOutGameView.GameIdConstant:
                    return new MiniGameLightsOutGameView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case MiniGameRiverCrossingGameView.GameIdConstant:
                    return new MiniGameRiverCrossingGameView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case MiniGameSlidingPuzzleGameView.GameIdConstant:
                    return new MiniGameSlidingPuzzleGameView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case MiniGameTowerOfHanoiGameView.GameIdConstant:
                    return new MiniGameTowerOfHanoiGameView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case MiniGameWaterPouringGameView.GameIdConstant:
                    return new MiniGameWaterPouringGameView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case MiniGameAkariGameView.GameIdConstant:
                    return new MiniGameAkariGameView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case StackMatchGameView.GameIdConstant:
                    return new StackMatchGameView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case MiniGameBlockPuzzleGameView.GameIdConstant:
                    return new MiniGameBlockPuzzleGameView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case BullsCowsGameView.GameIdConstant:
                    return new BullsCowsGameView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                case ArrowEscapeGameView.GameIdConstant:
                    return new ArrowEscapeGameView(this, rootCanvas.transform, CompleteCurrentGame, ExitCurrentGameToHall);
                default:
                    Debug.LogWarning("未注册小游戏运行时: " + gameId);
                    return null;
            }
        }
    }
}
