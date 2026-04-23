using System.Collections;
using System;
using System.Reflection;
using NUnit.Framework;
using FarmPrototype;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using HuanYouYu.MiniGameHall;

namespace Tests
{
    public sealed class StardewAIPortraitLayoutTests
    {
        private const float PortraitHorizontalMargin = 12f;
        private const float PortraitMaxPanelWidth = 560f;
        private const float PortraitMinPanelWidth = 280f;

        [UnityTest]
        public IEnumerator EntryShowsPreviewNoticePopupAndClosesOnConfirm()
        {
            PlayModeGlobalLogMonitor.Clear();

            GameObject canvasObject = CreateCanvas();
            GameObject hostObject = new GameObject("Host", typeof(RectTransform), typeof(TestHostBehaviour));
            try
            {
                var hostBehaviour = hostObject.GetComponent<TestHostBehaviour>();
                new GameStardewAIView(
                    hostBehaviour,
                    canvasObject.transform,
                    delegate { },
                    delegate { });

                var popupRoot = FindTransform(canvasObject.transform, "GameStardewAIView/PopupHost/MiniGamePopup");
                Assert.IsNotNull(popupRoot, "Preview notice popup should appear on entry.");

                Assert.AreEqual(UiTextCatalog.Get("common.action.hint"), GetTextValue(popupRoot, "Dialog/Title"), "Preview notice title");
                Assert.AreEqual(UiTextCatalog.Get("stardewai.preview.notice"), GetTextValue(popupRoot, "Dialog/MessagePanel/Message"), "Preview notice message");
                Assert.AreEqual(UiTextCatalog.Get("common.action.got_it"), GetTextValue(popupRoot, "Dialog/Buttons/ConfirmButton/Label"), "Preview notice confirm label");
                Assert.IsFalse(popupRoot.Find("Dialog/Buttons/CancelButton").gameObject.activeSelf, "Preview notice should not show a cancel button.");
                Assert.IsFalse(popupRoot.Find("Dialog/CloseButton").gameObject.activeSelf, "Preview notice should not show a close button.");

                popupRoot.Find("Dialog/Buttons/ConfirmButton").GetComponent<Button>().onClick.Invoke();
                yield return null;

                Assert.IsNull(FindTransform(canvasObject.transform, "GameStardewAIView/PopupHost/MiniGamePopup"), "Preview notice popup should close after confirmation.");
                Assert.IsNotNull(FindHudView(), "HUD should still be initialized after closing the preview notice.");
                AssertNoUnexpectedLogs();
            }
            finally
            {
                var controller = UnityEngine.Object.FindObjectOfType<FarmPrototypeController>();
                if (controller != null)
                {
                    UnityEngine.Object.DestroyImmediate(controller.gameObject);
                }
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        [TestCase(750, 1334, 0f, 40f, 750f, 1254f)]
        [TestCase(1080, 1920, 0f, 0f, 1080f, 1920f)]
        [TestCase(390, 844, 0f, 24f, 390f, 796f)]
        [TestCase(360, 640, 0f, 0f, 360f, 640f)]
        public void PortraitLayoutKeepsPanelsAndControlsInsideParents(int screenWidth, int screenHeight, float safeX, float safeY, float safeWidth, float safeHeight)
        {
            PlayModeGlobalLogMonitor.Clear();

            GameObject canvasObject = CreateCanvas();
            GameObject hostObject = new GameObject("Host", typeof(RectTransform), typeof(TestHostBehaviour));
            try
            {
                var hostBehaviour = hostObject.GetComponent<TestHostBehaviour>();
                new GameStardewAIView(
                    hostBehaviour,
                    canvasObject.transform,
                    delegate { },
                    delegate { });

                Rect safeArea = new Rect(safeX, safeY, safeWidth, safeHeight);
                Vector2Int screenSize = new Vector2Int(screenWidth, screenHeight);
                ApplyHudLayout(FindHudView(), safeArea, screenSize);
                Canvas.ForceUpdateCanvases();

                float safeAnchorMinY = safeY / screenHeight;
                float safeAnchorMaxY = (safeY + safeHeight) / screenHeight;
                float expectedPanelWidth = CalculateExpectedPortraitPanelWidth(screenWidth, screenHeight, safeWidth);

                AssertRectApproximately(GetRectTransform(canvasObject.transform, "GameStardewAIView/TopHost/FarmTopHudRoot"), safeX / screenWidth, safeAnchorMinY, (safeX + safeWidth) / screenWidth, safeAnchorMaxY, 0.001f, "Top root safe area");
                AssertRectApproximately(GetRectTransform(canvasObject.transform, "GameStardewAIView/BottomHost/FarmBottomHudRoot"), safeX / screenWidth, safeAnchorMinY, (safeX + safeWidth) / screenWidth, safeAnchorMaxY, 0.001f, "Bottom root safe area");
                AssertRectApproximately(GetRectTransform(canvasObject.transform, "GameStardewAIView/TopHost/FarmTopHudRoot/TopPanel"), 0.5f, 1f, 0.5f, 1f, 0.001f, "Portrait top panel anchors");
                AssertRectApproximately(GetRectTransform(canvasObject.transform, "GameStardewAIView/ContentHost/StardewAIContentRoot/FarmOverlayRoot/FarmOverlayRoot/InfoButtonPanel"), 0.5f, 1f, 0.5f, 1f, 0.001f, "Portrait info band anchors");
                AssertRectApproximately(GetRectTransform(canvasObject.transform, "GameStardewAIView/ContentHost/StardewAIContentRoot/FarmOverlayRoot/FarmOverlayRoot/InventoryPanel"), 0.5f, 0.5f, 0.5f, 0.5f, 0.001f, "Portrait inventory anchors");
                AssertRectApproximately(GetRectTransform(canvasObject.transform, "GameStardewAIView/ContentHost/StardewAIContentRoot/FarmOverlayRoot/FarmOverlayRoot/MerchantShopPanel"), 0.5f, 0.5f, 0.5f, 0.5f, 0.001f, "Portrait merchant anchors");
                AssertRectApproximately(GetRectTransform(canvasObject.transform, "GameStardewAIView/BottomHost/FarmBottomHudRoot/BottomPanel"), 0.5f, 0f, 0.5f, 0f, 0.001f, "Portrait bottom band anchors");

                AssertPanelWidth(GetRectTransform(canvasObject.transform, "GameStardewAIView/TopHost/FarmTopHudRoot/TopPanel"), expectedPanelWidth, "Top panel width");
                AssertPanelWidth(GetRectTransform(canvasObject.transform, "GameStardewAIView/ContentHost/StardewAIContentRoot/FarmOverlayRoot/FarmOverlayRoot/InfoButtonPanel"), expectedPanelWidth, "Info band width");
                AssertPanelWidth(GetRectTransform(canvasObject.transform, "GameStardewAIView/ContentHost/StardewAIContentRoot/FarmOverlayRoot/FarmOverlayRoot/RightPanel"), expectedPanelWidth, "Info card width");
                AssertPanelWidth(GetRectTransform(canvasObject.transform, "GameStardewAIView/ContentHost/StardewAIContentRoot/FarmOverlayRoot/FarmOverlayRoot/InventoryPanel"), expectedPanelWidth, "Inventory width");
                AssertPanelWidth(GetRectTransform(canvasObject.transform, "GameStardewAIView/ContentHost/StardewAIContentRoot/FarmOverlayRoot/FarmOverlayRoot/MerchantShopPanel"), expectedPanelWidth, "Merchant width");
                AssertPanelWidth(GetRectTransform(canvasObject.transform, "GameStardewAIView/BottomHost/FarmBottomHudRoot/BottomPanel"), expectedPanelWidth, "Bottom panel width");

                AssertChildrenInsideParent(canvasObject.transform, "GameStardewAIView/ContentHost/StardewAIContentRoot/FarmOverlayRoot/FarmOverlayRoot/InfoButtonPanel", "InfoTabRow");
                AssertChildrenInsideParent(canvasObject.transform, "GameStardewAIView/ContentHost/StardewAIContentRoot/FarmOverlayRoot/FarmOverlayRoot/InfoButtonPanel/InfoTabRow", "InfoTabButton_0", "InfoTabButton_1", "InfoTabButton_2", "InfoTabButton_3", "InfoTabButton_4");
                AssertChildrenInsideParent(canvasObject.transform, "GameStardewAIView/BottomHost/FarmBottomHudRoot/BottomPanel", "Toolbar", "AdvanceDayButton");
                AssertChildrenInsideParent(canvasObject.transform, "GameStardewAIView/BottomHost/FarmBottomHudRoot/BottomPanel/Toolbar", "ToolButton_0", "ToolButton_1", "ToolButton_2", "ToolButton_3");
                AssertHarvestButtonAboveAdvanceDayButton(canvasObject.transform);
                AssertChildrenInsideParent(canvasObject.transform, "GameStardewAIView/ContentHost/StardewAIContentRoot/FarmOverlayRoot/FarmOverlayRoot/InventoryPanel", "BackpackGrid");
                AssertChildrenInsideParent(canvasObject.transform, "GameStardewAIView/ContentHost/StardewAIContentRoot/FarmOverlayRoot/FarmOverlayRoot/InventoryPanel/BackpackGrid", "InventorySlot_0", "InventorySlot_1", "InventorySlot_2", "InventorySlot_3", "InventorySlot_4", "InventorySlot_5", "InventorySlot_6", "InventorySlot_7");
                AssertChildrenInsideParent(canvasObject.transform, "GameStardewAIView/ContentHost/StardewAIContentRoot/FarmOverlayRoot/FarmOverlayRoot/MerchantShopPanel", "MerchantShopList", "MerchantShopPrevButton", "MerchantShopNextButton");
                AssertChildrenInsideParent(canvasObject.transform, "GameStardewAIView/ContentHost/StardewAIContentRoot/FarmOverlayRoot/FarmOverlayRoot/MerchantShopPanel/MerchantShopList", "MerchantShopItem_0", "MerchantShopItem_1", "MerchantShopItem_2", "MerchantShopItem_3", "MerchantShopItem_4", "MerchantShopItem_5", "MerchantShopItem_6", "MerchantShopItem_7", "MerchantShopItem_8", "MerchantShopItem_9");

                AssertBackpackUsesTwoRows(canvasObject.transform);
                AssertDialogueTextFixedSize(FindHudView(), 17f, 32f, 17f);
                AssertStatusTextHeight(FindHudView(), 50f);

                AssertNoUnexpectedLogs();
            }
            finally
            {
                var controller = UnityEngine.Object.FindObjectOfType<FarmPrototypeController>();
                if (controller != null)
                {
                    UnityEngine.Object.DestroyImmediate(controller.gameObject);
                }
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void AdvanceDayButtonCanAdvanceDayWithoutKeyboard()
        {
            PlayModeGlobalLogMonitor.Clear();

            GameObject canvasObject = CreateCanvas();
            GameObject hostObject = new GameObject("Host", typeof(RectTransform), typeof(TestHostBehaviour));
            try
            {
                var hostBehaviour = hostObject.GetComponent<TestHostBehaviour>();
                new GameStardewAIView(
                    hostBehaviour,
                    canvasObject.transform,
                    delegate { },
                    delegate { });

                var buttonRect = GetRectTransform(canvasObject.transform, "GameStardewAIView/BottomHost/FarmBottomHudRoot/BottomPanel/AdvanceDayButton");
                Canvas.ForceUpdateCanvases();
                Assert.GreaterOrEqual(buttonRect.sizeDelta.x, 104f, "Advance day button should be wide enough for touch.");
                Assert.AreEqual(36f, buttonRect.sizeDelta.y, 0.001f, "Advance day button should be tall enough for touch.");

                FarmPrototypeController controller = FindController();
                int beforeDay = controller.Day;
                var buttonImage = buttonRect.GetComponent<Image>();
                Color beforeColor = buttonImage.color;
                var method = typeof(FarmPrototypeController).GetMethod("TryHandleAdvanceDayButtonClick", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(method, "Advance day touch handler was not found.");

                bool handled = (bool)method.Invoke(controller, new object[] { GetRectCenterScreenPoint(buttonRect) });
                Assert.IsTrue(handled, "Advance day button should handle the click.");
                Assert.AreEqual(beforeDay + 1, controller.Day, "Advance day button should move to the next day.");
                Assert.AreNotEqual(beforeColor, buttonImage.color, "Advance day button should show visual feedback when clicked.");
                Assert.Less(buttonRect.localScale.x, 1f, "Advance day button should press down when clicked.");

                AssertNoUnexpectedLogs();
            }
            finally
            {
                var controller = UnityEngine.Object.FindObjectOfType<FarmPrototypeController>();
                if (controller != null)
                {
                    UnityEngine.Object.DestroyImmediate(controller.gameObject);
                }
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void LandscapeLayoutKeepsOriginalFootprint()
        {
            PlayModeGlobalLogMonitor.Clear();

            GameObject canvasObject = CreateCanvas();
            GameObject hostObject = new GameObject("Host", typeof(RectTransform), typeof(TestHostBehaviour));
            try
            {
                var hostBehaviour = hostObject.GetComponent<TestHostBehaviour>();
                new GameStardewAIView(
                    hostBehaviour,
                    canvasObject.transform,
                    delegate { },
                    delegate { });

                ApplyHudLayout(FindHudView(), new Rect(0f, 0f, 1280f, 720f), new Vector2Int(1280, 720));

                AssertRectApproximately(GetRectTransform(canvasObject.transform, "GameStardewAIView/TopHost/FarmTopHudRoot"), 0f, 0f, 1f, 1f, 0.001f, "Landscape top root safe area");
                AssertRectApproximately(GetRectTransform(canvasObject.transform, "GameStardewAIView/TopHost/FarmTopHudRoot/TopPanel"), 0f, 1f, 0f, 1f, 0.001f, "Landscape top panel anchors");
                AssertRectApproximately(GetRectTransform(canvasObject.transform, "GameStardewAIView/ContentHost/StardewAIContentRoot/FarmOverlayRoot/FarmOverlayRoot/InventoryPanel"), 0.5f, 0.5f, 0.5f, 0.5f, 0.001f, "Landscape inventory anchors");
                AssertRectApproximately(GetRectTransform(canvasObject.transform, "GameStardewAIView/BottomHost/FarmBottomHudRoot/BottomPanel"), 0.5f, 0f, 0.5f, 0f, 0.001f, "Landscape bottom anchors");

                AssertRectSize(GetRectTransform(canvasObject.transform, "GameStardewAIView/TopHost/FarmTopHudRoot/TopPanel"), 328f, 60f, 0.001f, "Landscape top panel size");
                AssertRectSize(GetRectTransform(canvasObject.transform, "GameStardewAIView/ContentHost/StardewAIContentRoot/FarmOverlayRoot/FarmOverlayRoot/InfoButtonPanel"), 336f, 52f, 0.001f, "Landscape info band size");
                AssertRectSize(GetRectTransform(canvasObject.transform, "GameStardewAIView/ContentHost/StardewAIContentRoot/FarmOverlayRoot/FarmOverlayRoot/InventoryPanel"), 620f, 214f, 0.001f, "Landscape inventory size");
                AssertRectSize(GetRectTransform(canvasObject.transform, "GameStardewAIView/ContentHost/StardewAIContentRoot/FarmOverlayRoot/FarmOverlayRoot/MerchantShopPanel"), 460f, 560f, 0.001f, "Landscape merchant size");
                AssertRectSize(GetRectTransform(canvasObject.transform, "GameStardewAIView/BottomHost/FarmBottomHudRoot/BottomPanel"), 478f, 108f, 0.001f, "Landscape bottom size");
                AssertHarvestButtonAboveAdvanceDayButton(canvasObject.transform);
                AssertDialogueTextFixedSize(FindHudView(), 20f, 32f, 22f);
                AssertStatusTextHeight(FindHudView(), 50f);

                AssertNoUnexpectedLogs();
            }
            finally
            {
                var controller = UnityEngine.Object.FindObjectOfType<FarmPrototypeController>();
                if (controller != null)
                {
                    UnityEngine.Object.DestroyImmediate(controller.gameObject);
                }
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        private static GameObject CreateCanvas()
        {
            GameObject canvasObject = new GameObject("MiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(750f, 1334f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvasObject;
        }

        private static Transform FindTransform(Transform root, string path)
        {
            return root.Find(path);
        }

        private static string GetTextValue(Transform root, string path)
        {
            var transform = root.Find(path);
            Assert.IsNotNull(transform, "Transform was not found: " + path);
            var component = transform.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(component, "TextMeshProUGUI was not found: " + path);
            var textProperty = component.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(textProperty, "Text property was not found: " + path);
            return textProperty.GetValue(component) as string;
        }

        private static object FindHudView()
        {
            FarmPrototypeController controller = FindController();

            var field = typeof(FarmPrototypeController).GetField("_hudView", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "FarmPrototypeController._hudView field was not found.");

            var value = field.GetValue(controller);
            Assert.IsNotNull(value, "FarmHudView was not bound.");
            return value;
        }

        private static FarmPrototypeController FindController()
        {
            var controller = UnityEngine.Object.FindObjectOfType<FarmPrototypeController>();
            Assert.IsNotNull(controller, "FarmPrototypeController was not created.");
            return controller;
        }

        private static Vector2 GetRectCenterScreenPoint(RectTransform rectTransform)
        {
            return RectTransformUtility.WorldToScreenPoint(null, rectTransform.TransformPoint(rectTransform.rect.center));
        }

        private static void ApplyHudLayout(object hudView, Rect safeArea, Vector2Int screenSize)
        {
            var method = hudView.GetType().GetMethod("ApplyLayout", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method, "FarmHudView.ApplyLayout method was not found.");
            method.Invoke(hudView, new object[] { safeArea, screenSize });
        }

        private static RectTransform GetRectTransform(Transform root, string path)
        {
            var transform = root.Find(path);
            Assert.IsNotNull(transform, "Transform was not found: " + path);
            var rectTransform = transform as RectTransform;
            Assert.IsNotNull(rectTransform, "Transform is not a RectTransform: " + path);
            return rectTransform;
        }

        private static float CalculateExpectedPortraitPanelWidth(int screenWidth, int screenHeight, float safeWidth)
        {
            float canvasScale = ResolveCanvasScaleFactor(screenWidth, screenHeight);
            float canvasSafeWidth = Mathf.Max(320f, safeWidth / canvasScale);
            float horizontalInset = Mathf.Min(PortraitHorizontalMargin, canvasSafeWidth * 0.04f);
            return Mathf.Clamp(canvasSafeWidth - (horizontalInset * 2f), PortraitMinPanelWidth, PortraitMaxPanelWidth);
        }

        private static float ResolveCanvasScaleFactor(int screenWidth, int screenHeight)
        {
            float widthScale = Mathf.Max(0.0001f, screenWidth / 750f);
            float heightScale = Mathf.Max(0.0001f, screenHeight / 1334f);
            float logWidth = Mathf.Log(widthScale, 2f);
            float logHeight = Mathf.Log(heightScale, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, 0.5f));
        }

        private static void AssertPanelWidth(RectTransform rectTransform, float expectedWidth, string message)
        {
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(expectedWidth, rectTransform.sizeDelta.x, 0.001f, message);
            Assert.LessOrEqual(rectTransform.sizeDelta.x, PortraitMaxPanelWidth, message + " max width");
        }

        private static void AssertChildrenInsideParent(Transform root, string parentPath, params string[] childNames)
        {
            var parent = GetRectTransform(root, parentPath);
            for (int i = 0; i < childNames.Length; i++)
            {
                var child = GetRectTransform(parent, childNames[i]);
                AssertRectInsideParent(parent, child, parentPath + "/" + childNames[i]);
            }
        }

        private static void AssertRectInsideParent(RectTransform parent, RectTransform child, string message)
        {
            Canvas.ForceUpdateCanvases();

            Vector3[] worldCorners = new Vector3[4];
            child.GetWorldCorners(worldCorners);

            Rect parentRect = parent.rect;
            for (int i = 0; i < worldCorners.Length; i++)
            {
                Vector3 localCorner = parent.InverseTransformPoint(worldCorners[i]);
                Assert.GreaterOrEqual(localCorner.x, parentRect.xMin - 0.5f, message + " xMin");
                Assert.LessOrEqual(localCorner.x, parentRect.xMax + 0.5f, message + " xMax");
                Assert.GreaterOrEqual(localCorner.y, parentRect.yMin - 0.5f, message + " yMin");
                Assert.LessOrEqual(localCorner.y, parentRect.yMax + 0.5f, message + " yMax");
            }
        }

        private static void AssertBackpackUsesTwoRows(Transform root)
        {
            var slot0 = GetRectTransform(root, "GameStardewAIView/ContentHost/StardewAIContentRoot/FarmOverlayRoot/FarmOverlayRoot/InventoryPanel/BackpackGrid/InventorySlot_0");
            var slot3 = GetRectTransform(root, "GameStardewAIView/ContentHost/StardewAIContentRoot/FarmOverlayRoot/FarmOverlayRoot/InventoryPanel/BackpackGrid/InventorySlot_3");
            var slot4 = GetRectTransform(root, "GameStardewAIView/ContentHost/StardewAIContentRoot/FarmOverlayRoot/FarmOverlayRoot/InventoryPanel/BackpackGrid/InventorySlot_4");

            Assert.AreEqual(slot0.anchoredPosition.y, slot3.anchoredPosition.y, 0.001f, "First four backpack slots should share the first row.");
            Assert.AreEqual(slot0.anchoredPosition.x, slot4.anchoredPosition.x, 0.001f, "The fifth backpack slot should start the second row.");
            Assert.Less(slot4.anchoredPosition.y, slot0.anchoredPosition.y - 1f, "The fifth backpack slot should be below the first row.");
        }

        private static void AssertHarvestButtonAboveAdvanceDayButton(Transform root)
        {
            var bottomPanel = GetRectTransform(root, "GameStardewAIView/BottomHost/FarmBottomHudRoot/BottomPanel");
            var harvestButton = GetRectTransform(root, "GameStardewAIView/BottomHost/FarmBottomHudRoot/BottomPanel/Toolbar/ToolButton_3");
            var advanceDayButton = GetRectTransform(root, "GameStardewAIView/BottomHost/FarmBottomHudRoot/BottomPanel/AdvanceDayButton");
            Rect harvestRect = GetRectInParent(bottomPanel, harvestButton);
            Rect advanceDayRect = GetRectInParent(bottomPanel, advanceDayButton);

            Assert.GreaterOrEqual(harvestRect.yMin, advanceDayRect.yMax + 2f, "Harvest button should sit above the advance day button.");
        }

        private static Rect GetRectInParent(RectTransform parent, RectTransform child)
        {
            Canvas.ForceUpdateCanvases();
            Vector3[] worldCorners = new Vector3[4];
            child.GetWorldCorners(worldCorners);

            Vector3 bottomLeft = parent.InverseTransformPoint(worldCorners[0]);
            Vector3 topRight = parent.InverseTransformPoint(worldCorners[2]);
            return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
        }

        private static void AssertRectApproximately(RectTransform rectTransform, float anchorMinX, float anchorMinY, float anchorMaxX, float anchorMaxY, float tolerance, string message)
        {
            Assert.AreEqual(anchorMinX, rectTransform.anchorMin.x, tolerance, message + " anchorMin.x");
            Assert.AreEqual(anchorMinY, rectTransform.anchorMin.y, tolerance, message + " anchorMin.y");
            Assert.AreEqual(anchorMaxX, rectTransform.anchorMax.x, tolerance, message + " anchorMax.x");
            Assert.AreEqual(anchorMaxY, rectTransform.anchorMax.y, tolerance, message + " anchorMax.y");
        }

        private static void AssertRectSize(RectTransform rectTransform, float expectedWidth, float expectedHeight, float tolerance, string message)
        {
            Assert.AreEqual(expectedWidth, rectTransform.sizeDelta.x, tolerance, message + " width");
            Assert.AreEqual(expectedHeight, rectTransform.sizeDelta.y, tolerance, message + " height");
        }

        private static void AssertDialogueTextFixedSize(object hudView, float expectedSpeakerSize, float expectedSpeakerHeight, float expectedBodySize)
        {
            var speaker = GetTextMesh(hudView, "DialogueSpeakerText");
            var body = GetTextMesh(hudView, "DialogueBodyText");

            Assert.IsNotNull(speaker, "DialogueSpeakerText was not found.");
            Assert.IsNotNull(body, "DialogueBodyText was not found.");
            Assert.IsFalse((bool)speaker.GetType().GetProperty("enableAutoSizing").GetValue(speaker), "DialogueSpeakerText should not auto-size.");
            Assert.IsFalse((bool)body.GetType().GetProperty("enableAutoSizing").GetValue(body), "DialogueBodyText should not auto-size.");
            Assert.AreEqual(expectedSpeakerSize, (float)speaker.GetType().GetProperty("fontSize").GetValue(speaker), 0.001f, "DialogueSpeakerText font size");
            Assert.AreEqual(expectedSpeakerHeight, GetRectSizeDelta(speaker).y, 0.001f, "DialogueSpeakerText height");
            Assert.AreEqual(expectedBodySize, (float)body.GetType().GetProperty("fontSize").GetValue(body), 0.001f, "DialogueBodyText font size");
        }

        private static void AssertStatusTextHeight(object hudView, float expectedHeight)
        {
            var status = GetTextMesh(hudView, "StatusText");

            Assert.IsNotNull(status, "StatusText was not found.");
            Assert.AreEqual(expectedHeight, GetRectSizeDelta(status).y, 0.001f, "StatusText height");
        }

        private static object GetTextMesh(object hudView, string propertyName)
        {
            var property = hudView.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property, "Property was not found: " + propertyName);
            return property.GetValue(hudView);
        }

        private static Vector2 GetRectSizeDelta(object component)
        {
            var rectTransform = component.GetType().GetProperty("rectTransform").GetValue(component);
            return (Vector2)rectTransform.GetType().GetProperty("sizeDelta").GetValue(rectTransform);
        }

        private static void AssertNoUnexpectedLogs()
        {
            var report = PlayModeGlobalLogMonitor.BuildFailureReport();
            if (!string.IsNullOrEmpty(report))
            {
                Assert.Fail("Unexpected Error/Exception logs:\n" + report);
            }
        }

        private sealed class TestHostBehaviour : MonoBehaviour
        {
        }
    }
}
