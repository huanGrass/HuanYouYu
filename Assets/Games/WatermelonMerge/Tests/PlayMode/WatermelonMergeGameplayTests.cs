using System;
using System.Collections;
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
    public sealed class WatermelonMergeGameplayTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        [Test]
        public void WatermelonMergeTextResourceExists()
        {
            Assert.IsNotNull(Resources.Load<TextAsset>("Text/watermelon-merge.ui_texts.zh-CN"), "WatermelonMerge text catalog should exist.");
        }

        [UnityTest]
        public IEnumerator CanEnterAndDropFruit()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WatermelonMergeGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            var runtime = GetActiveGame(controller);
            Assert.IsTrue(controller.HasActiveGame, "WatermelonMerge should become the active game.");
            Assert.IsNotNull(GameObject.Find("WatermelonMergeView"), "WatermelonMerge shell root should exist.");
            Assert.IsNotNull(GameObject.Find("WatermelonMergeBoard"), "WatermelonMerge board should exist.");
            Assert.IsNotNull(GameObject.Find("NextFruitPreview"), "Next fruit preview should exist.");
            Assert.AreEqual(0, GetFruits(runtime).Count, "New game should start without dropped fruits.");

            InvokeBoardClick(runtime, Vector2.zero);
            yield return null;

            Assert.AreEqual(1, GetFruits(runtime).Count, "Clicking the board should drop one fruit.");
        }

        [UnityTest]
        public IEnumerator MatchingFruitsMergeAndAwardCoins()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WatermelonMergeGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            var runtime = GetActiveGame(controller);
            SpawnFruitForTest(runtime, -8f, -120f, 0);
            SpawnFruitForTest(runtime, 8f, -120f, 0);

            runtime.Tick(0.016f);
            yield return null;

            Assert.AreEqual(1, GetFruits(runtime).Count, "Two same level fruits should merge into one fruit.");
            Assert.AreEqual(4, GetIntField(runtime, "score"), "Level 1 merge should add score.");
            Assert.AreEqual(4, GetIntField(runtime, "coinCount"), "Coins should follow merge score.");
            Assert.AreEqual(0, GetIntField(runtime, "chestCount"), "Low level merge should not grant chest.");
        }

        [UnityTest]
        public IEnumerator MergingTopFruitAwardsChest()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WatermelonMergeGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            var runtime = GetActiveGame(controller);
            SpawnFruitForTest(runtime, -12f, -80f, 9);
            SpawnFruitForTest(runtime, 12f, -80f, 9);

            runtime.Tick(0.016f);
            yield return null;

            var fruits = GetFruits(runtime);
            Assert.AreEqual(1, fruits.Count, "Two level 7 fruits should merge into one top fruit.");
            Assert.AreEqual(10, GetIntField(fruits[0], "Level"), "Merged top fruit should use the final watermelon level.");
            Assert.AreEqual("watermelon", GetFruitTextureName(fruits[0]), "Final top fruit should render as watermelon.");
            Assert.AreEqual(2048, GetIntField(runtime, "score"), "Top fruit merge should award top score.");
            Assert.AreEqual(2048, GetIntField(runtime, "coinCount"), "Top fruit score should become coins.");
            Assert.AreEqual(1, GetIntField(runtime, "chestCount"), "Top fruit merge should grant one chest.");
        }

        [UnityTest]
        public IEnumerator PreWatermelonAndFinalWatermelonUseDifferentIcons()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WatermelonMergeGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            var runtime = GetActiveGame(controller);
            var preWatermelon = SpawnFruitForTest(runtime, -12f, -80f, 9);
            var finalWatermelon = SpawnFruitForTest(runtime, 120f, -80f, 10);

            Assert.AreNotEqual(
                GetFruitTextureName(preWatermelon),
                GetFruitTextureName(finalWatermelon),
                "The pre-watermelon level should not look identical to the final watermelon.");
            Assert.AreEqual("watermelon", GetFruitTextureName(finalWatermelon));
        }

        [UnityTest]
        public IEnumerator FruitProgressionUsesElevenLevels()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WatermelonMergeGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            var runtime = GetActiveGame(controller);
            for (var level = 0; level <= 10; level++)
            {
                var fruit = SpawnFruitForTest(runtime, -220f + (level * 44f), -80f, level);
                Assert.AreEqual(level, GetIntField(fruit, "Level"), "Fruit level should be creatable: " + level);
                Assert.IsNotNull(GetFruitTextureName(fruit), "Fruit level should have an icon: " + level);
            }

            Assert.AreEqual(11, GetFruits(runtime).Count, "WatermelonMerge should expose eleven fruit levels.");
        }

        [UnityTest]
        public IEnumerator PauseExitSettlesCurrentRewards()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WatermelonMergeGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            var runtime = GetActiveGame(controller);
            SpawnFruitForTest(runtime, -8f, -120f, 0);
            SpawnFruitForTest(runtime, 8f, -120f, 0);
            runtime.Tick(0.016f);
            yield return null;

            var gameRoot = GameObject.Find("WatermelonMergeView");
            var pauseButton = gameRoot.transform.Find("PauseButton")?.GetComponent<Button>();
            Assert.IsNotNull(pauseButton, "Pause button should exist.");
            pauseButton.onClick.Invoke();
            yield return null;

            var pausePopup = gameRoot.transform.Find("PopupHost/MiniGamePausePopup");
            Assert.IsNotNull(pausePopup, "Pause popup should be visible.");

            var exitButton = pausePopup.Find("Dialog/MainButtons/ExitButton")?.GetComponent<Button>();
            Assert.IsNotNull(exitButton, "Exit button should exist.");
            exitButton.onClick.Invoke();
            yield return null;

            var settlementPopup = gameRoot.transform.Find("PopupHost/WatermelonMergeSettlementPanel");
            Assert.IsNotNull(settlementPopup, "Pause exit should show settlement popup.");

            var confirmButton = settlementPopup.Find("Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(confirmButton, "Settlement confirm button should exist.");
            confirmButton.onClick.Invoke();
            yield return null;

            var progress = controller.GetProgress(WatermelonMergeGameView.GameIdConstant);
            Assert.AreEqual(1, progress.PlayCount);
            Assert.AreEqual(4, progress.BestScore);
            Assert.AreEqual(4, progress.TotalCoinCount);
            Assert.AreEqual(0, progress.TotalChestCount);
            Assert.IsTrue(controller.IsHallVisible, "Hall should be visible after settlement.");
        }

        [UnityTest]
        public IEnumerator OverflowShowsSettlement()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WatermelonMergeGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            var runtime = GetActiveGame(controller);
            var dangerLineY = GetFloatField(runtime, "dangerLineY");
            var fruit = SpawnFruitForTest(runtime, 0f, dangerLineY + 10f, 0);
            SetVector2Field(fruit, "Velocity", Vector2.zero);

            for (var i = 0; i < 100; i++)
            {
                InvokeCheckOverflow(runtime, 0.02f);
            }

            yield return null;

            var gameRoot = GameObject.Find("WatermelonMergeView");
            Assert.IsNotNull(gameRoot.transform.Find("PopupHost/WatermelonMergeSettlementPanel"), "Overflow should show settlement popup.");
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

        private static WatermelonMergeGameView GetActiveGame(MiniGameAppController controller)
        {
            var field = typeof(MiniGameAppController).GetField("activeGame", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access activeGame field.");
            var runtime = field.GetValue(controller) as WatermelonMergeGameView;
            Assert.IsNotNull(runtime, "WatermelonMerge runtime was not created.");
            return runtime;
        }

        private static IList GetFruits(WatermelonMergeGameView runtime)
        {
            var field = typeof(WatermelonMergeGameView).GetField("fruits", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access fruits field.");
            var fruits = field.GetValue(runtime) as IList;
            Assert.IsNotNull(fruits, "Fruits list should exist.");
            return fruits;
        }

        private static void InvokeBoardClick(WatermelonMergeGameView runtime, Vector2 localPosition)
        {
            var method = typeof(WatermelonMergeGameView).GetMethod("OnBoardClicked", InstancePrivate);
            Assert.IsNotNull(method, "OnBoardClicked should exist.");
            method.Invoke(runtime, new object[] { localPosition });
        }

        private static object SpawnFruitForTest(WatermelonMergeGameView runtime, float x, float y, int level)
        {
            var method = typeof(WatermelonMergeGameView).GetMethod("SpawnFruit", InstancePrivate);
            Assert.IsNotNull(method, "SpawnFruit should exist.");
            var fruit = method.Invoke(runtime, new object[] { x, level });
            Assert.IsNotNull(fruit, "SpawnFruit should return a fruit node.");
            SetVector2Field(fruit, "Position", new Vector2(x, y));
            SetVector2Field(fruit, "Velocity", Vector2.zero);
            return fruit;
        }

        private static int GetIntField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, "Failed to access field: " + fieldName);
            return (int)field.GetValue(target);
        }

        private static float GetFloatField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, "Failed to access field: " + fieldName);
            return (float)field.GetValue(target);
        }

        private static void SetVector2Field(object target, string fieldName, Vector2 value)
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, "Failed to access field: " + fieldName);
            field.SetValue(target, value);
        }

        private static string GetFruitTextureName(object fruit)
        {
            var image = GetField<Image>(fruit, "Icon");
            Assert.IsNotNull(image, "Fruit icon image should exist.");
            Assert.IsNotNull(image.sprite, "Fruit icon sprite should exist.");
            Assert.IsNotNull(image.sprite.texture, "Fruit icon texture should exist.");
            return image.sprite.texture.name;
        }

        private static T GetField<T>(object target, string fieldName)
            where T : class
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, "Failed to access field: " + fieldName);
            return field.GetValue(target) as T;
        }

        private static void InvokeCheckOverflow(WatermelonMergeGameView runtime, float deltaTime)
        {
            var method = typeof(WatermelonMergeGameView).GetMethod("CheckOverflow", InstancePrivate);
            Assert.IsNotNull(method, "CheckOverflow should exist.");
            method.Invoke(runtime, new object[] { deltaTime });
        }

        private static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.DeleteKey(MiniGameRuntimeSettings.PlayerPrefsKey);
            PlayerPrefs.Save();
        }
    }
}
