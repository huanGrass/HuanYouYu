using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests
{
    public class SudokuGamePlayModeTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Color FillModeActiveColor = new Color32(238, 196, 110, 255);
        private static readonly Color ModeTabInactiveColor = new Color32(242, 233, 214, 255);
        private static readonly Color HintDigitTextColor = new Color32(38, 143, 116, 255);
        private const float ExpectedTopHostHeight = 224f;
        private const float ExpectedBottomHostHeight = 324f;

        private GameObject rootObject;
        private HuanYouYu.MiniGameHall.GameSudokuView gameView;
        private HuanYouYu.MiniGameHall.MiniGameSettlement completedSettlement;

        [SetUp]
        public void SetUp()
        {
            PlayModeGlobalLogMonitor.Clear();
            EnsureEventSystem();

            rootObject = new GameObject(
                "SudokuTestRoot",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(TestRuntimeHost));

            var canvas = rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = rootObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(750f, 1334f);

            rootObject.AddComponent<HuanYouYu.MiniGameHall.MiniGameSfxPlayer>();
            completedSettlement = null;

            gameView = new HuanYouYu.MiniGameHall.GameSudokuView(
                rootObject.GetComponent<TestRuntimeHost>(),
                rootObject.transform,
                delegate(HuanYouYu.MiniGameHall.MiniGameSettlement settlement) { completedSettlement = settlement; },
                delegate { });
        }

        [TearDown]
        public void TearDown()
        {
            if (gameView != null)
            {
                gameView.Dispose();
                gameView = null;
            }

            if (rootObject != null)
            {
                Object.DestroyImmediate(rootObject);
                rootObject = null;
            }

            var eventSystem = Object.FindObjectOfType<EventSystem>();
            if (eventSystem != null)
            {
                Object.DestroyImmediate(eventSystem.gameObject);
            }
        }

        [Test]
        public void RuntimeBuildsBoardAndKeypad()
        {
            Assert.IsNotNull(Find("GameSudokuView"));
            Assert.IsNotNull(Find("SudokuTop"));
            Assert.IsNotNull(Find("SudokuBottom"));
            Assert.IsNotNull(Find("BoardPanel"));
            Assert.IsNotNull(Find("SudokuKeypad"));
            Assert.IsNotNull(Find("NumberButton_1"));
            Assert.IsNotNull(Find("RestartButton"));
            Assert.IsNotNull(Find("HintButton"));
            Assert.IsNotNull(Find("ClearButton"));
            Assert.IsNotNull(Find("FillModeButton"));
            Assert.IsNotNull(Find("NotesButton"));
            Assert.IsNotNull(Find("AutoCandidatesButton"));
            Assert.IsNotNull(Find("ResetRoundButton"));
            Assert.IsNotNull(Find("DifficultyBar"));
            Assert.IsNotNull(Find("EasyDifficultyButton"));
            Assert.IsNotNull(Find("NormalDifficultyButton"));
            Assert.IsNotNull(Find("HardDifficultyButton"));
            Assert.IsTrue(Find("HintButton").activeInHierarchy);
            Assert.IsEmpty(PlayModeGlobalLogMonitor.BuildFailureReport());
        }

        [Test]
        public void KeypadUsesSinglePanelWithoutLegacyDecorations()
        {
            var keypad = Find("SudokuKeypad");
            Assert.IsNotNull(keypad);
            Assert.IsNotNull(keypad.transform.Find("Panel"));
            Assert.IsNull(keypad.transform.Find("Panel/Inset"));
            Assert.IsNull(Find("NumberButtonShadow_1"));
            Assert.IsNull(Find("NumberButtonOutline_1"));
            Assert.IsNull(Find("LeftCard"));
        }

        [Test]
        public void ModeTabsDefaultToFillMode()
        {
            var fillModeButton = Find("FillModeButton");
            var notesButton = Find("NotesButton");
            Assert.IsNotNull(fillModeButton);
            Assert.IsNotNull(notesButton);
            Assert.AreEqual(FillModeActiveColor, fillModeButton.GetComponent<HuanYouYu.MiniGameHall.RoundedRectGraphic>().color);
            Assert.AreEqual(ModeTabInactiveColor, notesButton.GetComponent<HuanYouYu.MiniGameHall.RoundedRectGraphic>().color);
        }

        [Test]
        public void HeaderShowsDifficultyTimeAndProgress()
        {
            var summary = GetComponentText(Find("Score"));
            StringAssert.Contains(HuanYouYu.MiniGameHall.UiTextCatalog.GetOrFallback("sudoku.hud.difficulty.normal", "Normal"), summary);
            StringAssert.Contains("00:00", summary);
            StringAssert.Contains("-", summary);
        }

        [Test]
        public void DifficultyButtonsSwitchDifficultyAndStartNewPuzzle()
        {
            var originalSignature = BuildBoardSignature();
            var editableCell = FindFirstEmptyCell();
            Assert.IsNotNull(editableCell);

            Click(editableCell.name);
            Click("NumberButton_5");
            var editedSignature = BuildBoardSignature();
            Assert.AreNotEqual(originalSignature, editedSignature);

            gameView.Tick(2.2f);
            Click("EasyDifficultyButton");

            var summary = GetComponentText(Find("Score"));
            StringAssert.Contains(HuanYouYu.MiniGameHall.UiTextCatalog.GetOrFallback("sudoku.hud.difficulty.easy", "Easy"), summary);
            StringAssert.Contains("00:00", summary);
            Assert.AreNotEqual(editedSignature, BuildBoardSignature());

            Click("HardDifficultyButton");
            summary = GetComponentText(Find("Score"));
            StringAssert.Contains(HuanYouYu.MiniGameHall.UiTextCatalog.GetOrFallback("sudoku.hud.difficulty.hard", "Hard"), summary);
        }

        [Test]
        public void GivenCellsCannotBeEdited()
        {
            var givenCell = FindFirstFilledCell();
            Assert.IsNotNull(givenCell);

            var originalText = GetCellLabelText(givenCell);
            Click(givenCell.name);
            Click("NumberButton_9");

            Assert.AreEqual(originalText, GetCellLabelText(givenCell));
        }

        [Test]
        public void GivenCellsCannotReceiveCandidates()
        {
            var givenCell = FindFirstFilledCell();
            Assert.IsNotNull(givenCell);

            Click(givenCell.name);
            Click("NotesButton");
            Click("NumberButton_3");

            Assert.IsEmpty(GetVisibleCandidateDigits(givenCell));
            Assert.IsFalse(string.IsNullOrEmpty(GetCellLabelText(givenCell)));
        }

        [Test]
        public void EditableCellCanFillAndClear()
        {
            var editableCell = FindFirstEmptyCell();
            Assert.IsNotNull(editableCell);

            Click(editableCell.name);
            Click("NumberButton_5");
            Assert.AreEqual("5", GetCellLabelText(editableCell));

            Click("ClearButton");
            Assert.AreEqual(string.Empty, GetCellLabelText(editableCell));
        }

        [Test]
        public void NotesModeCanAddAndToggleCandidate()
        {
            var editableCell = FindFirstEmptyCell();
            Assert.IsNotNull(editableCell);

            Click(editableCell.name);
            Click("NotesButton");
            Click("NumberButton_5");
            CollectionAssert.AreEqual(new[] { "5" }, GetVisibleCandidateDigits(editableCell));
            Assert.AreEqual(string.Empty, GetCellLabelText(editableCell));

            Click("NumberButton_5");
            Assert.IsEmpty(GetVisibleCandidateDigits(editableCell));
        }

        [Test]
        public void NotesModeCanShowMultipleCandidates()
        {
            var editableCell = FindFirstEmptyCell();
            Assert.IsNotNull(editableCell);

            Click(editableCell.name);
            Click("NotesButton");
            Click("NumberButton_1");
            Click("NumberButton_5");
            Click("NumberButton_9");

            CollectionAssert.AreEqual(new[] { "1", "5", "9" }, GetVisibleCandidateDigits(editableCell));
            Assert.AreEqual(string.Empty, GetCellLabelText(editableCell));
        }

        [Test]
        public void NotesModeClearRemovesAllCandidates()
        {
            var editableCell = FindFirstEmptyCell();
            Assert.IsNotNull(editableCell);

            Click(editableCell.name);
            Click("NotesButton");
            Click("NumberButton_2");
            Click("NumberButton_6");
            Assert.IsNotEmpty(GetVisibleCandidateDigits(editableCell));

            Click("ClearButton");
            Assert.IsEmpty(GetVisibleCandidateDigits(editableCell));
        }

        [Test]
        public void FillingValueClearsCandidates()
        {
            var editableCell = FindFirstEmptyCell();
            Assert.IsNotNull(editableCell);

            Click(editableCell.name);
            Click("NotesButton");
            Click("NumberButton_3");
            Click("NumberButton_7");
            CollectionAssert.AreEqual(new[] { "3", "7" }, GetVisibleCandidateDigits(editableCell));

            Click("FillModeButton");
            Click("NumberButton_4");

            Assert.AreEqual("4", GetCellLabelText(editableCell));
            Assert.IsEmpty(GetVisibleCandidateDigits(editableCell));
        }

        [Test]
        public void BottomActionAreaShowsHintClearAndNotesButtons()
        {
            Assert.IsNotNull(Find("ActionBar"));
            Assert.IsNotNull(Find("HintButton"));
            Assert.IsNotNull(Find("ClearButton"));
            Assert.IsNotNull(Find("FillModeButton"));
            Assert.IsNotNull(Find("NotesButton"));
            Assert.IsNotNull(Find("AutoCandidatesButton"));
            Assert.IsNotNull(Find("ResetRoundButton"));
            Assert.IsTrue(Find("HintButton").activeInHierarchy);
            Assert.IsTrue(Find("ClearButton").activeInHierarchy);
            Assert.IsTrue(Find("NotesButton").activeInHierarchy);
            var bottomHost = Find("BottomHost").GetComponent<RectTransform>();
            Assert.GreaterOrEqual(bottomHost.rect.height, 250f);
        }

        [Test]
        public void AutoCandidatesBuildsValidCandidatesForEmptyCell()
        {
            var editableCell = FindFirstEmptyCell();
            Assert.IsNotNull(editableCell);

            Click("AutoCandidatesButton");

            var cellIndex = ParseCellIndex(editableCell.name);
            CollectionAssert.AreEqual(GetAllowedDigits(cellIndex), GetVisibleCandidateDigits(editableCell));
        }

        [Test]
        public void AutoCandidatesOverwritesManualCandidates()
        {
            var editableCell = FindFirstEmptyCell();
            Assert.IsNotNull(editableCell);

            Click(editableCell.name);
            Click("NotesButton");
            Click("NumberButton_1");
            Click("NumberButton_9");
            CollectionAssert.AreEqual(new[] { "1", "9" }, GetVisibleCandidateDigits(editableCell));

            Click("AutoCandidatesButton");

            var cellIndex = ParseCellIndex(editableCell.name);
            CollectionAssert.AreEqual(GetAllowedDigits(cellIndex), GetVisibleCandidateDigits(editableCell));
        }

        [Test]
        public void AutoCandidatesClearsCandidatesOnFilledEditableCell()
        {
            var editableCell = FindFirstEmptyCell();
            Assert.IsNotNull(editableCell);

            Click("AutoCandidatesButton");
            Assert.IsNotEmpty(GetVisibleCandidateDigits(editableCell));

            Click(editableCell.name);
            Click("NumberButton_5");
            Assert.AreEqual("5", GetCellLabelText(editableCell));

            Click("AutoCandidatesButton");
            Assert.IsEmpty(GetVisibleCandidateDigits(editableCell));
        }

        [Test]
        public void AutoCandidatesRebuildsAfterBoardChanges()
        {
            var board = ReadBoardFromUi();
            var pair = FindCandidateRemovalScenario(board);
            Assert.GreaterOrEqual(pair.SourceIndex, 0);

            var sourceCell = FindCell(pair.SourceIndex);
            var targetCell = FindCell(pair.TargetIndex);
            Click("AutoCandidatesButton");
            CollectionAssert.Contains(GetVisibleCandidateDigits(targetCell), pair.Digit.ToString());

            Click(sourceCell.name);
            Click("NumberButton_" + pair.Digit);
            Assert.AreEqual(pair.Digit.ToString(), GetCellLabelText(sourceCell));

            Click("AutoCandidatesButton");
            CollectionAssert.DoesNotContain(GetVisibleCandidateDigits(targetCell), pair.Digit.ToString());
        }

        [Test]
        public void FillingValueRemovesRelatedCandidates()
        {
            var board = ReadBoardFromUi();
            var pair = FindCandidateRemovalScenario(board);
            Assert.GreaterOrEqual(pair.SourceIndex, 0);

            var sourceCell = FindCell(pair.SourceIndex);
            var targetCell = FindCell(pair.TargetIndex);

            Click("AutoCandidatesButton");
            CollectionAssert.Contains(GetVisibleCandidateDigits(targetCell), pair.Digit.ToString());

            Click(sourceCell.name);
            Click("NumberButton_" + pair.Digit);

            Assert.AreEqual(pair.Digit.ToString(), GetCellLabelText(sourceCell));
            CollectionAssert.DoesNotContain(GetVisibleCandidateDigits(targetCell), pair.Digit.ToString());
        }

        [UnityTest]
        public IEnumerator HintRevealsBestCandidateCellWithDistinctColorAndLocksIt()
        {
            var board = ReadBoardFromUi();
            var solution = (int[])board.Clone();
            Assert.IsTrue(SolveBoard(solution));
            var expectedHintIndex = FindBestEmptyCell(board);
            Assert.GreaterOrEqual(expectedHintIndex, 0);

            Click("HintButton");
            yield return null;
            Assert.IsNotNull(Find("SudokuHintFlyDigit"));

            yield return new WaitForSeconds(0.55f);

            var afterHint = ReadBoardFromUi();
            var changedIndex = FindSingleChangedCell(board, afterHint);
            Assert.AreEqual(expectedHintIndex, changedIndex);
            Assert.AreEqual(solution[changedIndex], afterHint[changedIndex]);

            var hintCell = FindCell(changedIndex);
            Assert.AreEqual(HintDigitTextColor, GetComponentColor(hintCell.transform.Find("Value").gameObject));

            Click(hintCell.name);
            Click("ClearButton");
            Assert.AreEqual(solution[changedIndex].ToString(), GetCellLabelText(hintCell));

            var replacement = solution[changedIndex] == 9 ? 1 : solution[changedIndex] + 1;
            Click("NumberButton_" + replacement);
            Assert.AreEqual(solution[changedIndex].ToString(), GetCellLabelText(hintCell));
        }

        [Test]
        public void ShellLayoutUsesSudokuSpecificTopAndBottomInsets()
        {
            var topHost = Find("TopHost").GetComponent<RectTransform>();
            var bottomHost = Find("BottomHost").GetComponent<RectTransform>();
            var contentHost = Find("ContentHost").GetComponent<RectTransform>();

            Assert.That(topHost.rect.height, Is.EqualTo(ExpectedTopHostHeight).Within(0.1f));
            Assert.That(bottomHost.rect.height, Is.EqualTo(ExpectedBottomHostHeight).Within(0.1f));
            Assert.That(contentHost.offsetMin.y, Is.EqualTo(ExpectedBottomHostHeight).Within(0.1f));
            Assert.That(contentHost.offsetMax.y, Is.EqualTo(-ExpectedTopHostHeight).Within(0.1f));
            Assert.IsNotNull(Find("BoardPanel"));
            Assert.IsNotNull(Find("SudokuBottom"));
        }

        [Test]
        public void DuplicateInputMarksConflictAndCanBeFixed()
        {
            var board = ReadBoardFromUi();
            var pair = FindConflictCandidate(board);
            Assert.GreaterOrEqual(pair.GivenIndex, 0);

            var editableCell = FindCell(pair.EditableIndex);
            Click(editableCell.name);
            Click("NumberButton_" + board[pair.GivenIndex]);

            var conflictColor = editableCell.GetComponent<HuanYouYu.MiniGameHall.RoundedRectGraphic>().color;
            Assert.Greater(conflictColor.r, conflictColor.g);

            Click("ClearButton");
            var clearedColor = editableCell.GetComponent<HuanYouYu.MiniGameHall.RoundedRectGraphic>().color;
            Assert.AreEqual(string.Empty, GetCellLabelText(editableCell));
            Assert.Greater(clearedColor.g, conflictColor.g);
        }

        [Test]
        public void RoundedRectGraphicBuildsRoundedCornersForAllQuadrants()
        {
            var host = new GameObject("RoundedRectHost", typeof(RectTransform));
            try
            {
                var rect = host.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(120f, 80f);

                var graphic = host.AddComponent<HuanYouYu.MiniGameHall.RoundedRectGraphic>();
                graphic.CornerRadius = 20f;

                var vertexHelper = new VertexHelper();
                var populateMesh = typeof(HuanYouYu.MiniGameHall.RoundedRectGraphic).GetMethod(
                    "OnPopulateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(VertexHelper) },
                    null);
                Assert.IsNotNull(populateMesh);
                populateMesh.Invoke(graphic, new object[] { vertexHelper });

                var mesh = new Mesh();
                vertexHelper.FillMesh(mesh);
                var vertices = new List<Vector3>();
                mesh.GetVertices(vertices);

                Assert.IsFalse(HasVertexNear(vertices, new Vector2(-60f, 40f)));
                Assert.IsFalse(HasVertexNear(vertices, new Vector2(60f, 40f)));
                Assert.IsFalse(HasVertexNear(vertices, new Vector2(-60f, -40f)));
                Assert.IsFalse(HasVertexNear(vertices, new Vector2(60f, -40f)));

                Assert.IsTrue(HasCornerArcVertex(vertices, true, true));
                Assert.IsTrue(HasCornerArcVertex(vertices, false, true));
                Assert.IsTrue(HasCornerArcVertex(vertices, true, false));
                Assert.IsTrue(HasCornerArcVertex(vertices, false, false));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator SolvingPuzzleShowsSettlementPopup()
        {
            var puzzle = ReadBoardFromUi();
            Assert.IsTrue(SolveBoard(puzzle));

            for (var i = 0; i < puzzle.Length; i++)
            {
                var cell = FindCell(i);
                if (!string.IsNullOrEmpty(GetCellLabelText(cell)))
                {
                    continue;
                }

                Click(cell.name);
                Click("NumberButton_" + puzzle[i]);
                yield return null;
            }

            yield return null;

            var popup = Find("SudokuSettlementPanel");
            Assert.IsNotNull(popup);

            var backHallButton = popup.transform.Find("Dialog/BackHallButton")?.GetComponent<Button>();
            Assert.IsNotNull(backHallButton, "Settlement back hall button should exist.");
            backHallButton.onClick.Invoke();
            yield return null;

            Assert.IsNotNull(completedSettlement, "Solved settlement should be reported.");
            Assert.AreEqual(60, completedSettlement.CoinCount);
            Assert.AreEqual(1, completedSettlement.ChestCount);
            StringAssert.Contains("60 金币和 1 个宝箱", completedSettlement.Summary);
        }

        [UnityTest]
        public IEnumerator ExitingBeforeCompletionAwardsManualFilledCellsOnly()
        {
            var editableCell = FindFirstEmptyCell();
            Assert.IsNotNull(editableCell);

            Click(editableCell.name);
            Click("NumberButton_5");
            yield return null;

            InvokePrivate("ConfirmExitToHall");
            yield return null;

            var popup = Find("SudokuSettlementPanel");
            Assert.IsNotNull(popup, "Exit settlement popup should appear.");
            var confirmButton = popup.transform.Find("Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(confirmButton, "Exit settlement confirm button should exist.");
            confirmButton.onClick.Invoke();
            yield return null;

            Assert.IsNotNull(completedSettlement, "Exit settlement should be reported.");
            Assert.AreEqual(1, completedSettlement.CoinCount);
            Assert.AreEqual(0, completedSettlement.ChestCount);
            StringAssert.Contains("手动填入 1 格", completedSettlement.Summary);
            StringAssert.Contains("1 金币和 0 个宝箱", completedSettlement.Summary);
        }

        [UnityTest]
        public IEnumerator ExitingWithoutManualEntriesAwardsZeroCoinsAndNoChests()
        {
            InvokePrivate("ConfirmExitToHall");
            yield return null;

            var popup = Find("SudokuSettlementPanel");
            Assert.IsNotNull(popup, "Exit settlement popup should appear.");
            var confirmButton = popup.transform.Find("Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(confirmButton, "Exit settlement confirm button should exist.");
            confirmButton.onClick.Invoke();
            yield return null;

            Assert.IsNotNull(completedSettlement, "Exit settlement should be reported.");
            Assert.AreEqual(0, completedSettlement.CoinCount);
            Assert.AreEqual(0, completedSettlement.ChestCount);
            StringAssert.Contains("手动填入 0 格", completedSettlement.Summary);
            StringAssert.Contains("0 金币和 0 个宝箱", completedSettlement.Summary);
        }

        [UnityTest]
        public IEnumerator RestartResetsTimerAndBoard()
        {
            var originalSignature = BuildBoardSignature();
            var editableCell = FindFirstEmptyCell();
            Assert.IsNotNull(editableCell);
            Click(editableCell.name);
            Click("NumberButton_5");
            var editedSignature = BuildBoardSignature();
            Assert.AreNotEqual(originalSignature, editedSignature);

            gameView.Tick(2.2f);
            yield return null;

            var timerBeforeRestart = GetComponentText(Find("Score"));
            StringAssert.DoesNotContain("00:00", timerBeforeRestart);

            Click("RestartButton");
            yield return null;

            var timerAfterRestart = GetComponentText(Find("Score"));
            StringAssert.Contains("00:00", timerAfterRestart);
            Assert.AreNotEqual(editedSignature, BuildBoardSignature());
        }

        [UnityTest]
        public IEnumerator ResetRoundRestoresCurrentPuzzle()
        {
            var originalSignature = BuildBoardSignature();
            var editableCell = FindFirstEmptyCell();
            Assert.IsNotNull(editableCell);

            Click(editableCell.name);
            Click("NotesButton");
            Click("NumberButton_2");
            Click("FillModeButton");
            Click("NumberButton_5");
            Assert.AreNotEqual(originalSignature, BuildBoardSignature());

            gameView.Tick(2.2f);
            yield return null;

            Click("ResetRoundButton");
            yield return null;

            Assert.AreEqual(originalSignature, BuildBoardSignature());
            Assert.IsEmpty(GetVisibleCandidateDigits(editableCell));
            StringAssert.Contains("00:00", GetComponentText(Find("Score")));
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }
        }

        private static void Click(string objectName)
        {
            var target = Find(objectName);
            Assert.IsNotNull(target, "Missing UI node: " + objectName);
            target.GetComponent<Button>().onClick.Invoke();
        }

        private void InvokePrivate(string methodName)
        {
            var method = typeof(HuanYouYu.MiniGameHall.GameSudokuView).GetMethod(methodName, InstancePrivate);
            Assert.IsNotNull(method, "Missing private method: " + methodName);
            method.Invoke(gameView, null);
        }

        private static GameObject Find(string name)
        {
            var transforms = Object.FindObjectsOfType<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == name)
                {
                    return transforms[i].gameObject;
                }
            }

            return null;
        }

        private static GameObject FindCell(int cellIndex)
        {
            return Find("Cell_" + (cellIndex / 9) + "_" + (cellIndex % 9));
        }

        private static GameObject FindFirstFilledCell()
        {
            for (var i = 0; i < 81; i++)
            {
                var cell = FindCell(i);
                if (cell != null && !string.IsNullOrEmpty(GetCellLabelText(cell)))
                {
                    return cell;
                }
            }

            return null;
        }

        private static GameObject FindFirstEmptyCell()
        {
            for (var i = 0; i < 81; i++)
            {
                var cell = FindCell(i);
                if (cell != null && string.IsNullOrEmpty(GetCellLabelText(cell)))
                {
                    return cell;
                }
            }

            return null;
        }

        private static GameObject FindNextEmptyCell(GameObject excludedCell)
        {
            for (var i = 0; i < 81; i++)
            {
                var cell = FindCell(i);
                if (cell != null && cell != excludedCell && string.IsNullOrEmpty(GetCellLabelText(cell)))
                {
                    return cell;
                }
            }

            return null;
        }

        private static string GetCellLabelText(GameObject cell)
        {
            var label = cell.transform.Find("Value");
            return label == null ? string.Empty : GetComponentText(label.gameObject);
        }

        private static string[] GetVisibleCandidateDigits(GameObject cell)
        {
            var candidatesRoot = cell.transform.Find("Candidates");
            Assert.IsNotNull(candidatesRoot, "Missing Candidates root on: " + cell.name);

            var visibleDigits = new List<string>();
            for (var i = 1; i <= 9; i++)
            {
                var candidate = candidatesRoot.Find("Candidate_" + i);
                Assert.IsNotNull(candidate, "Missing Candidate node: " + i);
                var label = candidate.Find("Label");
                Assert.IsNotNull(label, "Missing Candidate label: " + i);
                if (!label.gameObject.activeInHierarchy)
                {
                    continue;
                }

                visibleDigits.Add(GetComponentText(label.gameObject));
            }

            return visibleDigits.ToArray();
        }

        private static int[] ReadBoardFromUi()
        {
            var board = new int[81];
            for (var i = 0; i < board.Length; i++)
            {
                var text = GetCellLabelText(FindCell(i));
                board[i] = string.IsNullOrEmpty(text) ? 0 : int.Parse(text);
            }

            return board;
        }

        private static (int GivenIndex, int EditableIndex) FindConflictCandidate(int[] board)
        {
            for (var row = 0; row < 9; row++)
            {
                for (var column = 0; column < 9; column++)
                {
                    var givenIndex = row * 9 + column;
                    if (board[givenIndex] == 0)
                    {
                        continue;
                    }

                    for (var otherColumn = 0; otherColumn < 9; otherColumn++)
                    {
                        var editableIndex = row * 9 + otherColumn;
                        if (editableIndex != givenIndex && board[editableIndex] == 0)
                        {
                            return (givenIndex, editableIndex);
                        }
                    }
                }
            }

            return (-1, -1);
        }

        private static (int SourceIndex, int TargetIndex, int Digit) FindCandidateRemovalScenario(int[] board)
        {
            for (var sourceIndex = 0; sourceIndex < board.Length; sourceIndex++)
            {
                if (board[sourceIndex] != 0)
                {
                    continue;
                }

                for (var digit = 1; digit <= 9; digit++)
                {
                    if (!CanPlace(board, sourceIndex, digit))
                    {
                        continue;
                    }

                    for (var targetIndex = 0; targetIndex < board.Length; targetIndex++)
                    {
                        if (targetIndex == sourceIndex || board[targetIndex] != 0)
                        {
                            continue;
                        }

                        if (!IsRelated(sourceIndex, targetIndex) || !CanPlace(board, targetIndex, digit))
                        {
                            continue;
                        }

                        return (sourceIndex, targetIndex, digit);
                    }
                }
            }

            return (-1, -1, 0);
        }

        private static bool IsRelated(int leftIndex, int rightIndex)
        {
            return leftIndex / 9 == rightIndex / 9 ||
                   leftIndex % 9 == rightIndex % 9 ||
                   ((leftIndex / 9) / 3 == (rightIndex / 9) / 3 && (leftIndex % 9) / 3 == (rightIndex % 9) / 3);
        }

        private static string[] GetAllowedDigits(int cellIndex)
        {
            var board = ReadBoardFromUi();
            var digits = new List<string>();
            for (var value = 1; value <= 9; value++)
            {
                if (CanPlace(board, cellIndex, value))
                {
                    digits.Add(value.ToString());
                }
            }

            return digits.ToArray();
        }

        private static int ParseCellIndex(string cellName)
        {
            var parts = cellName.Split('_');
            Assert.AreEqual(3, parts.Length);
            return int.Parse(parts[1]) * 9 + int.Parse(parts[2]);
        }

        private static string BuildBoardSignature()
        {
            return string.Join(",", ReadBoardFromUi());
        }

        private static int FindSingleChangedCell(int[] before, int[] after)
        {
            var changedIndex = -1;
            for (var i = 0; i < before.Length; i++)
            {
                if (before[i] == after[i])
                {
                    continue;
                }

                Assert.AreEqual(-1, changedIndex, "Only one cell should change.");
                changedIndex = i;
            }

            return changedIndex;
        }

        private static bool SolveBoard(int[] board)
        {
            var index = FindBestEmptyCell(board);
            if (index < 0)
            {
                return true;
            }

            for (var value = 1; value <= 9; value++)
            {
                if (!CanPlace(board, index, value))
                {
                    continue;
                }

                board[index] = value;
                if (SolveBoard(board))
                {
                    return true;
                }

                board[index] = 0;
            }

            return false;
        }

        private static int FindBestEmptyCell(int[] board)
        {
            var bestIndex = -1;
            var bestCount = int.MaxValue;
            for (var i = 0; i < board.Length; i++)
            {
                if (board[i] != 0)
                {
                    continue;
                }

                var count = 0;
                for (var value = 1; value <= 9; value++)
                {
                    if (CanPlace(board, i, value))
                    {
                        count++;
                    }
                }

                if (count < bestCount)
                {
                    bestCount = count;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static bool CanPlace(int[] board, int index, int value)
        {
            var row = index / 9;
            var column = index % 9;

            for (var i = 0; i < 9; i++)
            {
                if (board[row * 9 + i] == value)
                {
                    return false;
                }

                if (board[i * 9 + column] == value)
                {
                    return false;
                }
            }

            var boxRow = (row / 3) * 3;
            var boxColumn = (column / 3) * 3;
            for (var r = boxRow; r < boxRow + 3; r++)
            {
                for (var c = boxColumn; c < boxColumn + 3; c++)
                {
                    if (board[r * 9 + c] == value)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool HasVertexNear(List<Vector3> vertices, Vector2 expected, float tolerance = 0.05f)
        {
            for (var i = 0; i < vertices.Count; i++)
            {
                if (Vector2.Distance(vertices[i], expected) <= tolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasCornerArcVertex(List<Vector3> vertices, bool isLeft, bool isTop)
        {
            for (var i = 0; i < vertices.Count; i++)
            {
                var vertex = vertices[i];
                var inHorizontalBand = isLeft ? vertex.x < -40f && vertex.x > -60f : vertex.x > 40f && vertex.x < 60f;
                var inVerticalBand = isTop ? vertex.y > 20f && vertex.y < 40f : vertex.y < -20f && vertex.y > -40f;
                if (inHorizontalBand && inVerticalBand)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class TestRuntimeHost : MonoBehaviour
        {
        }

        private static string GetComponentText(GameObject gameObject)
        {
            var component = gameObject.GetComponent("TMP_Text");
            if (component == null)
            {
                component = gameObject.GetComponent("Text");
            }

            Assert.IsNotNull(component, "Missing text component on: " + gameObject.name);
            var property = component.GetType().GetProperty("text");
            Assert.IsNotNull(property, "Missing text property on: " + component.GetType().Name);
            return property.GetValue(component, null) as string ?? string.Empty;
        }

        private static Color GetComponentColor(GameObject gameObject)
        {
            var component = gameObject.GetComponent("TMP_Text");
            if (component == null)
            {
                component = gameObject.GetComponent("Text");
            }

            Assert.IsNotNull(component, "Missing text component on: " + gameObject.name);
            var property = component.GetType().GetProperty("color");
            Assert.IsNotNull(property, "Missing color property on: " + component.GetType().Name);
            return (Color)property.GetValue(component, null);
        }
    }
}
