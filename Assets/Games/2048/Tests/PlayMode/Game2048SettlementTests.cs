using System.Collections;
using System.Reflection;
using HuanYouYu.Game2048;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests
{
    public sealed class Game2048SettlementTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator SettlementUsesCurrentScoreAsCoinsAndMilestoneChests()
        {
            MiniGameSettlement completedSettlement = null;
            var root = CreateGameRoot();
            var host = root.GetComponent<TestHostBehaviour>();
            var canvas = root.transform.Find("MiniGameCanvas");
            var game = new Game2048View(host, canvas, settlement => completedSettlement = settlement, () => { });
            yield return null;

            var boardField = typeof(Game2048View).GetField("board", InstancePrivate);
            Assert.IsNotNull(boardField, "board field should exist.");
            var board = (Game2048Board)boardField.GetValue(game);
            Assert.IsNotNull(board, "board should be created during reset.");
            board.SetBoard(new[]
            {
                4096, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0
            }, 1234);

            var summaryField = typeof(Game2048View).GetField("pendingSettlementSummary", InstancePrivate);
            Assert.IsNotNull(summaryField, "pendingSettlementSummary field should exist.");
            summaryField.SetValue(game, "测试结算");

            var createSettlementMethod = typeof(Game2048View).GetMethod("CreateSettlement", InstancePrivate);
            Assert.IsNotNull(createSettlementMethod, "CreateSettlement method should exist.");
            completedSettlement = createSettlementMethod.Invoke(game, null) as MiniGameSettlement;
            yield return null;

            Assert.IsNotNull(completedSettlement, "Settlement should be created from the current board.");
            Assert.AreEqual(1234, completedSettlement.Score);
            Assert.AreEqual(1234, completedSettlement.CoinCount);
            Assert.AreEqual(7, completedSettlement.ChestCount);

            game.Dispose();
            Object.Destroy(root);
            yield return null;
        }

        private static GameObject CreateGameRoot()
        {
            var root = new GameObject("Game2048TestRoot");
            var canvasObject = new GameObject("MiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(root.transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

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
