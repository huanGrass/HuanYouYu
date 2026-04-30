using System;
using System.Collections;
using System.Reflection;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace Tests
{
    public class RiverCrossingGameplayTests
    {
        [UnityTest]
        public IEnumerator InitialStateHasAllItemsOnLeftBank()
        {
            var fixture = RiverCrossingFixture.Create();
            yield return null;

            Assert.AreEqual(0, fixture.GetIntField("stepCount"));
            Assert.AreEqual("Left", fixture.GetFieldString("boatSide"));
            fixture.AssertItemParent("FoxButton", "LeftBankItems");
            fixture.AssertItemParent("ChickenButton", "LeftBankItems");
            fixture.AssertItemParent("CornButton", "LeftBankItems");
            fixture.AssertItemIcon("FoxButton", "fox");
            fixture.AssertItemIcon("ChickenButton", "chicken");
            fixture.AssertItemIcon("CornButton", "corn");

            fixture.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ItemHostsUseManualIconPlacementWithoutCargoBackground()
        {
            var fixture = RiverCrossingFixture.Create();
            yield return null;

            Assert.IsNull(fixture.FindChild("LeftBankItems").GetComponent<HorizontalLayoutGroup>());
            Assert.IsNull(fixture.FindChild("RightBankItems").GetComponent<HorizontalLayoutGroup>());
            Assert.IsNull(fixture.FindChild("BoatCargoHost").GetComponent<Image>());
            fixture.AssertItemsAreClose("FoxButton", "ChickenButton");
            fixture.AssertItemsAreClose("ChickenButton", "CornButton");

            fixture.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator OptimalSolutionCompletesInSevenCrossings()
        {
            var fixture = RiverCrossingFixture.Create();
            yield return null;

            fixture.Click("ChickenButton");
            fixture.Click("CrossRiverButton");
            fixture.Click("ChickenButton");
            fixture.Click("CrossRiverButton");
            fixture.Click("FoxButton");
            fixture.Click("CrossRiverButton");
            fixture.Click("FoxButton");
            fixture.Click("ChickenButton");
            fixture.Click("CrossRiverButton");
            fixture.Click("ChickenButton");
            fixture.Click("CornButton");
            fixture.Click("CrossRiverButton");
            fixture.Click("CornButton");
            fixture.Click("CrossRiverButton");
            fixture.Click("ChickenButton");
            fixture.Click("CrossRiverButton");
            fixture.Click("ChickenButton");
            yield return null;

            Assert.AreEqual(7, fixture.GetIntField("stepCount"));
            fixture.AssertItemParent("FoxButton", "RightBankItems");
            fixture.AssertItemParent("ChickenButton", "RightBankItems");
            fixture.AssertItemParent("CornButton", "RightBankItems");
            Assert.IsNotNull(fixture.FindChild("RiverCrossingWinSettlementPanel"));

            fixture.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator BoatVisualMovesBetweenBanksWhenCrossing()
        {
            var fixture = RiverCrossingFixture.Create();
            yield return null;

            var boatRect = fixture.FindChild("BoatButton").GetComponent<RectTransform>();
            var sailingArea = fixture.FindChild("RiverSailingArea").GetComponent<RectTransform>();
            Assert.AreEqual("RiverPanel", sailingArea.transform.parent.gameObject.name);

            var startY = boatRect.anchoredPosition.y;
            var expectedDockY = Mathf.Max(56f, (sailingArea.rect.height - boatRect.rect.height) * 0.5f);

            fixture.Click("ChickenButton");
            fixture.Click("CrossRiverButton");
            yield return new WaitForSecondsRealtime(0.4f);

            Assert.Greater(boatRect.anchoredPosition.y, startY + 20f, "Boat visual should move from lower bank toward upper bank after crossing.");
            Assert.AreEqual(expectedDockY + 40f, boatRect.anchoredPosition.y, 0.5f, "Boat visual should dock 40 pixels above the full river panel's upper dock point.");

            fixture.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DraggingItemOntoBoatLoadsCargo()
        {
            var fixture = RiverCrossingFixture.Create();
            yield return null;

            fixture.DragTo("ChickenButton", "RiverTitle");
            yield return null;

            Assert.AreEqual("Chicken", fixture.GetFieldString("boatCargo"));
            fixture.AssertItemParent("ChickenButton", "BoatCargoHost");
            fixture.AssertItemAnchoredX("FoxButton", -72f);
            fixture.AssertItemAnchoredX("CornButton", 72f);

            fixture.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PointerClickAfterDragDoesNotUndoFirstDrop()
        {
            var fixture = RiverCrossingFixture.Create();
            yield return null;

            fixture.DragTo("ChickenButton", "RiverTitle");
            fixture.Click("ChickenButton");
            yield return null;

            Assert.AreEqual("Chicken", fixture.GetFieldString("boatCargo"));
            fixture.AssertItemParent("ChickenButton", "BoatCargoHost");

            fixture.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DraggingBoatCargoOntoCurrentBankUnloadsCargo()
        {
            var fixture = RiverCrossingFixture.Create();
            yield return null;

            fixture.DragTo("ChickenButton", "RiverTitle");
            yield return null;
            Assert.AreEqual("Chicken", fixture.GetFieldString("boatCargo"));

            fixture.DragTo("ChickenButton", "LeftBankTitle");
            yield return null;

            Assert.AreEqual("None", fixture.GetFieldString("boatCargo"));
            fixture.AssertItemParent("ChickenButton", "LeftBankItems");
            fixture.AssertItemAnchoredX("ChickenButton", 0f);

            fixture.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DraggingUsesOriginalIconWithoutPreviewGhost()
        {
            var fixture = RiverCrossingFixture.Create();
            yield return null;

            var originalParent = fixture.FindChild("FoxButton").transform.parent;
            var originalPosition = fixture.GetAnchoredPosition("FoxButton");

            fixture.BeginDragTo("FoxButton", "BoatCargoHost");
            yield return null;

            Assert.IsNull(fixture.FindChild("RiverCrossingDragPreview"));
            Assert.AreNotEqual(originalParent, fixture.FindChild("FoxButton").transform.parent);
            Assert.AreNotEqual(originalPosition, fixture.GetAnchoredPosition("FoxButton"));

            fixture.EndDragTo("FoxButton", "LeftBankItems");
            yield return null;

            fixture.AssertItemParent("FoxButton", "LeftBankItems");
            fixture.AssertItemAnchoredX("FoxButton", -72f);

            fixture.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator LeavingFoxAndChickenAloneIsRejected()
        {
            var fixture = RiverCrossingFixture.Create();
            yield return null;

            fixture.Click("CornButton");
            fixture.Click("CrossRiverButton");
            yield return null;

            Assert.AreEqual(0, fixture.GetIntField("stepCount"));
            Assert.AreEqual("Left", fixture.GetFieldString("boatSide"));
            Assert.AreNotEqual(
                UiTextCatalog.Get("rivercrossing.status.unsafe_fox_chicken"),
                fixture.FindTextValue("StatusText"));
            Assert.IsNotNull(fixture.FindChild("RiverCrossingFailureEffectRoot"));
            Assert.IsNull(fixture.FindChild("RiverCrossingFailureSettlementPanel"));
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.IsNotNull(fixture.FindChild("RiverCrossingFailureSettlementPanel"));
            Assert.AreEqual("狐狸吃掉了鸡", fixture.FindSettlementInfoValue("SecondaryInfoRow"));

            fixture.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator LeavingChickenAndCornAloneIsRejected()
        {
            var fixture = RiverCrossingFixture.Create();
            yield return null;

            fixture.Click("FoxButton");
            fixture.Click("CrossRiverButton");
            yield return null;

            Assert.AreEqual(0, fixture.GetIntField("stepCount"));
            Assert.AreEqual("Left", fixture.GetFieldString("boatSide"));
            Assert.AreNotEqual(
                UiTextCatalog.Get("rivercrossing.status.unsafe_chicken_corn"),
                fixture.FindTextValue("StatusText"));
            Assert.IsNotNull(fixture.FindChild("RiverCrossingFailureEffectRoot"));
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.IsNotNull(fixture.FindChild("RiverCrossingFailureSettlementPanel"));
            Assert.AreEqual("鸡吃掉了玉米", fixture.FindSettlementInfoValue("SecondaryInfoRow"));

            fixture.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator RestartRestoresInitialState()
        {
            var fixture = RiverCrossingFixture.Create();
            yield return null;

            fixture.Click("ChickenButton");
            fixture.Click("CrossRiverButton");
            fixture.Click("ChickenButton");
            fixture.Click("RestartButton");
            yield return null;

            Assert.AreEqual(0, fixture.GetIntField("stepCount"));
            Assert.AreEqual("Left", fixture.GetFieldString("boatSide"));
            fixture.AssertItemParent("FoxButton", "LeftBankItems");
            fixture.AssertItemParent("ChickenButton", "LeftBankItems");
            fixture.AssertItemParent("CornButton", "LeftBankItems");
            fixture.AssertBoatDockedOnLeftBank();

            fixture.Dispose();
            yield return null;
        }

        private sealed class RiverCrossingFixture : IDisposable
        {
            private readonly GameObject hostObject;
            private readonly GameObject rootObject;
            private readonly MiniGameRiverCrossingGameView view;

            private RiverCrossingFixture(GameObject hostObject, GameObject rootObject, MiniGameRiverCrossingGameView view)
            {
                this.hostObject = hostObject;
                this.rootObject = rootObject;
                this.view = view;
            }

            public static RiverCrossingFixture Create()
            {
                var hostObject = new GameObject("RiverCrossingTestHost");
                var host = hostObject.AddComponent<TestHostBehaviour>();
                if (EventSystem.current == null)
                {
                    var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                    eventSystemObject.transform.SetParent(hostObject.transform, false);
                }

                var canvasObject = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(hostObject.transform, false);

                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(750f, 1334f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                var view = new MiniGameRiverCrossingGameView(host, canvasObject.transform, null, null);
                var rootTransform = canvasObject.transform.Find("MiniGameRiverCrossingView");
                Assert.IsNotNull(rootTransform, "Missing fixture RiverCrossing root.");
                return new RiverCrossingFixture(hostObject, rootTransform.gameObject, view);
            }

            public int GetIntField(string fieldName)
            {
                return (int)GetField(fieldName).GetValue(view);
            }

            public string GetFieldString(string fieldName)
            {
                var value = GetField(fieldName).GetValue(view);
                return value == null ? string.Empty : value.ToString();
            }

            public void Click(string buttonName)
            {
                var button = FindButton(buttonName);
                Assert.IsNotNull(button, "Missing button: " + buttonName);
                var eventData = new PointerEventData(EventSystem.current);
                ExecuteEvents.Execute(button.gameObject, eventData, ExecuteEvents.pointerClickHandler);
            }

            public void DragTo(string sourceName, string targetName)
            {
                var source = FindChild(sourceName);
                var target = FindChild(targetName);
                Assert.IsNotNull(source, "Missing drag source: " + sourceName);
                Assert.IsNotNull(target, "Missing drag target: " + targetName);

                var targetRect = target.GetComponent<RectTransform>();
                Assert.IsNotNull(targetRect, "Drag target is not a RectTransform: " + targetName);

                var eventData = new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, targetRect.position)
                };

                ExecuteEvents.Execute(source, eventData, ExecuteEvents.beginDragHandler);
                ExecuteEvents.Execute(source, eventData, ExecuteEvents.dragHandler);
                ExecuteEvents.Execute(source, eventData, ExecuteEvents.endDragHandler);
            }

            public void BeginDragTo(string sourceName, string targetName)
            {
                ExecuteDragEvent(sourceName, targetName, ExecuteEvents.beginDragHandler);
                ExecuteDragEvent(sourceName, targetName, ExecuteEvents.dragHandler);
            }

            public void EndDragTo(string sourceName, string targetName)
            {
                ExecuteDragEvent(sourceName, targetName, ExecuteEvents.endDragHandler);
            }

            public string FindTextValue(string textObjectName)
            {
                var components = rootObject.GetComponentsInChildren<Component>(true);
                for (var i = 0; i < components.Length; i++)
                {
                    var component = components[i];
                    if (component == null ||
                        component.gameObject.name != textObjectName ||
                        component.GetType().FullName != "TMPro.TextMeshProUGUI")
                    {
                        continue;
                    }

                    var property = component.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                    Assert.IsNotNull(property, "TMP text property missing: " + textObjectName);
                    return property.GetValue(component, null) as string;
                }

                Assert.Fail("Missing text: " + textObjectName);
                return null;
            }

            public string FindSettlementInfoValue(string rowName)
            {
                var row = FindChild(rowName);
                Assert.IsNotNull(row, "Missing settlement info row: " + rowName);
                var valueObject = row.transform.Find("Value");
                Assert.IsNotNull(valueObject, "Missing settlement info value: " + rowName);

                Component textComponent = null;
                var components = valueObject.GetComponents<Component>();
                for (var i = 0; i < components.Length; i++)
                {
                    if (components[i] != null && components[i].GetType().FullName == "TMPro.TextMeshProUGUI")
                    {
                        textComponent = components[i];
                        break;
                    }
                }

                Assert.IsNotNull(textComponent, "Missing settlement info text component: " + rowName);
                var property = textComponent.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                Assert.IsNotNull(property, "TMP text property missing: " + rowName);
                return property.GetValue(textComponent, null) as string;
            }

            public void AssertItemParent(string itemButtonName, string parentName)
            {
                var button = FindButton(itemButtonName);
                Assert.IsNotNull(button, "Missing item button: " + itemButtonName);
                Assert.IsNotNull(button.transform.parent, "Missing parent for: " + itemButtonName);
                Assert.AreEqual(parentName, button.transform.parent.gameObject.name);
            }

            public void AssertItemIcon(string itemButtonName, string spriteName)
            {
                var button = FindButton(itemButtonName);
                Assert.IsNotNull(button, "Missing item button: " + itemButtonName);

                var icon = button.transform.Find("Icon");
                Assert.IsNotNull(icon, "Missing icon child for: " + itemButtonName);

                var image = icon.GetComponent<Image>();
                Assert.IsNotNull(image, "Missing icon image for: " + itemButtonName);
                Assert.IsNotNull(image.sprite, "Missing icon sprite for: " + itemButtonName);
                Assert.AreEqual(spriteName, image.sprite.name);
            }

            public void AssertItemsAreClose(string leftButtonName, string rightButtonName)
            {
                var left = FindButton(leftButtonName).GetComponent<RectTransform>();
                var right = FindButton(rightButtonName).GetComponent<RectTransform>();
                Assert.Less(Mathf.Abs(right.anchoredPosition.x - left.anchoredPosition.x), 90f);
            }

            public void AssertItemAnchoredX(string buttonName, float expectedX)
            {
                var rect = FindButton(buttonName).GetComponent<RectTransform>();
                Assert.AreEqual(expectedX, rect.anchoredPosition.x, 0.5f, buttonName + " should keep its fixed bank position.");
            }

            public Vector2 GetAnchoredPosition(string childName)
            {
                var rect = FindChild(childName).GetComponent<RectTransform>();
                Assert.IsNotNull(rect, "Missing RectTransform: " + childName);
                return rect.anchoredPosition;
            }

            public void AssertBoatDockedOnLeftBank()
            {
                var boatRect = FindChild("BoatButton").GetComponent<RectTransform>();
                var sailingArea = FindChild("RiverSailingArea").GetComponent<RectTransform>();
                var expectedDockY = -Mathf.Max(56f, (sailingArea.rect.height - boatRect.rect.height) * 0.5f);
                Assert.AreEqual(expectedDockY, boatRect.anchoredPosition.y, 0.5f, "Initial boat visual should dock against the lower bank after layout is ready.");
            }

            public GameObject FindChild(string childName)
            {
                var transforms = hostObject.GetComponentsInChildren<Transform>(true);
                for (var i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i] != null && transforms[i].gameObject.name == childName)
                    {
                        return transforms[i].gameObject;
                    }
                }

                return null;
            }

            public void Dispose()
            {
                view.Dispose();
                UnityEngine.Object.Destroy(hostObject);
            }

            private FieldInfo GetField(string fieldName)
            {
                var field = typeof(MiniGameRiverCrossingGameView).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(field, "Missing field: " + fieldName);
                return field;
            }

            private Button FindButton(string buttonName)
            {
                var buttons = rootObject.GetComponentsInChildren<Button>(true);
                for (var i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i] != null && buttons[i].gameObject.name == buttonName)
                    {
                        return buttons[i];
                    }
                }

                return null;
            }

            private void ExecuteDragEvent<T>(string sourceName, string targetName, ExecuteEvents.EventFunction<T> eventFunction)
                where T : IEventSystemHandler
            {
                var source = FindChild(sourceName);
                var target = FindChild(targetName);
                Assert.IsNotNull(source, "Missing drag source: " + sourceName);
                Assert.IsNotNull(target, "Missing drag target: " + targetName);

                var targetRect = target.GetComponent<RectTransform>();
                Assert.IsNotNull(targetRect, "Drag target is not a RectTransform: " + targetName);

                var eventData = new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, targetRect.position)
                };

                ExecuteEvents.Execute(source, eventData, eventFunction);
            }
        }

        private sealed class TestHostBehaviour : MonoBehaviour
        {
        }
    }
}
