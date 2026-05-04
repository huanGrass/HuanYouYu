using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests
{
    public sealed class BullsCowsGameplayTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        [Test]
        public void BullsCowsTextResourceExists()
        {
            Assert.IsNotNull(Resources.Load<TextAsset>("Text/bulls-cows.ui_texts.zh-CN"), "BullsCows text catalog should exist.");
        }

        [Test]
        public void BullsCowsRulesEvaluateBullsAndCows()
        {
            Assert.IsTrue(BullsCowsGameView.IsValidGuess("0123"));
            Assert.IsFalse(BullsCowsGameView.IsValidGuess("0012"));
            BullsCowsGameView.EvaluateGuess("1234", "1243", out var bulls, out var cows);
            Assert.AreEqual(2, bulls);
            Assert.AreEqual(2, cows);
            BullsCowsGameView.EvaluateGuess("5678", "1234", out bulls, out cows);
            Assert.AreEqual(0, bulls);
            Assert.AreEqual(0, cows);
        }

        [UnityTest]
        public IEnumerator CanEnterInputAndReceiveFeedback()
        {
            ResetProgress();
            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(BullsCowsGameView.GameIdConstant);
            yield return null;
            SetAnswer(controller, "1234");

            Assert.IsTrue(controller.HasActiveGame, "BullsCows should become active.");
            Assert.IsNotNull(GameObject.Find("BullsCowsView"), "BullsCows root should exist.");
            Assert.AreEqual(10, CountButtonsWithPrefix("DigitButton_"), "BullsCows should render ten digit buttons.");
            Assert.IsNull(FindButton("SubmitButton"), "BullsCows should auto submit without a submit button.");
            Assert.IsNull(GameObject.Find("StatusLabel"), "BullsCows should not render a status label.");

            ClickButton("DigitButton_1");
            ClickButton("DigitButton_2");
            ClickButton("DigitButton_4");
            ClickButton("DigitButton_3");
            yield return null;

            var row = GameObject.Find("HistoryRow_0").GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(row, "History row should have TMP text.");
            var textProperty = row.GetType().GetProperty("text", InstancePrivate);
            Assert.IsNotNull(textProperty, "TMP text should expose text property.");
            var historyText = textProperty.GetValue(row) as string;
            Assert.IsTrue(historyText.Contains("位置正确 2 个"), "History should describe exact matches in Chinese.");
            Assert.IsTrue(historyText.Contains("数字正确但位置不对 2 个"), "History should describe misplaced matches in Chinese.");
        }

        [UnityTest]
        public IEnumerator WinningGuessShowsSettlement()
        {
            ResetProgress();
            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(BullsCowsGameView.GameIdConstant);
            yield return null;
            SetAnswer(controller, "1234");

            ClickButton("DigitButton_1");
            ClickButton("DigitButton_2");
            ClickButton("DigitButton_3");
            ClickButton("DigitButton_4");
            yield return null;

            Assert.IsNotNull(GameObject.Find("BullsCowsSettlementPanel"), "Correct answer should show settlement.");
        }

        [UnityTest]
        public IEnumerator CanContinueAfterRewardTargetAttemptsAndKeepsScrollableHistory()
        {
            ResetProgress();
            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(BullsCowsGameView.GameIdConstant);
            yield return null;
            SetAnswer(controller, "9876");

            var wrongGuesses = new[]
            {
                "0123",
                "0124",
                "0125",
                "0134",
                "0135",
                "0145",
                "0234",
                "0235",
                "0245"
            };

            for (var i = 0; i < wrongGuesses.Length; i++)
            {
                SubmitGuess(wrongGuesses[i]);
                yield return null;
            }

            Assert.IsNull(GameObject.Find("BullsCowsSettlementPanel"), "Running past the reward target should not fail the game.");
            Assert.IsNotNull(GameObject.Find("HistoryRow_8"), "Scrollable history should keep entries beyond the reward target.");
            Assert.IsTrue(FindButton("DigitButton_0").interactable, "Digits should remain interactable after the reward target.");
        }

        [UnityTest]
        public IEnumerator PauseButtonOpensPausePopup()
        {
            ResetProgress();
            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(BullsCowsGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();

            var pauseButton = FindButton("PauseButton");
            Assert.IsNotNull(pauseButton, "Pause button should exist in BullsCows.");
            Assert.IsTrue(pauseButton.interactable, "Pause button should be interactable in BullsCows.");
            pauseButton.onClick.Invoke();
            yield return null;

            Assert.IsNotNull(GameObject.Find("MiniGamePausePopup"), "Pause button should open the pause popup in BullsCows.");
        }

        [UnityTest]
        public IEnumerator BullsCowsScreenshotHasNonBlankPlayableLayout()
        {
            ResetProgress();
            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(BullsCowsGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();

            AssertChildrenStayInside("BullsCowsContent", "GuessSlot_", 4);
            AssertChildrenStayInside("BullsCowsControls", "DigitButton_", 10);
            AssertChildStaysInside("BullsCowsControls", "Keypad");
            AssertChildStaysInside("BullsCowsControls", "BullsCowsActionRow");
            AssertChildSizeAtLeast("GuessSlot_0", 120f, 120f);
            AssertChildSizeAtLeast("DigitButton_0", 145f, 58f);
            var screenshotPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "PlayModeShots", "pm_bulls_cows.bmp");
            CaptureDiagnosticScreenshot(screenshotPath);
            Assert.IsTrue(File.Exists(screenshotPath), "BullsCows diagnostic screenshot should be generated.");
            Assert.Greater(new FileInfo(screenshotPath).Length, 1024, "BullsCows diagnostic screenshot should contain image data.");
        }

        private static IEnumerator LoadController(Action<MiniGameAppController> onLoaded)
        {
            var load = SceneManager.LoadSceneAsync("SampleScene");
            while (!load.isDone)
            {
                yield return null;
            }

            MiniGameAppController controller = null;
            for (var i = 0; i < 60; i++)
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

        private static void SetAnswer(MiniGameAppController controller, string answer)
        {
            var activeGameField = typeof(MiniGameAppController).GetField("activeGame", InstancePrivate);
            Assert.IsNotNull(activeGameField, "activeGame field should be accessible.");
            var runtime = activeGameField.GetValue(controller) as BullsCowsGameView;
            Assert.IsNotNull(runtime, "BullsCows runtime should be active.");
            var answerField = typeof(BullsCowsGameView).GetField("answer", InstancePrivate);
            Assert.IsNotNull(answerField, "answer field should be accessible for deterministic tests.");
            answerField.SetValue(runtime, answer);
        }

        private static void CaptureDiagnosticScreenshot(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            const int width = 375;
            const int height = 667;
            var pixels = new Color32[width * height];
            Fill(pixels, new Color32(18, 28, 40, 255));
            DrawRect(pixels, width, height, GameObject.Find("BullsCowsContent")?.GetComponent<RectTransform>(), new Color32(214, 232, 214, 255));
            DrawRect(pixels, width, height, GameObject.Find("BullsCowsControls")?.GetComponent<RectTransform>(), new Color32(204, 221, 244, 255));
            DrawRect(pixels, width, height, GameObject.Find("GuessRow")?.GetComponent<RectTransform>(), new Color32(255, 244, 174, 255));
            DrawRect(pixels, width, height, GameObject.Find("BullsCowsHistory")?.GetComponent<RectTransform>(), new Color32(134, 180, 194, 255));
            DrawRect(pixels, width, height, GameObject.Find("Keypad")?.GetComponent<RectTransform>(), new Color32(114, 154, 202, 255));
            AssertPixelsLookNonBlank(pixels);
            File.WriteAllBytes(path, EncodeBmp(pixels, width, height));
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

        private static void AssertChildrenStayInside(string parentName, string childPrefix, int count)
        {
            var parent = GameObject.Find(parentName)?.GetComponent<RectTransform>();
            Assert.IsNotNull(parent, "Missing parent rect: " + parentName);
            var parentRect = ToScreenRect(parent);
            for (var i = 0; i < count; i++)
            {
                var child = GameObject.Find(childPrefix + i)?.GetComponent<RectTransform>();
                Assert.IsNotNull(child, "Missing child rect: " + childPrefix + i);
                var childRect = ToScreenRect(child);
                Assert.GreaterOrEqual(childRect.xMin, parentRect.xMin - 1f, childPrefix + i + " should stay inside content horizontally.");
                Assert.LessOrEqual(childRect.xMax, parentRect.xMax + 1f, childPrefix + i + " should stay inside content horizontally.");
                Assert.GreaterOrEqual(childRect.yMin, parentRect.yMin - 1f, childPrefix + i + " should stay inside content vertically.");
                Assert.LessOrEqual(childRect.yMax, parentRect.yMax + 1f, childPrefix + i + " should stay inside content vertically.");
            }
        }

        private static void AssertChildSizeAtLeast(string childName, float minWidth, float minHeight)
        {
            var child = GameObject.Find(childName)?.GetComponent<RectTransform>();
            Assert.IsNotNull(child, "Missing child rect: " + childName);
            Assert.GreaterOrEqual(child.rect.width, minWidth, childName + " should be visually large enough.");
            Assert.GreaterOrEqual(child.rect.height, minHeight, childName + " should be visually large enough.");
        }

        private static Rect ToScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static byte[] EncodeBmp(Color32[] pixels, int width, int height)
        {
            var rowStride = ((width * 3) + 3) & ~3;
            var pixelDataSize = rowStride * height;
            var fileSize = 54 + pixelDataSize;
            var bytes = new byte[fileSize];
            bytes[0] = (byte)'B';
            bytes[1] = (byte)'M';
            WriteInt(bytes, 2, fileSize);
            WriteInt(bytes, 10, 54);
            WriteInt(bytes, 14, 40);
            WriteInt(bytes, 18, width);
            WriteInt(bytes, 22, height);
            bytes[26] = 1;
            bytes[28] = 24;
            WriteInt(bytes, 34, pixelDataSize);

            for (var y = 0; y < height; y++)
            {
                var rowOffset = 54 + y * rowStride;
                for (var x = 0; x < width; x++)
                {
                    var pixel = pixels[y * width + x];
                    var offset = rowOffset + x * 3;
                    bytes[offset] = pixel.b;
                    bytes[offset + 1] = pixel.g;
                    bytes[offset + 2] = pixel.r;
                }
            }

            return bytes;
        }

        private static void WriteInt(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)(value & 0xff);
            bytes[offset + 1] = (byte)((value >> 8) & 0xff);
            bytes[offset + 2] = (byte)((value >> 16) & 0xff);
            bytes[offset + 3] = (byte)((value >> 24) & 0xff);
        }

        private static void Fill(Color32[] pixels, Color32 color)
        {
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
        }

        private static void DrawRect(Color32[] pixels, int width, int height, RectTransform rect, Color32 color)
        {
            Assert.IsNotNull(rect, "Diagnostic screenshot target rect should exist.");
            var screenRect = ToScreenRect(rect);
            var sourceWidth = Mathf.Max(1f, Screen.width);
            var sourceHeight = Mathf.Max(1f, Screen.height);
            var minX = Mathf.Clamp(Mathf.FloorToInt(screenRect.xMin / sourceWidth * width), 0, width - 1);
            var maxX = Mathf.Clamp(Mathf.CeilToInt(screenRect.xMax / sourceWidth * width), 0, width - 1);
            var minY = Mathf.Clamp(Mathf.FloorToInt(screenRect.yMin / sourceHeight * height), 0, height - 1);
            var maxY = Mathf.Clamp(Mathf.CeilToInt(screenRect.yMax / sourceHeight * height), 0, height - 1);
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    pixels[y * width + x] = color;
                }
            }
        }

        private static void AssertPixelsLookNonBlank(Color32[] pixels)
        {
            var sampleStep = Mathf.Max(1, pixels.Length / 1200);
            var colorSpread = 0;
            var first = pixels[0];
            for (var i = 0; i < pixels.Length; i += sampleStep)
            {
                var pixel = pixels[i];
                if (Mathf.Abs(pixel.r - first.r) + Mathf.Abs(pixel.g - first.g) + Mathf.Abs(pixel.b - first.b) > 40)
                {
                    colorSpread++;
                }
            }

            Assert.Greater(colorSpread, 40, "Diagnostic screenshot should not be flat.");
        }

        private static int CountButtonsWithPrefix(string prefix)
        {
            var count = 0;
            var buttons = Object.FindObjectsOfType<Button>();
            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static void ClickButton(string buttonName)
        {
            var button = FindButton(buttonName);
            Assert.IsNotNull(button, "Could not find button: " + buttonName);
            Assert.IsTrue(button.interactable, "Button should be interactable before click: " + buttonName);
            button.onClick.Invoke();
        }

        private static void SubmitGuess(string guess)
        {
            for (var i = 0; i < guess.Length; i++)
            {
                ClickButton("DigitButton_" + guess[i]);
            }
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

        private static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.DeleteKey(MiniGameRuntimeSettings.PlayerPrefsKey);
            PlayerPrefs.Save();
        }
    }
}
