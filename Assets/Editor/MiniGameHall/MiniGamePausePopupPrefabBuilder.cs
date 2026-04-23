using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall.Editor
{
    public static class MiniGamePausePopupPrefabBuilder
    {
        private const string PrefabDirectory = "Assets/Common/Resources";
        private const string PrefabPath = PrefabDirectory + "/MiniGamePausePopup.prefab";
        private const string PanelSpritePath = "Assets/Common/Resources/HallTheme/popup_panel.png";
        private const string PanelTopDecorSpritePath = "Assets/Common/Resources/HallTheme/popup_panel_top_decor_side.png";
        private const string ButtonSpritePath = "Assets/Common/Resources/HallTheme/hall_tab_unselected.png";
        private const string CloseButtonSpritePath = "Assets/Common/Resources/HallTheme/close_button.png";

        [MenuItem("Tools/MiniGame/Build Pause Popup Prefab")]
        public static void BuildPausePopupPrefab()
        {
            EnsureFolder("Assets/Common/Resources");
            EnsureFolder(PrefabDirectory);

            var panelSprite = LoadRequiredSprite(PanelSpritePath);
            var panelTopDecorSprite = LoadRequiredSprite(PanelTopDecorSpritePath);
            var buttonSprite = LoadRequiredSprite(ButtonSpritePath);
            var closeButtonSprite = LoadRequiredSprite(CloseButtonSpritePath);

            var root = new GameObject("MiniGamePausePopup", typeof(RectTransform), typeof(CanvasGroup));
            Stretch(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            try
            {
                var blocker = new GameObject(
                    "Blocker",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RoundedRectGraphic),
                    typeof(Button));
                blocker.transform.SetParent(root.transform, false);
                Stretch(blocker.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var blockerGraphic = blocker.GetComponent<RoundedRectGraphic>();
                blockerGraphic.color = new Color(0.19f, 0.18f, 0.14f, 0.56f);
                blockerGraphic.CornerRadius = 0f;
                blocker.GetComponent<Button>().targetGraphic = blockerGraphic;

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
                dialogRect.sizeDelta = new Vector2(650f, 620f);

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
                closeButton.GetComponent<Button>().targetGraphic = closeButtonImage;

                var title = CreateText("Title", "已暂停", 36, FontStyles.Bold, TextAlignmentOptions.Center);
                title.transform.SetParent(dialog.transform, false);
                title.color = new Color(0.34f, 0.47f, 0.24f, 1f);
                title.enableAutoSizing = true;
                title.fontSizeMin = 26f;
                title.fontSizeMax = 36f;
                var titleRect = title.rectTransform;
                titleRect.anchorMin = new Vector2(0.5f, 1f);
                titleRect.anchorMax = new Vector2(0.5f, 1f);
                titleRect.pivot = new Vector2(0.5f, 0.5f);
                titleRect.anchoredPosition = new Vector2(0f, -35f);
                titleRect.sizeDelta = new Vector2(410f, 58f);

                var helpButton = new GameObject(
                    "HelpButton",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RoundedRectGraphic),
                    typeof(Button));
                helpButton.transform.SetParent(dialog.transform, false);
                var helpButtonRect = helpButton.GetComponent<RectTransform>();
                helpButtonRect.anchorMin = new Vector2(0.5f, 1f);
                helpButtonRect.anchorMax = new Vector2(0.5f, 1f);
                helpButtonRect.pivot = new Vector2(0.5f, 0.5f);
                helpButtonRect.anchoredPosition = new Vector2(0f, -120f);
                helpButtonRect.sizeDelta = new Vector2(252f, 56f);
                var helpButtonGraphic = helpButton.GetComponent<RoundedRectGraphic>();
                helpButtonGraphic.color = new Color(0.91f, 0.95f, 0.83f, 1f);
                helpButtonGraphic.CornerRadius = 28f;
                helpButton.GetComponent<Button>().targetGraphic = helpButtonGraphic;

                var helpLabel = CreateText("Label", "玩法说明", 24, FontStyles.Bold, TextAlignmentOptions.Center);
                helpLabel.transform.SetParent(helpButton.transform, false);
                helpLabel.color = new Color(0.38f, 0.50f, 0.24f, 1f);
                Stretch(helpLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                CreateTopDivider("TopDivider", dialog.transform, -170f, 514f);

                var settings = CreateRectOnly("Settings");
                settings.transform.SetParent(dialog.transform, false);
                var settingsRect = settings.GetComponent<RectTransform>();
                settingsRect.anchorMin = new Vector2(0.5f, 0.5f);
                settingsRect.anchorMax = new Vector2(0.5f, 0.5f);
                settingsRect.pivot = new Vector2(0.5f, 0.5f);
                settingsRect.anchoredPosition = new Vector2(0f, 0f);
                settingsRect.sizeDelta = new Vector2(514f, 288f);

                BuildSettingRow(settings.transform, "MusicRow", "音乐", 96f, true);
                BuildSettingRow(settings.transform, "SfxRow", "音效", 0f, true);
                BuildSettingRow(settings.transform, "VibrationRow", "震动", -96f, false);

                CreateTopDivider("BottomDivider", dialog.transform, -460f, 514f);

                var buttonRow = new GameObject(
                    "MainButtons",
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

                BuildStandaloneActionButton(
                    buttonRow.transform,
                    "ContinueButton",
                    "继续游戏",
                    buttonSprite,
                    new Color(1f, 0.84f, 0.34f, 1f),
                    new Color(0.42f, 0.26f, 0.08f, 1f));
                BuildStandaloneActionButton(
                    buttonRow.transform,
                    "ExitButton",
                    "返回大厅",
                    buttonSprite,
                    new Color(0.98f, 0.97f, 0.94f, 1f),
                    new Color(0.40f, 0.33f, 0.24f, 1f));

                var helpOverlay = new GameObject("HelpOverlay", typeof(RectTransform));
                helpOverlay.transform.SetParent(root.transform, false);
                Stretch(helpOverlay.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                helpOverlay.SetActive(false);

                var helpBlocker = new GameObject(
                    "Blocker",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RoundedRectGraphic),
                    typeof(Button));
                helpBlocker.transform.SetParent(helpOverlay.transform, false);
                Stretch(helpBlocker.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var helpBlockerGraphic = helpBlocker.GetComponent<RoundedRectGraphic>();
                helpBlockerGraphic.color = new Color(0.19f, 0.18f, 0.14f, 0.36f);
                helpBlockerGraphic.CornerRadius = 0f;
                helpBlocker.GetComponent<Button>().targetGraphic = helpBlockerGraphic;

                var helpDialog = new GameObject(
                    "Dialog",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                helpDialog.transform.SetParent(helpOverlay.transform, false);
                var helpDialogRect = helpDialog.GetComponent<RectTransform>();
                helpDialogRect.anchorMin = new Vector2(0.5f, 0.5f);
                helpDialogRect.anchorMax = new Vector2(0.5f, 0.5f);
                helpDialogRect.pivot = new Vector2(0.5f, 0.5f);
                helpDialogRect.sizeDelta = new Vector2(660f, 380f);

                var helpDialogImage = helpDialog.GetComponent<Image>();
                helpDialogImage.sprite = panelSprite;
                helpDialogImage.type = Image.Type.Sliced;
                helpDialogImage.preserveAspect = false;

                PopupPanelTopDecorUtility.CreateMirroredTopDecor(helpDialog.transform, panelTopDecorSprite, 660f);

                var helpTitle = CreateText("Title", "玩法说明", 34, FontStyles.Bold, TextAlignmentOptions.Center);
                helpTitle.transform.SetParent(helpDialog.transform, false);
                helpTitle.color = new Color(0.31f, 0.48f, 0.24f, 1f);
                var helpTitleRect = helpTitle.rectTransform;
                helpTitleRect.anchorMin = new Vector2(0.5f, 1f);
                helpTitleRect.anchorMax = new Vector2(0.5f, 1f);
                helpTitleRect.pivot = new Vector2(0.5f, 0.5f);
                helpTitleRect.anchoredPosition = new Vector2(0f, -35f);
                helpTitleRect.sizeDelta = new Vector2(300f, 56f);

                var helpMessage = CreateText("Message", "玩法说明加载中。", 24, FontStyles.Normal, TextAlignmentOptions.TopLeft);
                helpMessage.transform.SetParent(helpDialog.transform, false);
                helpMessage.color = new Color(0.37f, 0.33f, 0.25f, 1f);
                helpMessage.enableWordWrapping = true;
                helpMessage.enableAutoSizing = true;
                helpMessage.fontSizeMin = 22f;
                helpMessage.fontSizeMax = 26f;
                helpMessage.overflowMode = TextOverflowModes.Masking;
                helpMessage.verticalAlignment = VerticalAlignmentOptions.Top;
                helpMessage.margin = new Vector4(8f, 0f, 8f, 0f);
                var helpMessageRect = helpMessage.rectTransform;
                helpMessageRect.anchorMin = new Vector2(0f, 0f);
                helpMessageRect.anchorMax = new Vector2(1f, 1f);
                helpMessageRect.offsetMin = new Vector2(52f, 122f);
                helpMessageRect.offsetMax = new Vector2(-52f, -120f);

                var helpConfirmButton = BuildStandaloneActionButton(
                    helpDialog.transform,
                    "ConfirmButton",
                    "我知道了",
                    buttonSprite,
                    new Color(1f, 0.84f, 0.34f, 1f),
                    new Color(0.42f, 0.26f, 0.08f, 1f));
                var helpConfirmRect = helpConfirmButton.GetComponent<RectTransform>();
                helpConfirmRect.anchorMin = new Vector2(0.5f, 0f);
                helpConfirmRect.anchorMax = new Vector2(0.5f, 0f);
                helpConfirmRect.pivot = new Vector2(0.5f, 0.5f);
                helpConfirmRect.anchoredPosition = new Vector2(0f, 70f);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("MiniGame pause popup prefab generated at " + PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static void BuildPausePopupPrefabFromCommandLine()
        {
            BuildPausePopupPrefab();
        }

        private static void BuildSettingRow(Transform parent, string name, string titleText, float anchoredY, bool defaultOn)
        {
            var row = CreateRectOnly(name);
            row.transform.SetParent(parent, false);
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = new Vector2(0f, anchoredY);
            rowRect.sizeDelta = new Vector2(514f, 84f);

            var title = CreateText("Title", titleText, 28, FontStyles.Normal, TextAlignmentOptions.Left);
            title.transform.SetParent(row.transform, false);
            title.color = new Color(0.22f, 0.19f, 0.15f, 1f);
            title.enableAutoSizing = true;
            title.fontSizeMin = 24f;
            title.fontSizeMax = 30f;
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0.5f);
            titleRect.anchorMax = new Vector2(0f, 0.5f);
            titleRect.pivot = new Vector2(0f, 0.5f);
            titleRect.anchoredPosition = new Vector2(12f, 0f);
            titleRect.sizeDelta = new Vector2(220f, 44f);

            var toggleButton = new GameObject(
                "ToggleButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic),
                typeof(Button));
            toggleButton.transform.SetParent(row.transform, false);
            var toggleRect = toggleButton.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(1f, 0.5f);
            toggleRect.anchorMax = new Vector2(1f, 0.5f);
            toggleRect.pivot = new Vector2(1f, 0.5f);
            toggleRect.anchoredPosition = new Vector2(-8f, 0f);
            toggleRect.sizeDelta = new Vector2(138f, 60f);

            var track = toggleButton.GetComponent<RoundedRectGraphic>();
            track.color = defaultOn ? new Color(0.51f, 0.80f, 0.14f, 1f) : new Color(0.84f, 0.81f, 0.75f, 1f);
            track.CornerRadius = 30f;
            toggleButton.GetComponent<Button>().targetGraphic = track;

            var stateLabel = CreateText("Label", defaultOn ? "开" : "关", 24, FontStyles.Bold, TextAlignmentOptions.Center);
            stateLabel.transform.SetParent(toggleButton.transform, false);
            stateLabel.color = Color.white;
            var stateLabelRect = stateLabel.rectTransform;
            stateLabelRect.anchorMin = new Vector2(0f, 0f);
            stateLabelRect.anchorMax = new Vector2(1f, 1f);
            stateLabelRect.offsetMin = new Vector2(12f, 0f);
            stateLabelRect.offsetMax = new Vector2(-60f, 0f);

            var knob = new GameObject(
                "Knob",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic));
            knob.transform.SetParent(toggleButton.transform, false);
            var knobRect = knob.GetComponent<RectTransform>();
            knobRect.anchorMin = new Vector2(0.5f, 0.5f);
            knobRect.anchorMax = new Vector2(0.5f, 0.5f);
            knobRect.pivot = new Vector2(0.5f, 0.5f);
            knobRect.anchoredPosition = new Vector2(defaultOn ? 39f : -39f, 0f);
            knobRect.sizeDelta = new Vector2(48f, 48f);
            var knobGraphic = knob.GetComponent<RoundedRectGraphic>();
            knobGraphic.color = new Color(0.99f, 0.99f, 0.97f, 1f);
            knobGraphic.CornerRadius = 24f;

            if (anchoredY > -80f)
            {
                CreateRowDivider("Divider", row.transform, 490f);
            }
        }

        private static GameObject BuildStandaloneActionButton(
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

            var label = CreateText("Label", labelText, 26, FontStyles.Bold, TextAlignmentOptions.Center);
            label.transform.SetParent(buttonRoot.transform, false);
            label.color = textColor;
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.rectTransform.anchoredPosition = new Vector2(0f, 5f);
            label.rectTransform.sizeDelta = new Vector2(0f, 10f);
            return buttonRoot;
        }

        private static void CreateTopDivider(string name, Transform parent, float anchoredY, float width)
        {
            var divider = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic));
            divider.transform.SetParent(parent, false);
            var rect = divider.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, anchoredY);
            rect.sizeDelta = new Vector2(width, 2f);
            var graphic = divider.GetComponent<RoundedRectGraphic>();
            graphic.color = new Color(0.81f, 0.77f, 0.69f, 0.44f);
            graphic.CornerRadius = 1f;
        }

        private static void CreateRowDivider(string name, Transform parent, float width)
        {
            var divider = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic));
            divider.transform.SetParent(parent, false);
            var rect = divider.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, 2f);
            var graphic = divider.GetComponent<RoundedRectGraphic>();
            graphic.color = new Color(0.81f, 0.77f, 0.69f, 0.44f);
            graphic.CornerRadius = 1f;
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
