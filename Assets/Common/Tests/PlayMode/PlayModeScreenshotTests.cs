using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests
{
    public class PlayModeScreenshotTests
    {
        private const int BatchScreenshotWidth = 750;
        private const int BatchScreenshotHeight = 1334;
        private const int MiniGameScreenshotWaitFrames = 8;
        private static readonly Dictionary<string, ScreenshotSetup> MiniGameScreenshotSetups = new Dictionary<string, ScreenshotSetup>
        {
            { ArrowEscapeGameView.GameIdConstant, new ScreenshotSetup(1, 2) }
        };

        [UnityTest]
        public IEnumerator GameBootsWaitsCapturesScreenshotWithoutErrors()
        {
            AssertNoUnexpectedLogs("Before scene load");
            PlayModeGlobalLogMonitor.Clear();

            var load = SceneManager.LoadSceneAsync("SampleScene");
            while (!load.isDone)
            {
                yield return null;
            }
            yield return null;

            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var screenshotDir = Path.Combine(projectRoot, "PlayModeShots");
            if (!Directory.Exists(screenshotDir))
            {
                Directory.CreateDirectory(screenshotDir);
            }
            var mainPath = Path.Combine(screenshotDir, "pm_01_main.png");
            var allGamesPath = Path.Combine(screenshotDir, "pm_02_all_games.png");

            yield return CaptureRealScreenshot(mainPath);

            var allGamesTabObject = GameObject.Find("AllGamesTab");
            Assert.IsNotNull(allGamesTabObject, "AllGamesTab not found.");
            var allGamesTabButton = allGamesTabObject.GetComponent<Button>();
            Assert.IsNotNull(allGamesTabButton, "AllGamesTab missing Button component.");
            allGamesTabButton.onClick.Invoke();

            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            yield return CaptureRealScreenshot(allGamesPath);

            Assert.IsTrue(File.Exists(mainPath), "Screenshot not generated: " + mainPath);
            Assert.IsTrue(File.Exists(allGamesPath), "Screenshot not generated: " + allGamesPath);
            AssertNoUnexpectedLogs("During boot and screenshot capture");
        }

        [UnityTest]
        public IEnumerator CapturesEveryPlayableMiniGameScreenshot()
        {
            AssertNoUnexpectedLogs("Before mini-game screenshot capture");
            PlayModeGlobalLogMonitor.Clear();
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(value => controller = value);
            var screenshotCanvasScope = ConfigureCanvasForBatchMiniGameScreenshots();

            try
            {
                Canvas.ForceUpdateCanvases();
                yield return null;

                var definitions = MiniGameCatalog.GetDefinitions();
                Assert.IsNotNull(definitions, "Mini-game catalog should be available.");
                Assert.Greater(definitions.Count, 0, "Mini-game catalog should contain entries.");

                var screenshotDir = GetScreenshotDirectory();
                var capturedCount = 0;
                for (var i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (definition == null || !definition.IsPlayable)
                    {
                        continue;
                    }

                    ApplyScreenshotSetup(controller, definition.Id);
                    controller.EnterGame(definition.Id);
                    for (var frame = 0; frame < MiniGameScreenshotWaitFrames; frame++)
                    {
                        Canvas.ForceUpdateCanvases();
                        yield return null;
                    }

                    Assert.IsTrue(controller.HasActiveGame, "Mini-game should become active before screenshot: " + definition.Id);
                    var screenshotPath = Path.Combine(screenshotDir, GetMiniGameScreenshotFileName(definition.Id));
                    yield return CaptureRealScreenshot(screenshotPath);
                    Assert.IsTrue(File.Exists(screenshotPath), "Mini-game screenshot should be generated: " + definition.Id);
                    Assert.Greater(new FileInfo(screenshotPath).Length, 2048, "Mini-game screenshot should contain image data: " + definition.Id);
                    capturedCount++;

                    controller.ExitCurrentGameToHall();
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                }

                Assert.Greater(capturedCount, 0, "At least one playable mini-game screenshot should be captured.");
                AssertNoUnexpectedLogs("During mini-game screenshot capture");
            }
            finally
            {
                if (screenshotCanvasScope != null)
                {
                    screenshotCanvasScope.Dispose();
                }
            }
        }

        internal static IEnumerator CaptureRealScreenshot(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            Canvas.ForceUpdateCanvases();
            yield return null;

            if (Application.isBatchMode)
            {
                if (Screen.width != BatchScreenshotWidth || Screen.height != BatchScreenshotHeight)
                {
                    Screen.SetResolution(BatchScreenshotWidth, BatchScreenshotHeight, false);
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                }

                CaptureBatchModeScreenshot(path);
                yield break;
            }

            yield return new WaitForEndOfFrame();

            var captured = ScreenCapture.CaptureScreenshotAsTexture();
            if (captured != null)
            {
                try
                {
                    File.WriteAllBytes(path, captured.EncodeToPNG());
                }
                finally
                {
                    Object.Destroy(captured);
                }

                yield break;
            }

            ScreenCapture.CaptureScreenshot(path);
            for (var i = 0; i < 180; i++)
            {
                if (File.Exists(path) && new FileInfo(path).Length > 0)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Screenshot not generated: " + path);
        }

        private static void CaptureBatchModeScreenshot(string path)
        {
            var camera = Camera.main;
            Assert.IsNotNull(camera, "Missing Main Camera");

            var originalTargetTexture = camera.targetTexture;
            var originalClearFlags = camera.clearFlags;
            var originalBackground = camera.backgroundColor;

            RenderTexture rt = null;
            Texture2D finalTexture = null;

            try
            {
                rt = new RenderTexture(BatchScreenshotWidth, BatchScreenshotHeight, 24);
                camera.targetTexture = rt;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.05f, 0.08f, 0.12f);
                camera.Render();

                RenderTexture.active = rt;
                finalTexture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                finalTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                finalTexture.Apply();

                var canvases = Object.FindObjectsOfType<Canvas>();
                if (canvases.Length > 0)
                {
                    System.Array.Sort(canvases, (a, b) =>
                    {
                        var layerDiff = a.sortingLayerID.CompareTo(b.sortingLayerID);
                        if (layerDiff != 0)
                        {
                            return layerDiff;
                        }

                        return a.sortingOrder.CompareTo(b.sortingOrder);
                    });

                    CompositeCanvases(camera, canvases, rt.width, rt.height, finalTexture);
                }

                File.WriteAllBytes(path, finalTexture.EncodeToPNG());
            }
            finally
            {
                if (finalTexture != null)
                {
                    Object.Destroy(finalTexture);
                }

                camera.targetTexture = originalTargetTexture;
                camera.clearFlags = originalClearFlags;
                camera.backgroundColor = originalBackground;
                RenderTexture.active = null;
                if (rt != null)
                {
                    Object.Destroy(rt);
                }
            }
        }

        private static void CompositeCanvases(Camera sourceCamera, Canvas[] canvases, int width, int height, Texture2D finalTexture)
        {
            var uiRt = new RenderTexture(width, height, 24);
            var uiCameraObj = new GameObject("UICamera");
            var uiCamera = uiCameraObj.AddComponent<Camera>();
            uiCamera.transform.position = sourceCamera.transform.position;
            uiCamera.transform.rotation = sourceCamera.transform.rotation;
            uiCamera.orthographic = true;
            uiCamera.orthographicSize = sourceCamera.orthographicSize;
            uiCamera.clearFlags = CameraClearFlags.SolidColor;
            uiCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            uiCamera.targetTexture = uiRt;

            try
            {
                foreach (var canvas in canvases)
                {
                    CompositeCanvas(canvas, uiCamera, uiRt, finalTexture);
                }
            }
            finally
            {
                Object.Destroy(uiCameraObj);
                Object.Destroy(uiRt);
            }
        }

        private static void CompositeCanvas(Canvas canvas, Camera uiCamera, RenderTexture uiRt, Texture2D finalTexture)
        {
            var originalRenderMode = canvas.renderMode;
            var originalWorldCamera = canvas.worldCamera;
            var originalPlaneDistance = canvas.planeDistance;

            try
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                }

                canvas.worldCamera = uiCamera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();

                uiCamera.cullingMask = 1 << canvas.gameObject.layer;
                uiCamera.Render();

                RenderTexture.active = uiRt;
                var uiTexture = new Texture2D(uiRt.width, uiRt.height, TextureFormat.RGBA32, false);
                try
                {
                    uiTexture.ReadPixels(new Rect(0, 0, uiRt.width, uiRt.height), 0, 0);
                    uiTexture.Apply();
                    CompositeTexture(finalTexture, uiTexture);
                }
                finally
                {
                    Object.Destroy(uiTexture);
                }
            }
            finally
            {
                canvas.renderMode = originalRenderMode;
                canvas.worldCamera = originalWorldCamera;
                canvas.planeDistance = originalPlaneDistance;
            }
        }

        private static void CompositeTexture(Texture2D finalTexture, Texture2D uiTexture)
        {
            var basePixels = finalTexture.GetPixels();
            var uiPixels = uiTexture.GetPixels();
            for (var i = 0; i < basePixels.Length; i++)
            {
                var uiColor = uiPixels[i];
                if (uiColor.a > 0.01f)
                {
                    basePixels[i] = Color.Lerp(basePixels[i], uiColor, uiColor.a);
                }
            }

            finalTexture.SetPixels(basePixels);
            finalTexture.Apply();
        }

        private static void AssertNoUnexpectedLogs(string phase)
        {
            var report = PlayModeGlobalLogMonitor.BuildFailureReport();
            if (!string.IsNullOrEmpty(report))
            {
                Assert.Fail(phase + ": unexpected Error/Exception logs:\n" + report);
            }
        }

        private static IEnumerator LoadController(System.Action<MiniGameAppController> assign)
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
            assign(controller);
        }

        private static string GetScreenshotDirectory()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var screenshotDir = Path.Combine(projectRoot, "PlayModeShots");
            if (!Directory.Exists(screenshotDir))
            {
                Directory.CreateDirectory(screenshotDir);
            }

            return screenshotDir;
        }

        private static string GetMiniGameScreenshotFileName(string gameId)
        {
            var safeId = string.IsNullOrWhiteSpace(gameId) ? "unknown" : gameId.Trim().Replace('-', '_');
            return "pm_" + safeId + ".png";
        }

        private static void ApplyScreenshotSetup(MiniGameAppController controller, string gameId)
        {
            ScreenshotSetup setup;
            if (controller == null || string.IsNullOrWhiteSpace(gameId) || !MiniGameScreenshotSetups.TryGetValue(gameId, out setup))
            {
                return;
            }

            controller.SetLevelProgress(gameId, setup.CurrentLevelIndex, setup.UnlockedLevelCount);
        }

        private static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.DeleteKey(MiniGameRuntimeSettings.PlayerPrefsKey);
            PlayerPrefs.Save();
        }

        private static IDisposable ConfigureCanvasForBatchMiniGameScreenshots()
        {
            if (!Application.isBatchMode)
            {
                return null;
            }

            var canvas = Object.FindObjectOfType<Canvas>();
            Assert.IsNotNull(canvas, "Mini-game screenshot canvas should exist.");
            return new BatchCanvasLayoutScope(canvas, BatchScreenshotWidth, BatchScreenshotHeight);
        }

        private readonly struct ScreenshotSetup
        {
            public readonly int CurrentLevelIndex;
            public readonly int UnlockedLevelCount;

            public ScreenshotSetup(int currentLevelIndex, int unlockedLevelCount)
            {
                CurrentLevelIndex = currentLevelIndex;
                UnlockedLevelCount = unlockedLevelCount;
            }
        }

        private sealed class BatchCanvasLayoutScope : IDisposable
        {
            private readonly Canvas canvas;
            private readonly RenderMode originalRenderMode;
            private readonly Camera originalWorldCamera;
            private readonly float originalPlaneDistance;
            private readonly RenderTexture targetTexture;
            private readonly GameObject cameraObject;

            public BatchCanvasLayoutScope(Canvas canvas, int width, int height)
            {
                this.canvas = canvas;
                originalRenderMode = canvas.renderMode;
                originalWorldCamera = canvas.worldCamera;
                originalPlaneDistance = canvas.planeDistance;

                targetTexture = new RenderTexture(width, height, 24);
                cameraObject = new GameObject("BatchMiniGameScreenshotLayoutCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.orthographic = true;
                camera.orthographicSize = height * 0.5f;
                camera.targetTexture = targetTexture;

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose()
            {
                if (canvas != null)
                {
                    canvas.renderMode = originalRenderMode;
                    canvas.worldCamera = originalWorldCamera;
                    canvas.planeDistance = originalPlaneDistance;
                    Canvas.ForceUpdateCanvases();
                }

                if (cameraObject != null)
                {
                    Object.Destroy(cameraObject);
                }

                if (targetTexture != null)
                {
                    Object.Destroy(targetTexture);
                }
            }
        }
    }
}
