using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests
{
    public sealed class CabinetSortGameplayTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void RandomBoardsAndReshufflesAlwaysRemainSolvable()
        {
            for (var seed = 0; seed < 250; seed++)
            {
                var random = new System.Random(seed);
                var board = new CabinetSortBoard(random);
                Assert.AreEqual(81, board.Count);
                Assert.AreEqual(144, board.TotalCount);
                Assert.AreEqual(63, board.HiddenCount);
                Assert.AreEqual(24, Counts(board).Count);
                AssertCounts(board);
                var moves = 0;
                while (board.Remaining > 0 && moves < board.TotalCount * 2)
                {
                    if (moves % 7 == 0)
                    {
                        var before = Counts(board);
                        board.Shuffle(random);
                        CollectionAssert.AreEquivalent(before, Counts(board));
                    }
                    Assert.IsTrue(board.TryGetHint(out var a, out var b), "No hint for seed " + seed);
                    board.Swap(a, b);
                    ResolveRefills(board);
                    AssertCounts(board);
                    moves++;
                }
                Assert.AreEqual(0, board.Remaining, "Unsolvable seed " + seed);
                Assert.IsFalse(board.TryGetHint(out _, out _));
            }
        }

        [Test]
        public void InvalidSwapsDoNotChangeTheBoardOrMoveCount()
        {
            var board = new CabinetSortBoard(new System.Random(12));
            var before = Counts(board);
            board.Swap(-1, 4);
            board.Swap(0, board.Count);
            board.Swap(2, 2);
            Assert.AreEqual(0, board.Moves);
            CollectionAssert.AreEquivalent(before, Counts(board));
        }

        [UnityTest]
        public IEnumerator HintRemainsVisibleUntilBothItemsAreChosen()
        {
            yield return OpenCabinet();
            var runtime = ActiveCabinet();
            Click("HintButton");
            var first = (int)typeof(CabinetSortGameView).GetField("hintFirst", Private).GetValue(runtime);
            var second = (int)typeof(CabinetSortGameView).GetField("hintSecond", Private).GetValue(runtime);
            Assert.GreaterOrEqual(first, 0);
            Assert.GreaterOrEqual(second, 0);
            Click("CabinetItem_" + first);
            Assert.IsTrue(Item(second).GetComponentInChildren<Outline>().enabled,
                "Choosing the first hinted item must keep the destination visible.");
            yield return new WaitForSecondsRealtime(.25f);
            Assert.Greater(Item(second).GetComponentInChildren<RawImage>().transform.localScale.x, 1.04f,
                "The hinted item must visibly pulse instead of relying only on a thin outline.");
            AssertNoItemBackgrounds();
            Click("CabinetItem_" + second);
            yield return WaitForMotion(runtime);
            Assert.AreEqual(-1, (int)typeof(CabinetSortGameView).GetField("hintFirst", Private).GetValue(runtime));
            Assert.AreEqual(1, GetBoard(runtime).Moves);
        }

        [UnityTest]
        public IEnumerator HallEntryClearReplayAndExitUseFreePlayFlow()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            var load = SceneManager.LoadSceneAsync("SampleScene");
            while (!load.isDone) yield return null;
            yield return null;
            var controller = Object.FindObjectOfType<MiniGameAppController>();
            Assert.IsNotNull(controller);
            controller.EnterGame(CabinetSortGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var runtime = typeof(MiniGameAppController).GetField("activeGame", Private).GetValue(controller) as CabinetSortGameView;
            Assert.IsNotNull(runtime);
            var board = GetBoard(runtime);
            var root = GameObject.Find("CabinetSortView");
            Assert.IsNotNull(root);
            var icons = root.GetComponentsInChildren<RawImage>();
            var count = 0;
            foreach (var icon in icons)
            {
                if (icon.name != "Icon") continue;
                Assert.IsNotNull(icon.texture);
                Assert.Greater(icon.rectTransform.rect.width, 0);
                count++;
            }
            Assert.AreEqual(81, count);
            Assert.IsNull(root.transform.Find("LevelSelectButton"));
            Click("HintButton");
            AssertNoItemBackgrounds();
            Assert.GreaterOrEqual((int)typeof(CabinetSortGameView).GetField("hintFirst", Private).GetValue(runtime), 0);
            Click("ShuffleButton");
            AssertNoItemBackgrounds();
            Assert.AreEqual(144, board.Remaining);
            Assert.AreEqual(63, board.HiddenCount);
            var shelf0 = GameObject.Find("Shelf_0").transform;
            var shelf2 = GameObject.Find("Shelf_2").transform;
            var shelf3 = GameObject.Find("Shelf_3").transform;
            Assert.Greater(shelf2.position.x, shelf0.position.x);
            Assert.AreEqual(shelf0.position.y, shelf2.position.y, .1f);
            Assert.Less(shelf3.position.y, shelf0.position.y);
            Click("CabinetItem_0");
            yield return new WaitForSecondsRealtime(.15f);
            // Save a real UI render for visual verification without affecting the scene camera.
            var canvas = root.GetComponentInParent<Canvas>();
            var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera;
            var cameraObject = new GameObject("CabinetCapture", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            var target = new RenderTexture(720, 1280, 24);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var capture = new Texture2D(720, 1280, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0);
            capture.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "../Logs/cabinet-sort-preview.png"), capture.EncodeToPNG());
            RenderTexture.active = previous;
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            camera.targetTexture = null;
            Object.Destroy(cameraObject);
            Object.Destroy(capture);
            target.Release();
            Object.Destroy(target);
            Click("CabinetItem_0");
            var sawReveal = false;
            for (var steps = 0; board.Remaining > 0 && steps < board.TotalCount; steps++)
            {
                Assert.IsTrue(board.TryGetHint(out var a, out var b));
                var hiddenBefore = board.HiddenCount;
                Click("CabinetItem_" + a);
                Click("CabinetItem_" + b);
                var deadline = Time.realtimeSinceStartup + 8;
                while (IsAnimating(runtime) && Time.realtimeSinceStartup < deadline)
                {
                    AssertNoItemBackgrounds();
                    if (board.HiddenCount < hiddenBefore)
                    {
                        foreach (var icon in root.GetComponentsInChildren<RawImage>())
                            if (icon.name == "Icon" && icon.enabled && icon.transform.localScale.x < .9f && icon.color.a < .95f)
                                sawReveal = true;
                    }
                    yield return null;
                }
                yield return WaitForMotion(runtime);
            }
            Assert.IsTrue(sawReveal, "Hidden items must pop into the cleared shelves.");
            Assert.AreEqual(0, board.Remaining);
            Assert.AreEqual(0, board.HiddenCount);
            yield return null;
            Assert.IsNotNull(GameObject.Find("CabinetSortSettlementPanel"));
            Click("NextButton");
            yield return null;
            Assert.AreEqual(144, GetBoard(runtime).Remaining);
            Assert.AreEqual(63, GetBoard(runtime).HiddenCount);
            Assert.AreEqual(240, controller.GetProgress(CabinetSortGameView.GameIdConstant).TotalCoinCount);
            Assert.AreEqual(1, controller.GetProgress(CabinetSortGameView.GameIdConstant).TotalChestCount);
            typeof(CabinetSortGameView).GetMethod("OnPauseRequested", Private).Invoke(runtime, null);
            var pausedBoard = GetBoard(runtime);
            pausedBoard.TryGetHint(out var first, out var second);
            Click("CabinetItem_" + first);
            Click("CabinetItem_" + second);
            Assert.AreEqual(0, pausedBoard.Moves);
            typeof(CabinetSortGameView).GetMethod("ShowResult", Private).Invoke(runtime, new object[] { false });
            yield return null;
            Click("NextButton");
            yield return null;
            Assert.IsFalse(controller.HasActiveGame);
            Assert.AreEqual(240, controller.GetProgress(CabinetSortGameView.GameIdConstant).TotalCoinCount);
        }

        private static CabinetSortBoard GetBoard(CabinetSortGameView runtime) =>
            (CabinetSortBoard)typeof(CabinetSortGameView).GetField("board", Private).GetValue(runtime);

        [UnityTest]
        public IEnumerator IconsKeepTextureProportionsAndCabinetUsesAvailableHeight()
        {
            yield return OpenCabinet();
            var runtime = ActiveCabinet();
            var root = GameObject.Find("CabinetSortView");
            AssertCabinetIconProportions(root);
            var frame = GameObject.Find("CabinetFrame").GetComponent<RectTransform>();
            var content = GameObject.Find("CabinetSortContent").GetComponent<RectTransform>();
            Assert.AreEqual(content.rect.height * .92f, frame.rect.height, .1f,
                "Cabinet should retain its full available height without a fixed aspect ratio.");
            var shelf = GameObject.Find("Shelf_0").GetComponent<RectTransform>();
            Assert.Less(shelf.rect.height, shelf.rect.width * .5f,
                "Additional rows should remove the oversized space above the items.");
            Assert.IsNotNull(GameObject.Find("Shelf_26"), "The cabinet should contain three columns and nine rows.");
            Click("ShuffleButton");
            Canvas.ForceUpdateCanvases();
            AssertCabinetIconProportions(root);
            var board = GetBoard(runtime);
            for (var i = 0; i < 3; i++)
            {
                Assert.IsTrue(board.TryGetHint(out var first, out var second));
                Click("CabinetItem_" + first);
                Click("CabinetItem_" + second);
                AssertCabinetIconProportions(root);
                yield return WaitForMotion(runtime);
                Canvas.ForceUpdateCanvases();
                AssertCabinetIconProportions(root);
            }
            Assert.Less(board.HiddenCount, 63, "The aspect check must cover newly revealed items too.");
        }

        private static void AssertCabinetIconProportions(GameObject root)
        {
            foreach (var icon in root.GetComponentsInChildren<RawImage>())
            {
                if (icon.name != "Icon" && icon.name != "MovingFirst" && icon.name != "MovingSecond") continue;
                var rect = icon.rectTransform.rect;
                var scale = icon.rectTransform.lossyScale;
                Assert.AreEqual((float)icon.texture.width / icon.texture.height, rect.width * scale.x / (rect.height * scale.y), .01f,
                    "Distorted texture: " + icon.texture.name);
            }
        }

        [UnityTest]
        public IEnumerator ClickSelectionExchangeAndClearAnimateAndBlockDuplicateInput()
        {
            yield return OpenCabinet();
            var runtime = ActiveCabinet();
            var board = GetBoard(runtime);
            board.TryGetHint(out var a, out var b);
            var firstIcon = Item(a).GetComponentInChildren<RawImage>();
            Click("CabinetItem_" + a);
            yield return new WaitForSecondsRealtime(.12f);
            Assert.Greater(firstIcon.transform.localScale.x, 1.03f, "Selected item should lift and pulse.");
            Assert.AreEqual(0f, Item(a).GetComponent<RoundedRectGraphic>().color.a,
                "Selection should not paint a solid background across the slot.");
            Assert.IsTrue(firstIcon.GetComponent<Outline>().enabled);
            var start = firstIcon.transform.position;
            var originalValue = board[a];
            Click("CabinetItem_" + b);
            Assert.IsTrue(IsAnimating(runtime));
            Assert.AreEqual(originalValue, board[a], "Board must not jump ahead of the movement.");
            Click("CabinetItem_" + a);
            Click("ShuffleButton");
            yield return new WaitForSecondsRealtime(.1f);
            var moving = GameObject.Find("MovingFirst");
            Assert.IsNotNull(moving);
            Assert.Greater(Vector3.Distance(start, moving.transform.position), 1f);
            // Pausing must freeze the moving pieces, then resume the same exchange.
            typeof(CabinetSortGameView).GetMethod("OnPauseRequested", Private).Invoke(runtime, null);
            var pausedPosition = moving.transform.position;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.Less(Vector3.Distance(pausedPosition, moving.transform.position), .01f);
            typeof(CabinetSortGameView).GetField("locked", Private).SetValue(runtime, false);
            yield return WaitForMotion(runtime);
            Assert.AreEqual(1, board.Moves, "Repeated input during movement must not add a swap.");

            var sawFade = false;
            for (var attempt = 0; attempt < 3 && !sawFade; attempt++)
            {
                var remaining = board.Remaining;
                board.TryGetHint(out a, out b);
                Click("CabinetItem_" + a);
                Click("CabinetItem_" + b);
                var deadline = Time.realtimeSinceStartup + 2;
                while (IsAnimating(runtime) && Time.realtimeSinceStartup < deadline)
                {
                    AssertNoItemBackgrounds();
                    foreach (var icon in GameObject.Find("CabinetSortView").GetComponentsInChildren<RawImage>())
                        if (icon.name == "Icon" && icon.enabled && icon.color.a > .05f && icon.color.a < .95f)
                            sawFade = true;
                    yield return null;
                }
                Assert.IsFalse(IsAnimating(runtime));
                if (sawFade) Assert.Less(board.Remaining, remaining);
            }
            Assert.IsTrue(sawFade, "Matched items should visibly fade before disappearing.");
            foreach (var icon in GameObject.Find("CabinetSortView").GetComponentsInChildren<RawImage>())
                if (icon.name == "Icon") Assert.AreEqual(Color.white, icon.color);
        }

        [UnityTest]
        public IEnumerator DragFollowsPointerSwapsAndInvalidDropReturnsWithoutMovingBoard()
        {
            yield return OpenCabinet();
            var runtime = ActiveCabinet();
            var board = GetBoard(runtime);
            board.TryGetHint(out var a, out var b);
            var source = Item(a);
            var data = new PointerEventData(EventSystem.current)
            {
                pointerId = 7, button = PointerEventData.InputButton.Left,
                position = ScreenCenter(source), eligibleForClick = true
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            Assert.IsTrue(hits.Exists(hit => hit.gameObject == source), "The visible item must receive pointer input.");
            ExecuteEvents.Execute(source, data, ExecuteEvents.beginDragHandler);
            Assert.IsFalse(data.eligibleForClick);
            var ghost = GameObject.Find("DraggedItem");
            Assert.IsNotNull(ghost);
            var initial = ghost.transform.position;
            data.position = ScreenCenter(Item(b));
            ExecuteEvents.Execute(source, data, ExecuteEvents.dragHandler);
            Assert.Greater(Vector3.Distance(initial, ghost.transform.position), 1);
            Assert.IsTrue(Item(b).GetComponentInChildren<RawImage>().GetComponent<Outline>().enabled);
            Assert.AreEqual(0f, Item(b).GetComponent<RoundedRectGraphic>().color.a);
            var otherPointer = new PointerEventData(EventSystem.current) { pointerId = 8, position = data.position };
            ExecuteEvents.Execute(source, otherPointer, ExecuteEvents.endDragHandler);
            Assert.AreEqual(0, board.Moves);
            ExecuteEvents.Execute(source, data, ExecuteEvents.endDragHandler);
            Assert.IsTrue(IsAnimating(runtime));
            yield return WaitForMotion(runtime);
            Assert.AreEqual(1, board.Moves);
            Assert.IsNull(GameObject.Find("DraggedItem"));
            board.TryGetHint(out a, out b);
            source = Item(a);
            data.position = ScreenCenter(source);
            ExecuteEvents.Execute(source, data, ExecuteEvents.beginDragHandler);
            data.position = new Vector2(-500, -500);
            ExecuteEvents.Execute(source, data, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(source, data, ExecuteEvents.endDragHandler);
            Assert.IsTrue(IsAnimating(runtime));
            yield return WaitForMotion(runtime);
            Assert.AreEqual(1, board.Moves, "Dropping outside the cabinet must not change the board.");
            Assert.IsTrue(source.GetComponentInChildren<RawImage>().enabled);
            // Disposing during an exchange must cancel the host coroutine and moving overlays.
            Click("CabinetItem_" + a);
            Click("CabinetItem_" + b);
            Object.FindObjectOfType<MiniGameAppController>().ExitCurrentGameToHall();
            yield return new WaitForSecondsRealtime(.7f);
            Assert.IsNull(GameObject.Find("MovingFirst"));
        }

        private static IEnumerator OpenCabinet()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            var load = SceneManager.LoadSceneAsync("SampleScene");
            while (!load.isDone) yield return null;
            yield return null;
            Object.FindObjectOfType<MiniGameAppController>().EnterGame(CabinetSortGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTest]
        public IEnumerator IndependentShelvesAllowConcurrentClicksAndDrags()
        {
            yield return OpenCabinet();
            var runtime = ActiveCabinet();
            var board = GetBoard(runtime);
            for (var round = 0; round < 6; round++)
            {
                var initialMoves = board.Moves;
                Assert.IsTrue(board.TryGetHint(out var a, out var b));
                var excluded = new HashSet<int> { a / 3, b / 3 };
                FindIndependentPair(board, excluded, out var c, out var d);
                excluded.Add(c / 3);
                excluded.Add(d / 3);
                FindIndependentPair(board, excluded, out var e, out var f);
                Click("CabinetItem_" + a);
                Click("CabinetItem_" + b);
                Assert.IsFalse(Item(a).GetComponent<Button>().interactable);
                Assert.IsTrue(Item(c).GetComponent<Button>().interactable,
                    "Unrelated shelves must remain usable while the first exchange moves.");
                Click("CabinetItem_" + c);
                Click("CabinetItem_" + d);
                Assert.AreEqual(2, typeof(CabinetSortGameView).GetField("activeMotions", Private).GetValue(runtime));
                var source = Item(e);
                var data = new PointerEventData(EventSystem.current)
                {
                    pointerId = 12, button = PointerEventData.InputButton.Left,
                    position = ScreenCenter(source)
                };
                ExecuteEvents.Execute(source, data, ExecuteEvents.beginDragHandler);
                Assert.IsNotNull(GameObject.Find("DraggedItem"), "Dragging should start while other shelves animate.");
                data.position = ScreenCenter(Item(f));
                ExecuteEvents.Execute(source, data, ExecuteEvents.dragHandler);
                ExecuteEvents.Execute(source, data, ExecuteEvents.endDragHandler);
                Assert.AreEqual(3, typeof(CabinetSortGameView).GetField("activeMotions", Private).GetValue(runtime));
                Click("CabinetItem_" + a);
                Click("CabinetItem_" + b);
                if (round == 0)
                {
                    typeof(CabinetSortGameView).GetMethod("OnPauseRequested", Private).Invoke(runtime, null);
                    yield return new WaitForSecondsRealtime(.35f);
                    Assert.AreEqual(initialMoves, board.Moves, "Pause must freeze all concurrent exchanges.");
                    Click("ContinueButton");
                }
                yield return WaitForMotion(runtime);
                Assert.AreEqual(initialMoves + 3, board.Moves, "Each exchange should count exactly once.");
                AssertCounts(board);
                AssertCabinetIconProportions(GameObject.Find("CabinetSortView"));
            }
            Assert.Less(board.Remaining, board.TotalCount, "Concurrent exchanges should exercise elimination.");
            Assert.Less(board.HiddenCount, 63, "Concurrent exchanges should exercise refilling.");
            // Also exercise teardown with several coroutine-owned moving images at once.
            var blocked = new HashSet<int>();
            FindIndependentPair(board, blocked, out var first, out var second);
            blocked.Add(first / 3);
            blocked.Add(second / 3);
            FindIndependentPair(board, blocked, out var third, out var fourth);
            Click("CabinetItem_" + first);
            Click("CabinetItem_" + second);
            Click("CabinetItem_" + third);
            Click("CabinetItem_" + fourth);
            Object.FindObjectOfType<MiniGameAppController>().ExitCurrentGameToHall();
            yield return new WaitForSecondsRealtime(.7f);
            Assert.IsNull(GameObject.Find("MovingFirst"));
            Assert.IsNull(GameObject.Find("MovingSecond"));
        }

        private static void FindIndependentPair(CabinetSortBoard board, HashSet<int> excluded, out int first, out int second)
        {
            for (var i = 0; i < board.Count; i++)
            {
                if (excluded.Contains(i / 3) || board[i] < 0) continue;
                for (var j = i + 1; j < board.Count; j++)
                {
                    if (excluded.Contains(j / 3) || i / 3 == j / 3 || board[j] < 0 || board[i] == board[j]) continue;
                    first = i;
                    second = j;
                    return;
                }
            }
            Assert.Fail("No independent exchange available.");
            first = second = -1;
        }

        private static CabinetSortGameView ActiveCabinet() => (CabinetSortGameView)
            typeof(MiniGameAppController).GetField("activeGame", Private).GetValue(Object.FindObjectOfType<MiniGameAppController>());

        private static GameObject Item(int index) => GameObject.Find("CabinetItem_" + index);
        private static Vector2 ScreenCenter(GameObject item) => RectTransformUtility.WorldToScreenPoint(null, item.transform.position);
        private static bool IsAnimating(CabinetSortGameView runtime) =>
            (bool)typeof(CabinetSortGameView).GetField("animating", Private).GetValue(runtime);

        private static IEnumerator WaitForMotion(CabinetSortGameView runtime)
        {
            var deadline = Time.realtimeSinceStartup + 8;
            while (IsAnimating(runtime) && Time.realtimeSinceStartup < deadline)
            {
                AssertNoItemBackgrounds();
                yield return null;
            }
            Assert.IsFalse(IsAnimating(runtime), "Animation did not finish.");
            AssertNoItemBackgrounds();
        }

        private static void AssertNoItemBackgrounds()
        {
            foreach (var graphic in GameObject.Find("CabinetSortView").GetComponentsInChildren<RoundedRectGraphic>())
                if (graphic.name.StartsWith("CabinetItem_", StringComparison.Ordinal))
                    Assert.AreEqual(0f, graphic.color.a, "Item feedback must not draw a slot-sized background: " + graphic.name);
        }

        private static void Click(string name)
        {
            var target = GameObject.Find(name);
            Assert.IsNotNull(target, name);
            target.GetComponent<Button>().onClick.Invoke();
        }

        private static Dictionary<int, int> Counts(CabinetSortBoard board)
        {
            var counts = new Dictionary<int, int>();
            for (var i = 0; i < board.Count; i++)
            {
                if (board[i] < 0) continue;
                counts.TryGetValue(board[i], out var count);
                counts[board[i]] = count + 1;
            }
            return counts;
        }

        private static void AssertCounts(CabinetSortBoard board)
        {
            var counts = Counts(board);
            var reserve = (int[])typeof(CabinetSortBoard).GetField("reserve", Private).GetValue(board);
            var reserveIndex = reserve.Length - board.HiddenCount;
            for (var i = reserveIndex; i < reserve.Length; i++)
            {
                counts.TryGetValue(reserve[i], out var count);
                counts[reserve[i]] = count + 1;
            }
            var total = 0;
            foreach (var count in counts.Values)
            {
                Assert.AreEqual(0, count % 3);
                total += count;
            }
            Assert.AreEqual(board.Remaining, total);
            for (var i = 0; i < board.Count; i += 3)
                Assert.IsFalse(board[i] >= 0 && board[i] == board[i + 1] && board[i] == board[i + 2]);
        }

        private static void ResolveRefills(CabinetSortBoard board)
        {
            var safety = 0;
            int removed;
            do
            {
                var remaining = board.Remaining;
                var hidden = board.HiddenCount;
                var added = board.Refill();
                Assert.AreEqual(hidden - added, board.HiddenCount);
                Assert.AreEqual(remaining, board.Remaining, "Revealing stock must not add items or award points.");
                removed = board.ClearMatches();
                Assert.Less(++safety, 50);
            } while (removed > 0);
            if (board.HiddenCount > 0)
            {
                var visible = 0;
                foreach (var count in Counts(board).Values) visible += count;
                Assert.AreEqual(81, visible);
            }
        }
    }
}
