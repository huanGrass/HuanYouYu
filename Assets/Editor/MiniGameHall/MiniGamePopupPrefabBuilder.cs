using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall.Editor
{
    public static class MiniGamePopupPrefabBuilder
    {
        private const string PrefabDirectory = "Assets/Common/Resources";
        private const string PrefabPath = PrefabDirectory + "/MiniGamePopup.prefab";
        private const string PanelSpritePath = "Assets/Common/Resources/HallTheme/popup_panel.png";
        private const string PanelTopDecorSpritePath = "Assets/Common/Resources/HallTheme/popup_panel_top_decor_side.png";
        private const string ButtonSpritePath = "Assets/Common/Resources/HallTheme/hall_tab_unselected.png";
        private const string CloseButtonSpritePath = "Assets/Common/Resources/HallTheme/close_button.png";

        [MenuItem("Tools/小游戏/构建弹框预制体")]
        public static void BuildPopupPrefab()
        {
            EnsureFolder("Assets/Common/Resources");
            EnsureFolder(PrefabDirectory);

            var panelSprite = LoadRequiredSprite(PanelSpritePath);
            var panelTopDecorSprite = LoadRequiredSprite(PanelTopDecorSpritePath);
            var buttonSprite = LoadRequiredSprite(ButtonSpritePath);
            var closeButtonSprite = LoadRequiredSprite(CloseButtonSpritePath);

            var root = new GameObject("MiniGamePopup", typeof(RectTransform), typeof(CanvasGroup));
            Stretch(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            try
            {
                var blocker = new GameObject(
                    "Blocker",
                    typeof(RectTransform),
                    typeof(CanvasRenderer));
                blocker.transform.SetParent(root.transform, false);
                Stretch(blocker.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                var blockerGraphic = blocker.AddComponent<RoundedRectGraphic>();
                blockerGraphic.color = new Color(0.19f, 0.18f, 0.14f, 0.56f);
                blockerGraphic.CornerRadius = 0f;
                blocker.AddComponent<Button>().targetGraphic = blockerGraphic;

                var dialog = new GameObject(
                    "Dialog",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                dialog.transform.SetParent(root.transform, false);
                var dialogRect = dialog.GetComponent<RectTransform>();
                dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
                dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
                dialogRect.pivot = new Vector2(0.5f, 0.5f);
                dialogRect.sizeDelta = new Vector2(650f, 508f);

                var dialogImage = dialog.GetComponent<Image>();
                dialogImage.sprite = panelSprite;
                dialogImage.type = Image.Type.Sliced;
                dialogImage.preserveAspect = false;
                dialogImage.raycastTarget = true;

                PopupPanelTopDecorUtility.CreateMirroredTopDecor(dialog.transform, panelTopDecorSprite, 650f);

                var closeButton = new GameObject(
                    "CloseButton",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));
                closeButton.transform.SetParent(dialog.transform, false);
                var closeButtonRect = closeButton.GetComponent<RectTransform>();
                closeButtonRect.anchorMin = new Vector2(1f, 1f);
                closeButtonRect.anchorMax = new Vector2(1f, 1f);
                closeButtonRect.pivot = new Vector2(0.5f, 0.5f);
                closeButtonRect.anchoredPosition = new Vector2(-52f, -44f);
                closeButtonRect.sizeDelta = new Vector2(50f, 50f);

                var closeButtonImage = closeButton.GetComponent<Image>();
                closeButtonImage.sprite = closeButtonSprite;
                closeButtonImage.preserveAspect = true;
                closeButtonImage.color = Color.white;

                var closeButtonComponent = closeButton.GetComponent<Button>();
                closeButtonComponent.targetGraphic = closeButtonImage;

                var title = CreateText("Title", "已暂停", 36, FontStyles.Bold, TextAlignmentOptions.Center);
                title.transform.SetParent(dialog.transform, false);
                title.color = new Color(0.34f, 0.47f, 0.24f, 1f);
                title.enableWordWrapping = false;
                title.enableAutoSizing = true;
                title.fontSizeMin = 26f;
                title.fontSizeMax = 36f;
                var titleRect = title.rectTransform;
                titleRect.anchorMin = new Vector2(0.5f, 1f);
                titleRect.anchorMax = new Vector2(0.5f, 1f);
                titleRect.pivot = new Vector2(0.5f, 0.5f);
                titleRect.anchoredPosition = new Vector2(0f, -35f);
                titleRect.sizeDelta = new Vector2(410f, 58f);

                var messagePanel = CreateRectOnly("MessagePanel");
                messagePanel.transform.SetParent(dialog.transform, false);
                var messagePanelRect = messagePanel.GetComponent<RectTransform>();
                messagePanelRect.anchorMin = new Vector2(0f, 0f);
                messagePanelRect.anchorMax = new Vector2(1f, 1f);
                messagePanelRect.offsetMin = new Vector2(50f, 180f);
                messagePanelRect.offsetMax = new Vector2(-50f, -140f);

                var message = CreateText(
                    "Message",
                    "离开后将结束本局。",
                    27,
                    FontStyles.Normal,
                    TextAlignmentOptions.Center);
                message.transform.SetParent(messagePanel.transform, false);
                var messageRect = message.rectTransform;
                Stretch(messageRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                message.color = new Color(0.40f, 0.34f, 0.26f, 1f);
                message.enableWordWrapping = true;
                message.enableAutoSizing = true;
                message.fontSizeMin = 22f;
                message.fontSizeMax = 27f;
                message.overflowMode = TextOverflowModes.Overflow;
                message.verticalAlignment = VerticalAlignmentOptions.Middle;
                message.margin = new Vector4(6f, 0f, 6f, 0f);

                var buttonRow = new GameObject(
                    "Buttons",
                    typeof(RectTransform),
                    typeof(HorizontalLayoutGroup));
                buttonRow.transform.SetParent(dialog.transform, false);
                var buttonRowRect = buttonRow.GetComponent<RectTransform>();
                buttonRowRect.anchorMin = new Vector2(0.5f, 0f);
                buttonRowRect.anchorMax = new Vector2(0.5f, 0f);
                buttonRowRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRowRect.anchoredPosition = new Vector2(0f, 90f);
                buttonRowRect.sizeDelta = new Vector2(480f, 84f);

                var buttonRowLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
                buttonRowLayout.spacing = 24f;
                buttonRowLayout.childAlignment = TextAnchor.MiddleCenter;
                buttonRowLayout.childControlWidth = false;
                buttonRowLayout.childControlHeight = false;
                buttonRowLayout.childForceExpandWidth = false;
                buttonRowLayout.childForceExpandHeight = false;

                BuildActionButton(
                    buttonRow.transform,
                    "CancelButton",
                    "继续游戏",
                    buttonSprite,
                    new Color(1f, 0.82f, 0.38f, 1f),
                    new Color(0.43f, 0.25f, 0.08f, 1f));
                BuildActionButton(
                    buttonRow.transform,
                    "ConfirmButton",
                    "确认退出",
                    buttonSprite,
                    new Color(0.98f, 0.97f, 0.94f, 1f),
                    new Color(0.40f, 0.33f, 0.24f, 1f));

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("MiniGame popup prefab generated at " + PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static void BuildPopupPrefabFromCommandLine()
        {
            BuildPopupPrefab();
        }

        private static void BuildActionButton(
            Transform parent,
            string name,
            string labelText,
            Sprite buttonSprite,
            Color backgroundColor,
            Color textColor)
        {
            var buttonRoot = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonRoot.transform.SetParent(parent, false);
            var rect = buttonRoot.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.sizeDelta = new Vector2(228f, 72f);

            var image = buttonRoot.GetComponent<Image>();
            image.sprite = buttonSprite;
            image.color = backgroundColor;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;

            var button = buttonRoot.GetComponent<Button>();
            button.targetGraphic = image;

            var layout = buttonRoot.GetComponent<LayoutElement>();
            layout.preferredWidth = 228f;
            layout.preferredHeight = 72f;

            var label = CreateText("Label", labelText, 24, FontStyles.Bold, TextAlignmentOptions.Center);
            label.transform.SetParent(buttonRoot.transform, false);
            label.color = textColor;
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.rectTransform.anchoredPosition = new Vector2(0f, 5f);
            label.rectTransform.sizeDelta = new Vector2(0f, 10f);
        }

        private static GameObject CreateRectOnly(string name)
        {
            return new GameObject(name, typeof(RectTransform));
        }

        private static TextMeshProUGUI CreateText(
            string name,
            string content,
            int fontSize,
            FontStyles fontStyle,
            TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.white;
            text.enableWordWrapping = false;
            return text;
        }

        private static Sprite LoadRequiredSprite(string assetPath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                throw new InvalidOperationException("Sprite not found: " + assetPath);
            }

            return sprite;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            var folderName = System.IO.Path.GetFileName(assetPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            {
                throw new InvalidOperationException("Invalid folder path: " + assetPath);
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
