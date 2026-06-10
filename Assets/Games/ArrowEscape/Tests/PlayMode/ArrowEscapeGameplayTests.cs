using System;
using System.Collections;
using System.Reflection;
using System.Text;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests
{
    public sealed class ArrowEscapeGameplayTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        [Test]
        public void ArrowEscapeTextResourceExists()
        {
            Assert.IsNotNull(Resources.Load<TextAsset>("Text/arrow-escape.ui_texts.zh-CN"), "ArrowEscape text catalog should exist.");
        }

        [Test]
        public void GeneratedPuzzleSolutionAlwaysClearsBoard()
        {
            var puzzle = ArrowEscapeGameView.GeneratePuzzle(new[]
            {
                "111111",
                "111111",
                "110111",
                "111011",
                "111111",
                "111111"
            }, 123);

            var active = Copy(puzzle.Layout);
            Assert.GreaterOrEqual(CountActive(puzzle.Layout), 34, "Generated puzzle should keep a dense board.");
            Assert.AreEqual(puzzle.Pieces.Length, puzzle.Solution.Length, "Generated puzzle should expose one solution step for each arrow piece.");
            AssertPuzzlePiecesCoverActiveLayout(puzzle);
            for (var i = 0; i < puzzle.Solution.Length; i++)
            {
                var cell = puzzle.Solution[i];
                var pieceCells = FindPuzzlePieceByHead(puzzle, cell);
                Assert.IsTrue(ArrowEscapeGameView.CanEscapePiece(active, puzzle.Directions, pieceCells), "Solution step should be currently clear: " + i);
                for (var c = 0; c < pieceCells.Length; c++)
                {
                    var pieceCell = pieceCells[c];
                    active[pieceCell.x, pieceCell.y] = false;
                }
            }

            Assert.AreEqual(0, CountActive(active), "Following the generated solution should clear every arrow.");
        }

        [Test]
        public void BlockedArrowCannotEscapeUntilFrontCellIsCleared()
        {
            var active = new bool[2, 1];
            active[0, 0] = true;
            active[1, 0] = true;
            var directions = new int[2, 1];
            directions[0, 0] = 1;
            directions[1, 0] = 1;

            Assert.IsFalse(ArrowEscapeGameView.CanEscape(active, directions, 0, 0));
            active[1, 0] = false;
            Assert.IsTrue(ArrowEscapeGameView.CanEscape(active, directions, 0, 0));
        }

        [UnityTest]
        public IEnumerator RestartGeneratesDifferentPuzzle()
        {
            ResetProgress();
            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(ArrowEscapeGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            var runtime = GetRuntime(controller);
            var firstSignature = BuildPuzzleSignature(GetPuzzle(runtime));
            var changed = false;
            for (var i = 0; i < 4 && !changed; i++)
            {
                ClickButton("RestartButton");
                yield return null;
                Canvas.ForceUpdateCanvases();
                yield return null;

                changed = BuildPuzzleSignature(GetPuzzle(runtime)) != firstSignature;
            }

            Assert.IsTrue(changed, "Restarting ArrowEscape should generate a different puzzle.");
        }

        [UnityTest]
        public IEnumerator CanEnterUseHintUndoAndClearFirstLevel()
        {
            ResetProgress();
            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(ArrowEscapeGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.IsTrue(controller.HasActiveGame, "ArrowEscape should become active.");
            Assert.IsNotNull(GameObject.Find("ArrowEscapeView"), "ArrowEscape root should exist.");
            Assert.IsNotNull(GameObject.Find("ArrowEscapeBoard"), "ArrowEscape board should exist.");
            var runtime = GetRuntime(controller);
            var puzzle = GetPuzzle(runtime);
            var expectedTileCount = puzzle.Layout.GetLength(0) * puzzle.Layout.GetLength(1);
            var expectedRemaining = CountActive(puzzle.Layout);
            Assert.AreEqual(expectedTileCount, CountObjectsWithPrefix("ArrowEscapeTile_"), "First level should render the whole simple board.");
            Assert.LessOrEqual(expectedRemaining, 25, "First ArrowEscape level should stay low difficulty.");
            AssertNoAdjacentSameDirectionPieces(puzzle);
            AssertButtonExists("ArrowEscapeUndoButton");
            AssertButtonExists("ArrowEscapeHintButton");
            Assert.IsNull(FindButton("ArrowEscapeLevelSelectButton"), "ArrowEscape should not show a level select button.");
            AssertButtonExists("RestartButton");

            ClickButton("ArrowEscapeHintButton");
            yield return null;

            var first = puzzle.Solution[0];
            var firstTile = GameObject.Find("ArrowEscapeTile_" + first.x + "_" + first.y)?.GetComponent<RectTransform>();
            Assert.IsNotNull(firstTile, "First cleared arrow tile should exist.");
            var firstPiece = GetPieceAtCell(runtime, first);
            Assert.IsNotNull(firstPiece, "First cleared arrow piece should exist.");
            var firstVisualStart = GetPieceVisualPoints(firstPiece);
            var firstTileCanvasGroup = firstTile.GetComponent<CanvasGroup>();
            Assert.IsNotNull(firstTileCanvasGroup, "First cleared arrow tile should have a CanvasGroup.");
            ClickButton("ArrowEscapeTile_" + first.x + "_" + first.y);
            for (var i = 0; i < 60; i++)
            {
                yield return null;
            }

            var firstVisualMoving = GetPieceVisualPoints(firstPiece);
            Assert.Greater(Vector2.Distance(firstVisualStart[0], firstVisualMoving[0]), 80f, "Cleared arrow should visibly fly toward its direction.");
            Assert.Greater(firstTileCanvasGroup.alpha, 0.2f, "Cleared arrow should remain visible during the slower snake-style fly-out animation.");
            yield return WaitForFlyAnimation(runtime);
            Assert.Less(GetIntField(runtime, "remainingTileCount"), expectedRemaining, "One cleared arrow piece should reduce remaining count.");

            ClickButton("ArrowEscapeUndoButton");
            yield return null;
            Assert.AreEqual(expectedRemaining, GetIntField(runtime, "remainingTileCount"), "Undo should restore the cleared arrow.");

            puzzle = GetPuzzle(runtime);
            for (var i = 0; i < puzzle.Solution.Length && GetIntField(runtime, "remainingTileCount") > 0; i++)
            {
                var cell = puzzle.Solution[i];
                if (!GetActiveTiles(runtime)[cell.x, cell.y] || !TryClickButton("ArrowEscapeTile_" + cell.x + "_" + cell.y))
                {
                    continue;
                }

                yield return WaitForFlyAnimation(runtime);
            }

            Assert.IsNotNull(GameObject.Find("ArrowEscapeSettlementPanel"), "Clearing all arrows should show settlement.");
            var progress = controller.GetProgress(ArrowEscapeGameView.GameIdConstant);
            Assert.AreEqual(2, progress.UnlockedLevelCount, "Clearing level 1 should unlock level 2.");
        }

        [UnityTest]
        public IEnumerator FlyingArrowBodyFollowsHeadAsConnectedSnake()
        {
            ResetProgress();
            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(ArrowEscapeGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            var runtime = GetRuntime(controller);
            var piece = FindEscapablePieceWithBody(runtime);
            Assert.IsNotNull(piece, "ArrowEscape should have an immediately escapable multi-cell arrow for body animation coverage.");
            var head = GetPieceHead(piece);
            var startPositions = GetPieceVisualPoints(piece);

            ClickButton("ArrowEscapeTile_" + head.x + "_" + head.y);
            Vector2[] movingPositions = null;
            for (var i = 0; i < 90; i++)
            {
                yield return null;
                movingPositions = GetPieceVisualPoints(piece);
                if (Vector2.Distance(startPositions[0], movingPositions[0]) > 40f)
                {
                    break;
                }
            }

            Assert.IsNotNull(movingPositions, "Flying arrow positions should be sampled.");
            Assert.Greater(Vector2.Distance(startPositions[0], movingPositions[0]), 40f, "Arrow head should move at a visible fixed speed.");
            AssertSnakeBodyStaysConnected(startPositions, movingPositions);
            yield return WaitForFlyAnimation(runtime);
        }

        [UnityTest]
        public IEnumerator CanClickNextEscapableArrowWhilePreviousIsFlying()
        {
            ResetProgress();
            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(ArrowEscapeGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            var runtime = GetRuntime(controller);
            var puzzle = GetPuzzle(runtime);
            Assert.GreaterOrEqual(puzzle.Solution.Length, 2, "ArrowEscape should have at least two solution steps.");

            var first = puzzle.Solution[0];
            var second = puzzle.Solution[1];
            var firstPiece = GetPieceAtCell(runtime, first);
            var secondPiece = GetPieceAtCell(runtime, second);
            Assert.IsNotNull(firstPiece, "First solution piece should exist.");
            Assert.IsNotNull(secondPiece, "Second solution piece should exist.");
            var expectedRemaining = GetIntField(runtime, "remainingTileCount") - GetPieceCells(firstPiece).Length - GetPieceCells(secondPiece).Length;

            ClickButton("ArrowEscapeTile_" + first.x + "_" + first.y);
            yield return null;
            Assert.IsTrue(GetBoolField(runtime, "isAnimating"), "First arrow should still be flying.");

            ClickButton("ArrowEscapeTile_" + second.x + "_" + second.y);
            Assert.AreEqual(expectedRemaining, GetIntField(runtime, "remainingTileCount"), "Second arrow should clear before the first fly-out finishes.");
            yield return WaitForFlyAnimation(runtime);
        }

        [UnityTest]
        public IEnumerator ArrowEscapeScreenshotHasDenseNonOverlappingLayout()
        {
            ResetProgress();
            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.SetLevelProgress(ArrowEscapeGameView.GameIdConstant, 1, 2);
            controller.EnterGame(ArrowEscapeGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            var runtime = GetRuntime(controller);
            var puzzle = GetPuzzle(runtime);
            Assert.GreaterOrEqual(CountActive(puzzle.Layout), 470, "Second ArrowEscape level should be very hard.");
            AssertNoAdjacentSameDirectionPieces(puzzle);
            AssertRenderedArrowsUseContinuousMazeSegments(runtime, CountActive(puzzle.Layout));
            AssertInitialPlayableSurfaceIsLimited(runtime, CountActive(puzzle.Layout));

            AssertChildStaysInside("ArrowEscapeContent", "ArrowEscapeBoardPanel");
            AssertChildStaysInside("ArrowEscapeControls", "ArrowEscapeToolRow");
            Assert.GreaterOrEqual(CountObjectsWithPrefix("ArrowEscapeTile_"), 25, "Board should have enough visible tile cells.");
            Assert.IsNotNull(GameObject.Find("ArrowEscapeZoomControl"), "ArrowEscape should expose a zoom control for larger boards.");
            AssertZoomSliderScalesBoard();
            AssertRectSizeAtLeast("ArrowEscapeBoard", 560f, 760f);
            AssertRectSizeAtLeast("ArrowEscapeTile_0_0", 30f, 30f);
        }

        private static IEnumerator LoadController(Action<MiniGameAppController> onLoaded)
        {
            var load = SceneManager.LoadSceneAsync("SampleScene");
            while (!load.isDone)
            {
                yield return null;
            }

            MiniGameAppController controller = null;
            for (var i = 0; i < 1000; i++)
            {
                controller = Object.FindObjectOfType<MiniGameAppController>();
                if (controller != null)
                {
                    break;
                }

                yield return null;
            }

            Assert.IsNotNull(controller, "MiniGameAppController was not created.");
            onLoaded(controller);
        }

        private static IEnumerator WaitForFlyAnimation(ArrowEscapeGameView runtime)
        {
            var field = typeof(ArrowEscapeGameView).GetField("isAnimating", InstancePrivate);
            Assert.IsNotNull(field, "isAnimating field should be accessible for tests.");
            for (var i = 0; i < 1000; i++)
            {
                if (!(bool)field.GetValue(runtime))
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("ArrowEscape fly animation did not finish in time.");
        }

        private static ArrowEscapeGameView GetRuntime(MiniGameAppController controller)
        {
            var activeGameField = typeof(MiniGameAppController).GetField("activeGame", InstancePrivate);
            Assert.IsNotNull(activeGameField, "activeGame field should be accessible.");
            var runtime = activeGameField.GetValue(controller) as ArrowEscapeGameView;
            Assert.IsNotNull(runtime, "ArrowEscape runtime should be active.");
            return runtime;
        }

        private static ArrowEscapePuzzleData GetPuzzle(ArrowEscapeGameView runtime)
        {
            var field = typeof(ArrowEscapeGameView).GetField("currentPuzzle", InstancePrivate);
            Assert.IsNotNull(field, "currentPuzzle field should be accessible for tests.");
            return (ArrowEscapePuzzleData)field.GetValue(runtime);
        }

        private static int GetIntField(ArrowEscapeGameView runtime, string fieldName)
        {
            var field = typeof(ArrowEscapeGameView).GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, fieldName + " field should be accessible for tests.");
            return (int)field.GetValue(runtime);
        }

        private static bool GetBoolField(ArrowEscapeGameView runtime, string fieldName)
        {
            var field = typeof(ArrowEscapeGameView).GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, fieldName + " field should be accessible for tests.");
            return (bool)field.GetValue(runtime);
        }

        private static bool[,] GetActiveTiles(ArrowEscapeGameView runtime)
        {
            var field = typeof(ArrowEscapeGameView).GetField("activeTiles", InstancePrivate);
            Assert.IsNotNull(field, "activeTiles field should be accessible for tests.");
            return (bool[,])field.GetValue(runtime);
        }

        private static object GetPieceAtCell(ArrowEscapeGameView runtime, Vector2Int cell)
        {
            var field = typeof(ArrowEscapeGameView).GetField("pieceByCell", InstancePrivate);
            Assert.IsNotNull(field, "pieceByCell field should be accessible for tests.");
            var pieces = (Array)field.GetValue(runtime);
            return pieces.GetValue(cell.x, cell.y);
        }

        private static object FindEscapablePieceWithBody(ArrowEscapeGameView runtime)
        {
            var piecesField = typeof(ArrowEscapeGameView).GetField("arrowPieces", InstancePrivate);
            Assert.IsNotNull(piecesField, "Arrow pieces field should be accessible.");
            var canPieceEscapeMethod = typeof(ArrowEscapeGameView).GetMethod("CanPieceEscape", InstancePrivate);
            Assert.IsNotNull(canPieceEscapeMethod, "CanPieceEscape method should be accessible.");
            var pieces = (IEnumerable)piecesField.GetValue(runtime);
            foreach (var piece in pieces)
            {
                var cells = GetPieceCells(piece);
                if (cells.Length <= 1)
                {
                    continue;
                }

                if ((bool)canPieceEscapeMethod.Invoke(runtime, new object[] { piece }))
                {
                    return piece;
                }
            }

            return null;
        }

        private static Vector2Int GetPieceHead(object piece)
        {
            var headField = piece.GetType().GetField("Head", InstancePrivate);
            Assert.IsNotNull(headField, "Arrow piece head field should be accessible.");
            return (Vector2Int)headField.GetValue(piece);
        }

        private static Vector2Int[] GetPieceCells(object piece)
        {
            var cellsField = piece.GetType().GetField("Cells", InstancePrivate);
            Assert.IsNotNull(cellsField, "Arrow piece cells field should be accessible.");
            var cellsArray = (Array)cellsField.GetValue(piece);
            var cells = new Vector2Int[cellsArray.Length];
            for (var i = 0; i < cellsArray.Length; i++)
            {
                cells[i] = (Vector2Int)cellsArray.GetValue(i);
            }

            return cells;
        }

        private static Vector2[] GetPieceVisualPoints(object piece)
        {
            var visualField = piece.GetType().GetField("Visual", InstancePrivate);
            Assert.IsNotNull(visualField, "Arrow piece visual field should be accessible.");
            var visual = visualField.GetValue(piece);
            Assert.IsNotNull(visual, "Arrow piece should own one continuous path visual.");
            var pointsField = visual.GetType().GetField("points", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(pointsField, "Arrow piece visual points should be accessible.");
            var rawPoints = (Vector2[])pointsField.GetValue(visual);
            var positions = new Vector2[rawPoints.Length];
            Array.Copy(rawPoints, positions, rawPoints.Length);

            return positions;
        }

        private static void AssertSnakeBodyStaysConnected(Vector2[] startPositions, Vector2[] currentPositions)
        {
            Assert.AreEqual(startPositions.Length, currentPositions.Length, "Sampled body positions should stay aligned.");
            for (var i = 1; i < currentPositions.Length; i++)
            {
                var expectedDistance = Vector2.Distance(startPositions[i - 1], startPositions[i]);
                var currentDistance = Vector2.Distance(currentPositions[i - 1], currentPositions[i]);
                Assert.LessOrEqual(currentDistance, expectedDistance + 4f, "Flying arrow body should not split into delayed chunks.");
                Assert.GreaterOrEqual(currentDistance, expectedDistance * 0.45f, "Flying arrow body should not collapse while following the head.");
            }
        }

        private static void AssertRenderedArrowsUseContinuousMazeSegments(ArrowEscapeGameView runtime, int expectedCellCount)
        {
            var field = typeof(ArrowEscapeGameView).GetField("arrowPieces", InstancePrivate);
            Assert.IsNotNull(field, "arrowPieces field should be accessible for tests.");
            var pieces = (IEnumerable)field.GetValue(runtime);
            var pieceCount = 0;
            var coveredCellCount = 0;
            var multiCellPieceCount = 0;
            foreach (var piece in pieces)
            {
                pieceCount++;
                var cellsField = piece.GetType().GetField("Cells", InstancePrivate);
                Assert.IsNotNull(cellsField, "Arrow piece cells field should be accessible.");
                var cells = (Array)cellsField.GetValue(piece);
                coveredCellCount += cells.Length;
                if (cells.Length > 1)
                {
                    multiCellPieceCount++;
                }

                AssertSnakePieceUsesOrderedPath(piece, cells);
                AssertRenderedPiecePathIsOrthogonal(piece);
            }

            Assert.AreEqual(expectedCellCount, coveredCellCount, "Rendered arrow segments should cover every active cell.");
            Assert.Greater(multiCellPieceCount, pieceCount / 3, "ArrowEscape should render as continuous maze-like arrow segments.");
            Assert.AreEqual(pieceCount, CountObjectsWithPrefix("ArrowEscapePiece_"), "Each arrow should render as one continuous path object.");
            Assert.AreEqual(0, CountObjectsWithPrefix("MazeTrack"), "ArrowEscape should not render arrows from per-tile track fragments.");
        }

        private static void AssertInitialPlayableSurfaceIsLimited(ArrowEscapeGameView runtime, int activeCellCount)
        {
            var piecesField = typeof(ArrowEscapeGameView).GetField("arrowPieces", InstancePrivate);
            Assert.IsNotNull(piecesField, "arrowPieces field should be accessible.");
            var canEscapeMethod = typeof(ArrowEscapeGameView).GetMethod("CanPieceEscape", InstancePrivate);
            Assert.IsNotNull(canEscapeMethod, "CanPieceEscape method should be accessible.");
            var pieces = (IEnumerable)piecesField.GetValue(runtime);
            var pieceCount = 0;
            var playablePieceCount = 0;
            var playableCellCount = 0;
            foreach (var piece in pieces)
            {
                pieceCount++;
                if (!(bool)canEscapeMethod.Invoke(runtime, new[] { piece }))
                {
                    continue;
                }

                playablePieceCount++;
                playableCellCount += GetPieceCells(piece).Length;
            }

            Assert.Greater(pieceCount, 0, "Hard ArrowEscape level should contain arrow pieces.");
            Assert.LessOrEqual(playablePieceCount, Mathf.Max(16, pieceCount / 8), "Hard ArrowEscape should not expose too many immediately playable arrows.");
            Assert.LessOrEqual(playableCellCount, activeCellCount / 2, "Hard ArrowEscape should not expose too much immediately removable body.");
        }

        private static void AssertZoomSliderScalesBoard()
        {
            var slider = GameObject.Find("ArrowEscapeZoomSlider")?.GetComponent<Slider>();
            Assert.IsNotNull(slider, "ArrowEscape zoom control should use a slider.");
            var board = GameObject.Find("ArrowEscapeBoard")?.GetComponent<RectTransform>();
            Assert.IsNotNull(board, "ArrowEscape board should exist for zoom validation.");
            var initialScale = board.localScale.x;
            slider.value = 1f;
            Canvas.ForceUpdateCanvases();
            Assert.Greater(board.localScale.x, initialScale + 0.1f, "Zoom slider should enlarge the board.");
            slider.value = 0f;
            Canvas.ForceUpdateCanvases();
        }

        private static void AssertSnakePieceUsesOrderedPath(object piece, Array cells)
        {
            var headField = piece.GetType().GetField("Head", InstancePrivate);
            Assert.IsNotNull(headField, "Arrow piece head field should be accessible.");
            var directionField = piece.GetType().GetField("Direction", InstancePrivate);
            Assert.IsNotNull(directionField, "Arrow piece direction field should be accessible.");
            var head = (Vector2Int)headField.GetValue(piece);
            Assert.Greater(cells.Length, 0, "Arrow piece should contain at least the head cell.");
            Assert.AreEqual(head, (Vector2Int)cells.GetValue(0), "Arrow piece cells should be ordered from head to tail.");

            for (var i = 1; i < cells.Length; i++)
            {
                var previous = (Vector2Int)cells.GetValue(i - 1);
                var current = (Vector2Int)cells.GetValue(i);
                var distance = Mathf.Abs(previous.x - current.x) + Mathf.Abs(previous.y - current.y);
                Assert.AreEqual(1, distance, "Arrow piece cells should form a continuous orthogonal path.");
                for (var j = 0; j < i; j++)
                {
                    Assert.AreNotEqual((Vector2Int)cells.GetValue(j), current, "Snake body should not reuse a cell.");
                }
            }

            if (cells.Length > 1)
            {
                var direction = (int)directionField.GetValue(piece);
                var firstBody = (Vector2Int)cells.GetValue(1);
                Assert.AreNotEqual(head + DirectionDelta(direction), firstBody, "Snake body should start behind or beside the head, not in front of the arrow.");
            }
        }

        private static void AssertRenderedPiecePathIsOrthogonal(object piece)
        {
            var visualField = piece.GetType().GetField("Visual", InstancePrivate);
            Assert.IsNotNull(visualField, "Arrow piece visual field should be accessible.");
            var visual = visualField.GetValue(piece);
            Assert.IsNotNull(visual, "Arrow piece visual should exist.");
            var renderPointsField = visual.GetType().GetField("renderPoints", InstancePrivate);
            Assert.IsNotNull(renderPointsField, "Arrow piece render points field should be accessible.");
            var renderPoints = (Vector2[])renderPointsField.GetValue(visual);
            for (var i = 1; i < renderPoints.Length; i++)
            {
                var previous = renderPoints[i - 1];
                var current = renderPoints[i];
                if (float.IsNaN(previous.x) || float.IsNaN(previous.y) || float.IsNaN(current.x) || float.IsNaN(current.y))
                {
                    continue;
                }

                Assert.IsTrue(
                    Mathf.Approximately(previous.x, current.x) || Mathf.Approximately(previous.y, current.y),
                    "Rendered ArrowEscape paths should never draw diagonal segments.");
            }
        }

        private static Vector2Int DirectionDelta(int direction)
        {
            switch (direction)
            {
                case 0:
                    return new Vector2Int(0, -1);
                case 1:
                    return new Vector2Int(1, 0);
                case 2:
                    return new Vector2Int(0, 1);
                default:
                    return new Vector2Int(-1, 0);
            }
        }

        private static void ClickButton(string buttonName)
        {
            var button = FindButton(buttonName);
            Assert.IsNotNull(button, "Could not find button: " + buttonName);
            Assert.IsTrue(button.interactable, "Button should be interactable before click: " + buttonName);
            button.onClick.Invoke();
        }

        private static bool TryClickButton(string buttonName)
        {
            var button = FindButton(buttonName);
            if (button == null || !button.interactable)
            {
                return false;
            }

            button.onClick.Invoke();
            return true;
        }

        private static void AssertButtonExists(string buttonName)
        {
            Assert.IsNotNull(FindButton(buttonName), "Missing button: " + buttonName);
        }

        private static Button FindButton(string buttonName)
        {
            var buttons = Object.FindObjectsOfType<Button>();
            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name == buttonName)
                {
                    return buttons[i];
                }
            }

            return null;
        }

        private static int CountObjectsWithPrefix(string prefix)
        {
            var count = 0;
            var transforms = Object.FindObjectsOfType<Transform>();
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertRectSizeAtLeast(string name, float width, float height)
        {
            var rect = GameObject.Find(name)?.GetComponent<RectTransform>();
            Assert.IsNotNull(rect, "Missing rect: " + name);
            Assert.GreaterOrEqual(rect.rect.width, width, name + " width should be large enough.");
            Assert.GreaterOrEqual(rect.rect.height, height, name + " height should be large enough.");
        }

        private static void AssertChildStaysInside(string parentName, string childName)
        {
            var parent = GameObject.Find(parentName)?.GetComponent<RectTransform>();
            var child = GameObject.Find(childName)?.GetComponent<RectTransform>();
            Assert.IsNotNull(parent, "Missing parent rect: " + parentName);
            Assert.IsNotNull(child, "Missing child rect: " + childName);
            var parentRect = ToScreenRect(parent);
            var childRect = ToScreenRect(child);
            Assert.GreaterOrEqual(childRect.xMin, parentRect.xMin - 1f, childName + " should stay inside parent horizontally.");
            Assert.LessOrEqual(childRect.xMax, parentRect.xMax + 1f, childName + " should stay inside parent horizontally.");
            Assert.GreaterOrEqual(childRect.yMin, parentRect.yMin - 1f, childName + " should stay inside parent vertically.");
            Assert.LessOrEqual(childRect.yMax, parentRect.yMax + 1f, childName + " should stay inside parent vertically.");
        }

        private static Rect ToScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static bool[,] Copy(bool[,] source)
        {
            var result = new bool[source.GetLength(0), source.GetLength(1)];
            for (var y = 0; y < source.GetLength(1); y++)
            {
                for (var x = 0; x < source.GetLength(0); x++)
                {
                    result[x, y] = source[x, y];
                }
            }

            return result;
        }

        private static Vector2Int[] FindPuzzlePieceByHead(ArrowEscapePuzzleData puzzle, Vector2Int head)
        {
            for (var i = 0; i < puzzle.Pieces.Length; i++)
            {
                var cells = puzzle.Pieces[i];
                if (cells.Length > 0 && cells[0] == head)
                {
                    return cells;
                }
            }

            Assert.Fail("Generated puzzle should contain a piece headed at " + head + ".");
            return Array.Empty<Vector2Int>();
        }

        private static void AssertPuzzlePiecesCoverActiveLayout(ArrowEscapePuzzleData puzzle)
        {
            var covered = new bool[puzzle.Layout.GetLength(0), puzzle.Layout.GetLength(1)];
            var coveredCount = 0;
            for (var i = 0; i < puzzle.Pieces.Length; i++)
            {
                var cells = puzzle.Pieces[i];
                Assert.Greater(cells.Length, 0, "Generated arrow piece should contain at least one cell.");
                for (var c = 0; c < cells.Length; c++)
                {
                    var cell = cells[c];
                    Assert.IsTrue(puzzle.Layout[cell.x, cell.y], "Generated arrow piece should stay inside active layout.");
                    Assert.IsFalse(covered[cell.x, cell.y], "Generated arrow pieces should not overlap.");
                    covered[cell.x, cell.y] = true;
                    coveredCount++;
                }
            }

            Assert.AreEqual(CountActive(puzzle.Layout), coveredCount, "Generated arrow pieces should cover every active cell.");
        }

        private static void AssertNoAdjacentSameDirectionPieces(ArrowEscapePuzzleData puzzle)
        {
            var pieceByCell = BuildPieceIndexByCell(puzzle);
            for (var i = 0; i < puzzle.Pieces.Length; i++)
            {
                var cells = puzzle.Pieces[i];
                Assert.Greater(cells.Length, 0, "Puzzle piece should contain at least one cell.");
                var head = cells[0];
                var direction = puzzle.Directions[head.x, head.y];
                var frontPieceIndex = FindFrontPieceIndexInRay(head, direction, pieceByCell);
                if (frontPieceIndex < 0 || frontPieceIndex == i)
                {
                    continue;
                }

                if (!CanConnectSameDirectionPieces(puzzle.Pieces[frontPieceIndex], cells, direction))
                {
                    continue;
                }

                var frontHead = puzzle.Pieces[frontPieceIndex][0];
                Assert.AreNotEqual(
                    direction,
                    puzzle.Directions[frontHead.x, frontHead.y],
                    "Puzzle should merge a same-direction arrow when one head connects to another tail.");
            }
        }

        private static bool CanConnectSameDirectionPieces(Vector2Int[] frontCells, Vector2Int[] backCells, int direction)
        {
            return frontCells != null
                && backCells != null
                && frontCells.Length > 0
                && backCells.Length > 0
                && frontCells[frontCells.Length - 1] == backCells[0] + DirectionDelta(direction);
        }

        private static int FindFrontPieceIndexInRay(Vector2Int head, int direction, int[,] pieceByCell)
        {
            var delta = DirectionDelta(direction);
            var x = head.x + delta.x;
            var y = head.y + delta.y;
            while (x >= 0 && y >= 0 && x < pieceByCell.GetLength(0) && y < pieceByCell.GetLength(1))
            {
                var pieceIndex = pieceByCell[x, y];
                if (pieceIndex >= 0)
                {
                    return pieceIndex;
                }

                x += delta.x;
                y += delta.y;
            }

            return -1;
        }

        private static int[,] BuildPieceIndexByCell(ArrowEscapePuzzleData puzzle)
        {
            var pieceByCell = new int[puzzle.Layout.GetLength(0), puzzle.Layout.GetLength(1)];
            for (var y = 0; y < pieceByCell.GetLength(1); y++)
            {
                for (var x = 0; x < pieceByCell.GetLength(0); x++)
                {
                    pieceByCell[x, y] = -1;
                }
            }

            for (var i = 0; i < puzzle.Pieces.Length; i++)
            {
                var cells = puzzle.Pieces[i];
                for (var c = 0; c < cells.Length; c++)
                {
                    var cell = cells[c];
                    pieceByCell[cell.x, cell.y] = i;
                }
            }

            return pieceByCell;
        }

        private static string BuildPuzzleSignature(ArrowEscapePuzzleData puzzle)
        {
            var builder = new StringBuilder();
            for (var y = 0; y < puzzle.Layout.GetLength(1); y++)
            {
                for (var x = 0; x < puzzle.Layout.GetLength(0); x++)
                {
                    builder.Append(puzzle.Layout[x, y] ? '1' : '0');
                    builder.Append(puzzle.Directions[x, y]);
                    builder.Append(',');
                }
            }

            builder.Append('|');
            for (var i = 0; i < puzzle.Pieces.Length; i++)
            {
                var cells = puzzle.Pieces[i];
                for (var c = 0; c < cells.Length; c++)
                {
                    builder.Append(cells[c].x);
                    builder.Append(':');
                    builder.Append(cells[c].y);
                    builder.Append(';');
                }

                builder.Append('|');
            }

            return builder.ToString();
        }

        private static int CountActive(bool[,] values)
        {
            var count = 0;
            for (var y = 0; y < values.GetLength(1); y++)
            {
                for (var x = 0; x < values.GetLength(0); x++)
                {
                    if (values[x, y])
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.DeleteKey(MiniGameRuntimeSettings.PlayerPrefsKey);
            PlayerPrefs.Save();
        }
    }
}
