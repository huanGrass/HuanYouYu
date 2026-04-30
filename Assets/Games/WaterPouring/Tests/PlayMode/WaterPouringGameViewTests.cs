using System.Collections;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests
{
    public sealed class WaterPouringGameViewTests
    {
        private GameObject hostObject;
        private Canvas canvas;
        private MiniGameWaterPouringGameView view;
        private MiniGameAppController controller;
        private MiniGameSettlement completedSettlement;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (view != null)
            {
                view.Dispose();
                view = null;
            }

            if (hostObject != null)
            {
                Object.Destroy(hostObject);
                hostObject = null;
            }

            controller = null;
            completedSettlement = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator CreatesPlayableCupUiWithValidInitialState()
        {
            CreateView();
            yield return null;

            Assert.AreEqual(10, MiniGameWaterPouringGameView.LevelCount);
            Assert.IsNotNull(Find("WaterPouringContent"));
            Assert.IsNotNull(Find("TargetLabel"));
            Assert.IsNotNull(Find("MovesLabel"));
            Assert.IsNotNull(Button("RestartButton"));
            Assert.IsNotNull(Button("LevelSelectButton"));
            Assert.IsNotNull(Button("FillButton"));
            Assert.IsNotNull(Button("EmptyButton"));
            Assert.AreEqual(2, Find("CupsHost").transform.childCount);
            Assert.IsTrue(TextValue("TargetLabel").Contains("4"));
            Assert.AreEqual("0/3 升", TextValue("Cup_0/AmountLabel"));
            Assert.AreEqual("0/5 升", TextValue("Cup_1/AmountLabel"));
            var smallCupBody = Find("Cup_0/CupBody").GetComponent<RectTransform>();
            var largeCupBody = Find("Cup_1/CupBody").GetComponent<RectTransform>();
            Assert.Less(smallCupBody.sizeDelta.y, largeCupBody.sizeDelta.y);
            Assert.AreEqual(smallCupBody.sizeDelta.x, largeCupBody.sizeDelta.x);
            Assert.IsTrue(Find("Cup_1/TargetLine").activeSelf);
            Assert.IsTrue(TextValue("MovesLabel").Contains("0"));
        }

        [UnityTest]
        public IEnumerator FillAndPourUpdateAmountsAndMoves()
        {
            CreateView();
            yield return null;

            Button("FillButton").onClick.Invoke();
            Assert.IsTrue(TextValue("MovesLabel").Contains("0"));

            Button("Cup_0").onClick.Invoke();
            Button("EmptyButton").onClick.Invoke();
            Assert.IsTrue(TextValue("MovesLabel").Contains("0"));

            Button("FillButton").onClick.Invoke();
            Button("Cup_0").onClick.Invoke();
            Button("Cup_1").onClick.Invoke();
            yield return null;

            Assert.AreEqual("0/3 升", TextValue("Cup_0/AmountLabel"));
            Assert.AreEqual("3/5 升", TextValue("Cup_1/AmountLabel"));
            Assert.IsTrue(TextValue("MovesLabel").Contains("2"));
        }

        [UnityTest]
        public IEnumerator WinningLocksInputAndCreatesSettlementWithScore()
        {
            CreateView();
            yield return null;

            Fill(1);
            Pour(1, 0);
            Empty(0);
            Pour(1, 0);
            Fill(1);
            Pour(1, 0);
            yield return null;

            Assert.AreEqual("3/3 升", TextValue("Cup_0/AmountLabel"));
            Assert.AreEqual("4/5 升", TextValue("Cup_1/AmountLabel"));
            Assert.IsNull(Find("WaterPouringWinSettlementPanel"));
            Assert.IsTrue(Find("Cup_1/SelectionFrame").GetComponent<Graphic>().color.a > 0.9f);

            yield return new WaitForSecondsRealtime(1f);

            Assert.IsNotNull(Find("WaterPouringWinSettlementPanel"));
            Assert.IsTrue(TextValue("Score").Contains("880"));
            Assert.IsFalse(Button("Cup_0").interactable);
            Assert.IsFalse(Button("FillButton").interactable);
            Assert.IsNull(completedSettlement);
        }

        [UnityTest]
        public IEnumerator RestartResetsCurrentLevelAmountsMovesAndSelection()
        {
            CreateView();
            yield return null;

            Fill(0);
            Assert.AreEqual("3/3 升", TextValue("Cup_0/AmountLabel"));
            Assert.IsTrue(TextValue("MovesLabel").Contains("1"));

            Button("RestartButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual("0/3 升", TextValue("Cup_0/AmountLabel"));
            Assert.AreEqual("0/5 升", TextValue("Cup_1/AmountLabel"));
            Assert.IsTrue(TextValue("MovesLabel").Contains("0"));
            Assert.IsFalse(Button("FillButton").interactable);
            Assert.IsTrue(TextValue("InstructionLabel").Contains("选择"));
        }

        [UnityTest]
        public IEnumerator CompletingLevelUnlocksNextLevelForHallProgressAndLevelSelect()
        {
            CreateControllerView();
            yield return null;

            CompleteFirstLevel();
            yield return new WaitForSecondsRealtime(1f);

            var progress = controller.GetProgress(MiniGameWaterPouringGameView.GameIdConstant);
            Assert.AreEqual(2, progress.UnlockedLevelCount);

            GlobalButton("LevelSelectButton").onClick.Invoke();
            yield return null;

            Assert.IsNotNull(GameObject.Find("WaterPouringLevelSelectPanel"));
            Assert.IsTrue(GlobalButton("WaterPouringLevelButton_1").interactable);
            Assert.IsTrue(GlobalButton("WaterPouringLevelButton_2").interactable);
            Assert.IsFalse(GlobalButton("WaterPouringLevelButton_3").interactable);
        }

        private void CreateView()
        {
            hostObject = new GameObject("WaterPouringTestHost", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TestHost));
            canvas = hostObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            view = new MiniGameWaterPouringGameView(
                hostObject.GetComponent<TestHost>(),
                canvas.transform,
                delegate(MiniGameSettlement settlement) { completedSettlement = settlement; },
                delegate { });
        }

        private void CreateControllerView()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();

            hostObject = new GameObject("WaterPouringControllerHost", typeof(MiniGameAppController));
            controller = hostObject.GetComponent<MiniGameAppController>();
            controller.EnterGame(MiniGameWaterPouringGameView.GameIdConstant);
        }

        private void CompleteFirstLevel()
        {
            GlobalButton("Cup_1").onClick.Invoke();
            GlobalButton("FillButton").onClick.Invoke();
            GlobalButton("Cup_1").onClick.Invoke();
            GlobalButton("Cup_0").onClick.Invoke();
            GlobalButton("Cup_0").onClick.Invoke();
            GlobalButton("EmptyButton").onClick.Invoke();
            GlobalButton("Cup_1").onClick.Invoke();
            GlobalButton("Cup_0").onClick.Invoke();
            GlobalButton("Cup_1").onClick.Invoke();
            GlobalButton("FillButton").onClick.Invoke();
            GlobalButton("Cup_1").onClick.Invoke();
            GlobalButton("Cup_0").onClick.Invoke();
        }

        private void Fill(int cupIndex)
        {
            Button("Cup_" + cupIndex).onClick.Invoke();
            Button("FillButton").onClick.Invoke();
        }

        private void Empty(int cupIndex)
        {
            Button("Cup_" + cupIndex).onClick.Invoke();
            Button("EmptyButton").onClick.Invoke();
        }

        private void Pour(int sourceIndex, int targetIndex)
        {
            Button("Cup_" + sourceIndex).onClick.Invoke();
            Button("Cup_" + targetIndex).onClick.Invoke();
        }

        private GameObject Find(string path)
        {
            var root = canvas.transform.Find("MiniGameWaterPouringView");
            Assert.IsNotNull(root, "Game root should exist.");
            var target = FindRecursive(root, path.Split('/'), 0);
            return target != null ? target.gameObject : null;
        }

        private static Transform FindRecursive(Transform root, string[] names, int nameIndex)
        {
            if (root == null || names == null || nameIndex >= names.Length)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == names[nameIndex])
                {
                    if (nameIndex == names.Length - 1)
                    {
                        return child;
                    }

                    var nested = FindRecursive(child, names, nameIndex + 1);
                    if (nested != null)
                    {
                        return nested;
                    }
                }

                var descendant = FindRecursive(child, names, nameIndex);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }

        private Button Button(string path)
        {
            var target = Find(path);
            Assert.IsNotNull(target, path + " should exist.");
            var button = target.GetComponent<Button>();
            Assert.IsNotNull(button, path + " should have Button.");
            return button;
        }

        private static Button GlobalButton(string name)
        {
            var target = GameObject.Find(name);
            Assert.IsNotNull(target, name + " should exist.");
            var button = target.GetComponent<Button>();
            Assert.IsNotNull(button, name + " should have Button.");
            return button;
        }

        private string TextValue(string path)
        {
            var target = Find(path);
            Assert.IsNotNull(target, path + " should exist.");
            var text = target.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(text, path + " should have TextMeshProUGUI.");
            var property = text.GetType().GetProperty("text");
            Assert.IsNotNull(property, path + " text property should exist.");
            return (string)property.GetValue(text);
        }

        private sealed class TestHost : MonoBehaviour
        {
        }
    }
}
