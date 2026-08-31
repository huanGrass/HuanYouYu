using System.Collections;
using System.Collections.Generic;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Tests
{
    public class BlockPuzzleLogicTests
    {
        [Test]
        public void BoardRejectsOutOfBoundsAndOverlappingPlacements()
        {
            var board = new BlockPuzzleBoard();
            var horizontalTwo = BlockPuzzlePieceLibrary.CreateFromCells(
                "h2_test",
                new BlockPuzzleCell(0, 0),
                new BlockPuzzleCell(1, 0));

            Assert.IsTrue(board.CanPlace(horizontalTwo, 8, 9));
            Assert.IsFalse(board.CanPlace(horizontalTwo, 9, 0));

            var single = BlockPuzzlePieceLibrary.CreateFromCells("single_test", new BlockPuzzleCell(0, 0));
            Assert.IsTrue(board.PlacePiece(single, 0, 0).Success);
            Assert.IsFalse(board.CanPlace(horizontalTwo, 0, 0));
        }

        [Test]
        public void BoardClearsFullRowAndColumnTogether()
        {
            var board = new BlockPuzzleBoard();
            for (var x = 1; x < BlockPuzzleBoard.Size; x++)
            {
                board.SetCell(x, 0, 1);
            }

            for (var y = 1; y < BlockPuzzleBoard.Size; y++)
            {
                board.SetCell(0, y, 2);
            }

            var single = BlockPuzzlePieceLibrary.CreateFromCells("single_test", 3, new BlockPuzzleCell(0, 0));
            var result = board.PlacePiece(single, 0, 0);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.LinesCleared);
            Assert.AreEqual(251, result.ScoreEarned);
            Assert.AreEqual(0, board.CountOccupiedCells());
        }

        [Test]
        public void GameStateRefreshesTrayAfterAllThreePiecesArePlaced()
        {
            var pieces = new Queue<BlockPuzzlePiece>();
            for (var i = 0; i < 6; i++)
            {
                pieces.Enqueue(BlockPuzzlePieceLibrary.CreateFromCells("single_" + i, i + 1, new BlockPuzzleCell(0, 0)));
            }

            var state = new BlockPuzzleGameState(delegate { return pieces.Dequeue(); });
            state.Reset();

            Assert.IsFalse(state.TryPlaceTrayPiece(0, 0, 0).TrayRefreshed);
            Assert.IsFalse(state.TryPlaceTrayPiece(1, 1, 0).TrayRefreshed);
            var thirdMove = state.TryPlaceTrayPiece(2, 2, 0);

            Assert.IsTrue(thirdMove.Success);
            Assert.IsTrue(thirdMove.TrayRefreshed);
            Assert.AreEqual(3, state.Score);
            Assert.IsNotNull(state.GetTrayPiece(0));
            Assert.IsNotNull(state.GetTrayPiece(1));
            Assert.IsNotNull(state.GetTrayPiece(2));
        }

        [Test]
        public void GameStateReportsGameOverWhenNoTrayPieceFits()
        {
            var single = BlockPuzzlePieceLibrary.CreateFromCells("single_test", new BlockPuzzleCell(0, 0));
            var state = new BlockPuzzleGameState(delegate { return single; });
            state.Reset();

            for (var y = 0; y < BlockPuzzleBoard.Size; y++)
            {
                for (var x = 0; x < BlockPuzzleBoard.Size; x++)
                {
                    state.Board.SetCell(x, y, 1);
                }
            }

            state.SetTrayPieces(single, null, null);

            Assert.IsTrue(state.IsGameOver);
            var move = state.TryPlaceTrayPiece(0, 0, 0);
            Assert.IsFalse(move.Success);
            Assert.IsTrue(move.GameOver);
        }

        [UnityTest]
        public IEnumerator DraggedPieceStaysAbovePointer()
        {
            var hostObject = new GameObject("BlockPuzzleDragTestHost");
            var controller = hostObject.AddComponent<MiniGameAppController>();
            yield return null;

            controller.EnterGame(MiniGameBlockPuzzleGameView.GameIdConstant);
            yield return null;

            var slot = GameObject.Find("TraySlot_0").GetComponent<RectTransform>();
            var content = GameObject.Find("BlockPuzzleContent").GetComponent<RectTransform>();
            var pointerPosition = RectTransformUtility.WorldToScreenPoint(null, slot.position);
            var eventData = new PointerEventData(EventSystem.current) { position = pointerPosition };
            ExecuteEvents.Execute(slot.gameObject, eventData, ExecuteEvents.beginDragHandler);

            var piece = GameObject.Find("Piece_0").GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(content, pointerPosition, null, out var pointerLocalPoint);
            Assert.GreaterOrEqual(piece.anchoredPosition.y - pointerLocalPoint.y, 120f,
                "Dragged piece should be lifted far enough to remain visible above the pointer.");

            Object.Destroy(hostObject);
            yield return null;
        }
    }
}
