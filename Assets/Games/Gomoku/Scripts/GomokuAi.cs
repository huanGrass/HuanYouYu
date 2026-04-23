namespace HuanYouYu.MiniGameHall
{
    internal static class GomokuAi
    {
        private const int CandidateRadius = 2;

        public static GomokuMove ChooseMove(GomokuBoardState boardState, GomokuStone aiStone, GomokuStone playerStone)
        {
            if (boardState == null)
            {
                return new GomokuMove(-1, -1);
            }

            GomokuMove bestAttackMove = new GomokuMove(-1, -1);
            var bestAttackScore = int.MinValue;
            GomokuMove bestBlockMove = new GomokuMove(-1, -1);
            var bestBlockScore = int.MinValue;

            foreach (var candidate in boardState.EnumerateCandidateMoves(CandidateRadius))
            {
                if (boardState.WouldWin(candidate.Row, candidate.Column, aiStone))
                {
                    return candidate;
                }

                if (boardState.WouldWin(candidate.Row, candidate.Column, playerStone))
                {
                    var blockScore = boardState.EvaluateMove(candidate.Row, candidate.Column, playerStone);
                    if (IsBetterCandidate(boardState, candidate, blockScore, bestBlockMove, bestBlockScore))
                    {
                        bestBlockMove = candidate;
                        bestBlockScore = blockScore;
                    }

                    continue;
                }

                var attackScore = boardState.EvaluateMove(candidate.Row, candidate.Column, aiStone) * 2;
                attackScore += boardState.EvaluateMove(candidate.Row, candidate.Column, playerStone);
                if (IsBetterCandidate(boardState, candidate, attackScore, bestAttackMove, bestAttackScore))
                {
                    bestAttackMove = candidate;
                    bestAttackScore = attackScore;
                }
            }

            if (bestBlockMove.IsValid)
            {
                return bestBlockMove;
            }

            return bestAttackMove;
        }

        private static bool IsBetterCandidate(
            GomokuBoardState boardState,
            GomokuMove candidate,
            int candidateScore,
            GomokuMove currentBestMove,
            int currentBestScore)
        {
            if (!currentBestMove.IsValid)
            {
                return true;
            }

            if (candidateScore != currentBestScore)
            {
                return candidateScore > currentBestScore;
            }

            return boardState.DistanceToCenter(candidate.Row, candidate.Column) <
                boardState.DistanceToCenter(currentBestMove.Row, currentBestMove.Column);
        }
    }
}
