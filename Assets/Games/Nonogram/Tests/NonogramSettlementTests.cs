using System.Collections;
using System.Reflection;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests
{
    public sealed class NonogramSettlementTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator ExitSettlementReturnsFixedCoinsAndNoChests()
        {
            var root = CreateGameRoot();
            var host = root.GetComponent<TestHostBehaviour>();
            var canvas = root.transform.Find("MiniGameCanvas");
            var game = new NonogramGameView(host, canvas, _ => { }, () => { });
            yield return null;

            var buildMethod = typeof(NonogramGameView).GetMethod("BuildExitSettlement", InstancePrivate);
            Assert.IsNotNull(buildMethod, "BuildExitSettlement method should exist.");
            var settlement = buildMethod.Invoke(game, null) as MiniGameSettlement;

            Assert.IsNotNull(settlement, "Exit settlement should be created.");
            Assert.AreEqual(20, settlement.CoinCount);
            Assert.AreEqual(0, settlement.ChestCount);
            StringAssert.Contains("20", settlement.Summary);

            game.Dispose();
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExitAfterAdvancingFromSolvedPuzzleKeepsSolvedChestReward()
        {
            var root = CreateGameRoot();
            var host = root.GetComponent<TestHostBehaviour>();
            var canvas = root.transform.Find("MiniGameCanvas");
            var game = new NonogramGameView(host, canvas, _ => { }, () => { });
            yield return null;

            var accumulateMethod = typeof(NonogramGameView).GetMethod("AccumulateSolvedSettlement", InstancePrivate);
            var buildMethod = typeof(NonogramGameView).GetMethod("BuildSessionSettlementForExit", InstancePrivate);
            Assert.IsNotNull(accumulateMethod, "AccumulateSolvedSettlement method should exist.");
            Assert.IsNotNull(buildMethod, "BuildSessionSettlementForExit method should exist.");

            accumulateMethod.Invoke(game, null);
            var settlement = buildMethod.Invoke(game, null) as MiniGameSettlement;

            Assert.IsNotNull(settlement, "Session exit settlement should be created.");
            Assert.AreEqual(80, settlement.CoinCount);
            Assert.AreEqual(1, settlement.ChestCount);
            Assert.IsFalse(string.IsNullOrWhiteSpace(settlement.Summary));

            game.Dispose();
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChangingPuzzleDoesNotCountAsSolvedReward()
        {
            var root = CreateGameRoot();
            var host = root.GetComponent<TestHostBehaviour>();
            var canvas = root.transform.Find("MiniGameCanvas");
            var game = new NonogramGameView(host, canvas, _ => { }, () => { });
            yield return null;

            var advanceMethod = typeof(NonogramGameView).GetMethod("AdvancePuzzle", InstancePrivate);
            var buildMethod = typeof(NonogramGameView).GetMethod("BuildSessionSettlementForExit", InstancePrivate);
            Assert.IsNotNull(advanceMethod, "AdvancePuzzle method should exist.");
            Assert.IsNotNull(buildMethod, "BuildSessionSettlementForExit method should exist.");

            advanceMethod.Invoke(game, null);
            var settlement = buildMethod.Invoke(game, null) as MiniGameSettlement;

            Assert.IsNotNull(settlement, "Session exit settlement should be created.");
            Assert.AreEqual(20, settlement.CoinCount);
            Assert.AreEqual(0, settlement.ChestCount);

            game.Dispose();
            Object.Destroy(root);
            yield return null;
        }

        private static GameObject CreateGameRoot()
        {
            var root = new GameObject("NonogramTestRoot");
            var canvasObject = new GameObject("MiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(root.transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(750f, 1334f);

            if (Object.FindObjectOfType<AudioListener>() == null)
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.transform.SetParent(root.transform, false);
            }

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            root.AddComponent<MiniGameSfxPlayer>();
            root.AddComponent<TestHostBehaviour>();
            return root;
        }

        private sealed class TestHostBehaviour : MonoBehaviour
        {
        }
    }
}
