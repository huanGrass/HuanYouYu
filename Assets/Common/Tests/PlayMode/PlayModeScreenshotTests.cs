using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests
{
    public class PlayModeScreenshotTests
    {
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

            CaptureCompositedScreenshot(mainPath);

            var allGamesTabObject = GameObject.Find("AllGamesTab");
            Assert.IsNotNull(allGamesTabObject, "AllGamesTab not found.");
            var allGamesTabButton = allGamesTabObject.GetComponent<Button>();
            Assert.IsNotNull(allGamesTabButton, "AllGamesTab missing Button component.");
            allGamesTabButton.onClick.Invoke();

            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            CaptureCompositedScreenshot(allGamesPath);

            Assert.IsTrue(File.Exists(mainPath), "Screenshot not generated: " + mainPath);
            Assert.IsTrue(File.Exists(allGamesPath), "Screenshot not generated: " + allGamesPath);
            AssertNoUnexpectedLogs("During boot and screenshot capture");
        }

        private static void CaptureCompositedScreenshot(string path)
        {
            var camera = Camera.main;
            Assert.IsNotNull(camera, "Missing Main Camera");

            var originalClearFlags = camera.clearFlags;
            var originalBackground = camera.backgroundColor;

            RenderTexture rt = null;
            Texture2D finalTexture = null;

            try
            {
                rt = new RenderTexture(750, 1334, 24);
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

                    var uiRt = new RenderTexture(rt.width, rt.height, 24);
                    var uiCameraObj = new GameObject("UICamera");
                    var uiCamera = uiCameraObj.AddComponent<Camera>();
                    uiCamera.transform.position = camera.transform.position;
                    uiCamera.transform.rotation = camera.transform.rotation;
                    uiCamera.orthographic = true;
                    uiCamera.orthographicSize = camera.orthographicSize;
                    uiCamera.clearFlags = CameraClearFlags.SolidColor;
                    uiCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                    uiCamera.targetTexture = uiRt;

                    try
                    {
                        foreach (var canvas in canvases)
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

                                uiCamera.cullingMask = 1 << canvas.gameObject.layer;
                                uiCamera.Render();

                                RenderTexture.active = uiRt;
                                var uiTexture = new Texture2D(uiRt.width, uiRt.height, TextureFormat.RGBA32, false);
                                uiTexture.ReadPixels(new Rect(0, 0, uiRt.width, uiRt.height), 0, 0);
                                uiTexture.Apply();

                                var basePixels = finalTexture.GetPixels();
                                var uiPixels = uiTexture.GetPixels();
                                for (int i = 0; i < basePixels.Length; i++)
                                {
                                    var uiColor = uiPixels[i];
                                    if (uiColor.a > 0.01f)
                                    {
                                        basePixels[i] = Color.Lerp(basePixels[i], uiColor, uiColor.a);
                                    }
                                }
                                finalTexture.SetPixels(basePixels);
                                finalTexture.Apply();

                                Object.Destroy(uiTexture);
                            }
                            finally
                            {
                                canvas.renderMode = originalRenderMode;
                                canvas.worldCamera = originalWorldCamera;
                                canvas.planeDistance = originalPlaneDistance;
                            }
                        }
                    }
                    finally
                    {
                        Object.Destroy(uiCameraObj);
                        Object.Destroy(uiRt);
                    }
                }

                File.WriteAllBytes(path, finalTexture.EncodeToPNG());
            }
            finally
            {
                if (finalTexture != null)
                {
                    Object.Destroy(finalTexture);
                }
                camera.targetTexture = null;
                camera.clearFlags = originalClearFlags;
                camera.backgroundColor = originalBackground;
                RenderTexture.active = null;
                if (rt != null)
                {
                    Object.Destroy(rt);
                }
            }
        }

        private static void AssertNoUnexpectedLogs(string phase)
        {
            var report = PlayModeGlobalLogMonitor.BuildFailureReport();
            if (!string.IsNullOrEmpty(report))
            {
                Assert.Fail(phase + ": unexpected Error/Exception logs:\n" + report);
            }
        }
    }
}
