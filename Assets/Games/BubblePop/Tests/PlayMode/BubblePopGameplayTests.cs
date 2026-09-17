using System.Collections;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests
{
    public sealed class BubblePopGameplayTests
    {
        private readonly string[] preferenceKeys = {
            "huanyouyu.dropdown.bubble-pop.BubblePopSize",
            "huanyouyu.dropdown.bubble-pop.BubblePopColor",
            "huanyouyu.dropdown.bubble-pop.BubblePopRebound"
        };
        private int?[] savedPreferences;

        [SetUp]
        public void IsolatePreferences()
        {
            savedPreferences = new int?[preferenceKeys.Length];
            for (var i = 0; i < preferenceKeys.Length; i++)
            {
                savedPreferences[i] = PlayerPrefs.HasKey(preferenceKeys[i]) ? (int?)PlayerPrefs.GetInt(preferenceKeys[i]) : null;
                PlayerPrefs.DeleteKey(preferenceKeys[i]);
            }
        }

        [TearDown]
        public void RestorePreferences()
        {
            for (var i = 0; i < preferenceKeys.Length; i++)
            {
                if (savedPreferences[i].HasValue) PlayerPrefs.SetInt(preferenceKeys[i], savedPreferences[i].Value);
                else PlayerPrefs.DeleteKey(preferenceKeys[i]);
            }
            PlayerPrefs.Save();
        }

        [Test]
        public void GeneratedSoundsMatchWeixinPcmClockAndContainAudio()
        {
            var count = 0;
            foreach (var clip in Resources.FindObjectsOfTypeAll<AudioClip>())
            {
                if (clip.name != "BubblePopSoftPop" && clip.name != "BubblePopSoftRebound") continue;
                Assert.AreEqual(44100, clip.frequency, "Weixin reports PCM lengths in 44100 Hz frames.");
                Assert.AreEqual(1, clip.channels);
                Assert.That(clip.length, Is.InRange(0.079f, 0.091f));
                var data = new float[clip.samples];
                Assert.IsTrue(clip.GetData(data, 0));
                var peak = 0f;
                foreach (var sample in data)
                {
                    Assert.IsFalse(float.IsNaN(sample) || float.IsInfinity(sample));
                    peak = Mathf.Max(peak, Mathf.Abs(sample));
                }
                Assert.That(peak, Is.InRange(0.1f, 1f), "Each tone must contain audible, unclipped samples.");
                count++;
            }
            Assert.AreEqual(10, count);
        }
        [UnityTest]
        public IEnumerator DropdownSelectionsSurviveViewRecreation()
        {
            yield return ChooseDropdown("BubblePopSizeDropdown", 4);
            yield return ChooseDropdown("BubblePopColorDropdown", 5);
            yield return ChooseDropdown("BubblePopReboundDropdown", 2);
            Assert.AreEqual(4, PlayerPrefs.GetInt(preferenceKeys[0], -1));
            Assert.AreEqual(5, PlayerPrefs.GetInt(preferenceKeys[1], -1));
            Assert.AreEqual(2, PlayerPrefs.GetInt(preferenceKeys[2], -1));
            view.Dispose();
            yield return null;
            view = new BubblePopGameView(host.GetComponent<CanvasScaler>(), host.transform, null, null);
            board = host.GetComponentInChildren<BubblePopBoard>();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(20, board.ColumnCount);
            Assert.AreEqual(24, board.RowCount);
            Assert.AreEqual(5, board.ColorMode);
            Assert.AreEqual(2, board.ReboundMode);
            Assert.AreEqual(4, GameObject.Find("BubblePopSizeDropdown").GetComponent<MiniGameDropdown>().Value);
            Assert.AreEqual(5, GameObject.Find("BubblePopColorDropdown").GetComponent<MiniGameDropdown>().Value);
            Assert.AreEqual(2, GameObject.Find("BubblePopReboundDropdown").GetComponent<MiniGameDropdown>().Value);
            board.OnPointerDown(Pointer(0)); board.OnPointerUp(Pointer(0));
            Assert.AreEqual(1, board.PressedCount);
            view.Tick(1.01f);
            Assert.AreEqual(0, board.PressedCount);
        }

        [UnityTest]
        public IEnumerator InvalidRememberedOptionsFallBackToDefaults()
        {
            PlayerPrefs.SetInt(preferenceKeys[0], 99);
            PlayerPrefs.SetInt(preferenceKeys[1], -1);
            PlayerPrefs.SetInt(preferenceKeys[2], 99);
            view.Dispose();
            yield return null;
            view = new BubblePopGameView(host.GetComponent<CanvasScaler>(), host.transform, null, null);
            board = host.GetComponentInChildren<BubblePopBoard>();
            yield return null;
            Assert.AreEqual(6, board.ColumnCount);
            Assert.AreEqual(7, board.RowCount);
            Assert.AreEqual(0, board.ColorMode);
            Assert.AreEqual(0, board.ReboundMode);
            Assert.AreEqual(1, GameObject.Find("BubblePopSizeDropdown").GetComponent<MiniGameDropdown>().Value);
        }
        private GameObject host;
        private GameObject events;
        private BubblePopGameView view;
        private BubblePopBoard board;
        private bool exited;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            host = new GameObject("BubblePopTestHost", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = host.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            if (EventSystem.current == null)
                events = new GameObject("BubblePopTestEvents", typeof(EventSystem), typeof(StandaloneInputModule));
            view = new BubblePopGameView(scaler, host.transform, _ => Assert.Fail("No settlement expected."), () => exited = true);
            board = host.GetComponentInChildren<BubblePopBoard>();
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            view.Dispose();
            Object.Destroy(host);
            if (events != null) Object.Destroy(events);
            yield return null;
        }

        private PointerEventData Pointer(int index, int pointerId = -1)
        {
            var socket = board.transform.Find("Bubble_" + index);
            return new PointerEventData(EventSystem.current) {
                pointerId = pointerId, button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, socket.position)
            };
        }

        private IEnumerator ChooseDropdown(string name, int option)
        {
            GameObject.Find(name).GetComponent<Button>().onClick.Invoke();
            yield return null;
            var item = GameObject.Find("Item" + option);
            Assert.IsNotNull(item);
            item.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTest]
        public IEnumerator ReboundOptionsUseIndependentTimersAndAllowPressingAgain()
        {
            yield return ChooseDropdown("BubblePopReboundDropdown", 1);
            board.OnPointerDown(Pointer(0)); board.OnPointerUp(Pointer(0));
            view.Tick(4.9f);
            Assert.AreEqual(1, board.PressedCount);
            board.OnPointerDown(Pointer(1)); board.OnPointerUp(Pointer(1));
            view.Tick(0.2f);
            Assert.AreEqual(1, board.PressedCount);
            board.OnPointerDown(Pointer(0)); board.OnPointerUp(Pointer(0));
            Assert.AreEqual(2, board.PressedCount);
            board.ResetBoard();
            yield return ChooseDropdown("BubblePopReboundDropdown", 2);
            board.OnPointerDown(Pointer(0)); board.OnPointerUp(Pointer(0));
            view.Tick(0.99f);
            Assert.AreEqual(1, board.PressedCount);
            view.Tick(0.02f);
            Assert.AreEqual(0, board.PressedCount);
            yield return ChooseDropdown("BubblePopReboundDropdown", 3);
            board.OnPointerDown(Pointer(0)); board.OnPointerUp(Pointer(0));
            view.Tick(0.27f);
            Assert.AreEqual(1, board.PressedCount, "Instant rebound must preserve the press animation.");
            view.Tick(0.02f);
            Assert.AreEqual(0, board.PressedCount);
        }

        [UnityTest]
        public IEnumerator ChangingReboundAndPausingHandlePendingBubbles()
        {
            board.OnPointerDown(Pointer(0)); board.OnPointerUp(Pointer(0));
            yield return ChooseDropdown("BubblePopReboundDropdown", 2);
            board.SetPaused(true);
            view.Tick(10f);
            Assert.AreEqual(1, board.PressedCount);
            board.SetPaused(false);
            view.Tick(0.5f);
            Assert.AreEqual(1, board.PressedCount);
            yield return ChooseDropdown("BubblePopReboundDropdown", 0);
            view.Tick(10f);
            Assert.AreEqual(1, board.PressedCount);
            yield return ChooseDropdown("BubblePopReboundDropdown", 1);
            yield return ChooseDropdown("BubblePopSizeDropdown", 4);
            Assert.AreEqual(1, board.ReboundMode);
            board.OnPointerDown(Pointer(479)); board.OnPointerUp(Pointer(479));
            Assert.AreEqual(1, board.PressedCount, "Largest bubble center must be hittable before timer advances.");
            view.Tick(4.99f);
            Assert.AreEqual(1, board.PressedCount);
            view.Tick(0.02f);
            Assert.AreEqual(0, board.PressedCount);
            board.ResetBoard();
            view.Tick(10f);
            Assert.AreEqual(0, board.PressedCount);
        }
        [UnityTest]
        public IEnumerator FirstEightByNineSelectionCost()
        { yield return MeasureFirstSelection(2, 72); }

        [UnityTest]
        public IEnumerator FirstTwelveBySixteenSelectionCost()
        { yield return MeasureFirstSelection(3, 192); }

        private IEnumerator MeasureFirstSelection(int option, int expectedCount)
        {
            GameObject.Find("BubblePopSizeDropdown").GetComponent<Button>().onClick.Invoke();
            yield return null;
            var item = GameObject.Find("Item" + option).GetComponent<Button>();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            item.onClick.Invoke();
            var createMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            Canvas.ForceUpdateCanvases();
            Debug.Log("BubblePopFirstSelection count=" + expectedCount + " createMs=" + createMs.ToString("F3")
                + " layoutRenderMs=" + watch.Elapsed.TotalMilliseconds.ToString("F3"));
            yield return null;
            Assert.AreEqual(expectedCount, board.GetComponentsInChildren<BubblePopGraphic>().Length);
        }
        [UnityTest]
        public IEnumerator FirstLargeBoardSelectionCost()
        {
            GameObject.Find("BubblePopSizeDropdown").GetComponent<Button>().onClick.Invoke();
            yield return null;
            var item = GameObject.Find("Item4").GetComponent<Button>();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            item.onClick.Invoke();
            var createMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            Canvas.ForceUpdateCanvases();
            var renderMs = watch.Elapsed.TotalMilliseconds;
            Debug.Log("BubblePopFirstSelection createMs=" + createMs.ToString("F3")
                + " layoutRenderMs=" + renderMs.ToString("F3"));
            yield return null;
            Assert.AreEqual(480, board.GetComponentsInChildren<BubblePopGraphic>().Length);            var firstBubble = board.transform.Find("Bubble_0");
            yield return ChooseDropdown("BubblePopSizeDropdown", 0);
            yield return ChooseDropdown("BubblePopSizeDropdown", 4);
            Assert.AreSame(firstBubble, board.transform.Find("Bubble_0"));
            Assert.AreEqual(480, board.GetComponentsInChildren<BubblePopGraphic>(true).Length);
        }
        [UnityTest]
        public IEnumerator LargeBoardRenderingCost()
        {
            yield return ChooseDropdown("BubblePopSizeDropdown", 4);
            view.Tick(2f);
            Canvas.ForceUpdateCanvases();
            var faces = board.GetComponentsInChildren<BubblePopGraphic>();

            var vertices = 0;
            foreach (var face in faces) { vertices += face.canvasRenderer.GetMesh().vertexCount; }

            var dirty = 0;
            UnityEngine.Events.UnityAction countDirty = () => dirty++;
            foreach (var face in faces) face.RegisterDirtyVerticesCallback(countDirty);
            var timings = new double[90];
            board.ResetBoard();
            Canvas.ForceUpdateCanvases();
            dirty = 0;
            for (var frame = 0; frame < timings.Length; frame++)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                view.Tick(1f / 60f);
                Canvas.ForceUpdateCanvases();
                watch.Stop();
                timings[frame] = watch.Elapsed.TotalMilliseconds;
                yield return null;
            }
            foreach (var face in faces) face.UnregisterDirtyVerticesCallback(countDirty);
            System.Array.Sort(timings);
            Debug.Log("BubblePopProfile vertices=" + vertices + " dirty=" + dirty
                + " medianMs=" + timings[45].ToString("F3") + " p95Ms=" + timings[85].ToString("F3")
                + " maxMs=" + timings[89].ToString("F3"));
            Assert.AreEqual(480, faces.Length);
            Assert.AreEqual(0, dirty, "Refill scale animation should not rebuild unchanged bubble surfaces.");
            Assert.Less(vertices, 100000, "Small bubbles should stay within the large-board geometry budget.");
            board.OnPointerDown(Pointer(0));
            board.OnDrag(Pointer(19));
            board.OnPointerUp(Pointer(19));
            view.Tick(0.1f);
            yield return null;
            CapturePreview("bubble-pop-large-optimized.png");
        }
        [UnityTest]
        public IEnumerator SizeDropdownRebuildsBoardAndKeepsEntireBoardVisible()
        {

            foreach (var option in new[] { 0, 2, 3, 4, 1 })
            {
                yield return ChooseDropdown("BubblePopSizeDropdown", option);
                var columns = new[] { 4, 6, 8, 12, 20 }[option];
                var rows = new[] { 5, 7, 9, 16, 24 }[option];
                Assert.AreEqual(columns, board.ColumnCount);
                Assert.AreEqual(rows, board.RowCount);
                Assert.AreEqual(columns * rows, board.GetComponentsInChildren<BubblePopGraphic>().Length);
                Assert.AreEqual(0, board.PressedCount);

                var rect = (RectTransform)board.transform;
                Assert.AreEqual(columns / (float)rows, rect.rect.width / rect.rect.height, 0.001f);
                Assert.AreEqual(Vector3.one, rect.localScale);
                var area = (RectTransform)rect.parent;
                Assert.LessOrEqual(rect.rect.width, area.rect.width + 1f);
                Assert.LessOrEqual(rect.rect.height, area.rect.height + 1f);
                var tap = Pointer(columns * columns, 21);
                board.OnPointerDown(tap);
                board.OnPointerUp(tap);
                Assert.AreEqual(1, board.PressedCount, "Size=" + columns + "x" + rows + " rect=" + rect.rect + " bubble=" + board.transform.Find("Bubble_" + columns * columns).localPosition);
                for (var row = 0; row < board.RowCount; row++)
                {
                    board.OnPointerDown(Pointer(row * columns));
                    board.OnDrag(Pointer(row * columns + columns - 1));
                    board.OnPointerUp(Pointer(row * columns));
                }
                Assert.AreEqual(columns * rows, board.PressedCount);
                view.Tick(10f);
                Assert.AreEqual(columns * rows, board.PressedCount);
                board.ResetBoard();
                Assert.AreEqual(0, board.PressedCount);
            }
        }

        [UnityTest]
        public IEnumerator ColorDropdownPreservesPressesAndSingleColorSurvivesRefill()
        {
            board.OnPointerDown(Pointer(20));
            board.OnPointerUp(Pointer(20));
            yield return ChooseDropdown("BubblePopColorDropdown", 2);
            Assert.AreEqual(1, board.PressedCount);
            var faces = board.GetComponentsInChildren<BubblePopGraphic>();
            var single = faces[0].color;
            foreach (var face in faces) Assert.AreEqual(single, face.color);
            board.ResetBoard();
            Assert.AreEqual(single, faces[0].color);
            yield return ChooseDropdown("BubblePopColorDropdown", 1);
            Assert.AreNotEqual(faces[0].color, faces[1].color);
            Assert.AreEqual(1, board.ColorMode);
            yield return ChooseDropdown("BubblePopSizeDropdown", 2);
            Assert.AreEqual(1, board.ColorMode);
            yield return ChooseDropdown("BubblePopColorDropdown", 0);
            faces = board.GetComponentsInChildren<BubblePopGraphic>();
            Assert.AreEqual(faces[0].color, faces[1].color);
            Assert.AreNotEqual(faces[0].color, faces[board.ColumnCount].color);
        }
        [Test]
        public void BoardReceivesInputDirectlyAndHasNoNavigationControls()
        {
            var e = Pointer(21);
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(e, hits);
            Assert.IsNotEmpty(hits);
            Assert.AreEqual(board.gameObject, hits[0].gameObject);
            ExecuteEvents.Execute(hits[0].gameObject, e, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(hits[0].gameObject, e, ExecuteEvents.pointerUpHandler);
            Assert.AreEqual(1, board.PressedCount);
            e.scrollDelta = new Vector2(0f, 30f);
            Assert.IsFalse(ExecuteEvents.Execute(board.gameObject, e, ExecuteEvents.scrollHandler));
            Assert.AreEqual(Vector3.one, board.transform.localScale);
            Assert.IsNull(GameObject.Find("BubblePopZoomSlider"));
            Assert.IsNull(GameObject.Find("BubblePopMoveButton"));
            Assert.IsNull(GameObject.Find("BubblePopOverviewButton"));
        }
        [UnityTest]
        public IEnumerator PressAndRefillAnimationsSettleAndPauseFreezesThem()
        {
            board.OnPointerDown(Pointer(0));
            board.OnDrag(Pointer(2));
            board.OnPointerUp(Pointer(2));
            view.Tick(0.08f);
            var face = board.transform.Find("Bubble_0");
            var compressed = face.localScale;
            board.SetPaused(true);
            view.Tick(1f);
            Assert.AreEqual(compressed, face.localScale);
            board.SetPaused(false);
            view.Tick(0.3f);
            Assert.AreNotEqual(compressed, face.localScale);
            yield return null;
            CapturePreview();
            board.ResetBoard();
            var start = face.localScale;
            view.Tick(0.2f);
            Assert.AreNotEqual(start, face.localScale);
            view.Tick(1f);
            Assert.AreEqual(Vector3.one, face.localScale);
            Assert.AreEqual(Vector3.one, board.transform.Find("Bubble_41").localScale);
        }

        private void CapturePreview(string fileName = "bubble-pop-effects.png")
        {
            var cameraObject = new GameObject("BubblePreviewCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            var target = new RenderTexture(720, 1280, 24);
            var canvas = host.GetComponent<Canvas>();
            var previousTarget = RenderTexture.active;
            var pixels = new Texture2D(720, 1280, TextureFormat.RGB24, false);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.white;
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0);
                pixels.Apply();
                System.IO.Directory.CreateDirectory("TestResults");
                System.IO.File.WriteAllBytes(System.IO.Path.Combine("TestResults", fileName), pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                RenderTexture.active = previousTarget;
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(pixels);
                Object.Destroy(cameraObject);
            }
        }
        [Test]
        public void TapAndFastDragPressEachBubbleOnlyOnce()
        {
            var first = Pointer(0);
            ExecuteEvents.Execute(board.gameObject, first, ExecuteEvents.pointerDownHandler);
            Assert.AreEqual(1, board.PressedCount);
            ExecuteEvents.Execute(board.gameObject, Pointer(5), ExecuteEvents.dragHandler);
            Assert.AreEqual(6, board.PressedCount, "Fast swipes must also press bubbles between events.");
            board.OnDrag(first);
            Assert.AreEqual(6, board.PressedCount);
            board.OnPointerUp(first);
            board.OnPointerDown(first);
            Assert.AreEqual(6, board.PressedCount);
        }

        [Test]
        public void FullBoardWithNoReboundStaysPressedUntilManualReset()
        {
            for (var row = 0; row < BubblePopBoard.Rows; row++)
            {
                board.OnPointerDown(Pointer(row * 6));
                board.OnDrag(Pointer(row * 6 + 5));
                if (row < 6) board.OnPointerUp(Pointer(row * 6));
            }
            Assert.AreEqual(42, board.PressedCount);
            view.Tick(1f);
            Assert.AreEqual(42, board.PressedCount);
            board.OnPointerUp(Pointer(41));
            view.Tick(10f);
            Assert.AreEqual(42, board.PressedCount);
            GameObject.Find("BubblePopResetButton").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(0, board.PressedCount);
            Assert.IsFalse(exited);
        }

        [Test]
        public void PauseCancelsGestureAndResetClearsPressedState()
        {
            board.OnPointerDown(Pointer(0));
            board.SetPaused(true);
            board.OnDrag(Pointer(5));
            board.OnPointerDown(Pointer(8));
            Assert.AreEqual(1, board.PressedCount);
            board.SetPaused(false);
            board.OnDrag(Pointer(5));
            Assert.AreEqual(1, board.PressedCount, "Old touch must not continue after pause.");
            GameObject.Find("BubblePopResetButton").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(0, board.PressedCount);
            board.OnPointerDown(Pointer(0));
            Assert.AreEqual(1, board.PressedCount);
        }

        [Test]
        public void OtherPointerAndRightClickCannotHijackGesture()
        {
            var right = Pointer(0);
            right.button = PointerEventData.InputButton.Right;
            board.OnPointerDown(right);
            Assert.AreEqual(0, board.PressedCount);
            board.OnPointerDown(Pointer(0, 1));
            board.OnPointerDown(Pointer(5, 2));
            board.OnDrag(Pointer(5, 2));
            board.OnPointerUp(Pointer(5, 2));
            Assert.AreEqual(1, board.PressedCount);
            board.OnDrag(Pointer(5, 1));
            Assert.AreEqual(6, board.PressedCount);
        }

        [UnityTest]
        public IEnumerator HallCanEnterExitAndReenterBubblePop()
        {
            view.Dispose();
            Object.Destroy(host);
            yield return null;
            host = new GameObject("BubblePopControllerHost", typeof(MiniGameAppController));
            var controller = host.GetComponent<MiniGameAppController>();
            controller.EnterGame(BubblePopGameView.GameIdConstant);
            yield return null;
            Assert.IsTrue(controller.HasActiveGame);
            Assert.IsNotNull(GameObject.Find("BubblePopBoard"));
            controller.ExitCurrentGameToHall();
            yield return null;
            Assert.IsFalse(controller.HasActiveGame);
            Assert.IsNull(GameObject.Find("BubblePopBoard"));
            controller.EnterGame(BubblePopGameView.GameIdConstant);
            yield return null;
            Assert.AreEqual(0, GameObject.Find("BubblePopBoard").GetComponent<BubblePopBoard>().PressedCount);
            controller.ExitCurrentGameToHall();
        }
        [Test]
        public void BoardFitsContentAndHasLocalizedCatalogEntry()
        {
            var rect = (RectTransform)board.transform;
            var area = (RectTransform)rect.parent;
            Assert.Greater(rect.rect.width, 0f);
            Assert.LessOrEqual(rect.rect.width, area.rect.width + 1f);
            Assert.LessOrEqual(rect.rect.height, area.rect.height + 1f);
            var definition = MiniGameCatalog.GetDefinition(BubblePopGameView.GameIdConstant);
            Assert.IsNotNull(definition);
            Assert.IsTrue(definition.IsPlayable);
            Assert.AreEqual("彩色泡泡板", UiTextCatalog.Get("game.bubble-pop.name"));
        }
    }
}
