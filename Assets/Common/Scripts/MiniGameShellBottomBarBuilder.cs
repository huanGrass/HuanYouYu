using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HuanYouYu.MiniGameHall
{
    internal static class MiniGameShellBottomBarBuilder
    {
        private const string RestartSpriteResourcePath = "HallTheme/shuffle_button";
        private const string ShuffleSpriteResourcePath = RestartSpriteResourcePath;
        private const string HintSpriteResourcePath = "HallTheme/hint_button";

        private static readonly Color TrayColor = new Color(1f, 0.98f, 0.92f, 0.66f);
        private static readonly Color ShadowColor = new Color(0.31f, 0.42f, 0.26f, 0.10f);
        private static readonly Color TextButtonColor = new Color(0.96f, 0.97f, 0.92f, 1f);
        private static readonly Color SelectedTextButtonColor = new Color(0.90f, 0.75f, 0.28f, 1f);
        private static readonly Color TextButtonLabelColor = new Color(0.25f, 0.36f, 0.22f, 1f);
        private static readonly Vector2 TrayPadding = new Vector2(24f, 12f);
        private static readonly Vector2 ShadowPadding = new Vector2(26f, 14f);
        private const float ShadowYOffset = -4f;

        internal sealed class ButtonRefs
        {
            public ButtonRefs(Button button, RectTransform root, Image icon)
            {
                Button = button;
                Root = root;
                Icon = icon;
            }

            public Button Button { get; }

            public RectTransform Root { get; }

            public Image Icon { get; }
        }

        internal sealed class BottomContainerConfig
        {
            public string InstanceName { get; set; }

            public Vector2 RootAnchoredPosition { get; set; }
        }

        internal sealed class BottomContainerRefs
        {
            public BottomContainerRefs(
                RectTransform root,
                RectTransform actionTray,
                RectTransform actionBar)
            {
                Root = root;
                ActionTray = actionTray;
                ActionBar = actionBar;
            }

            public RectTransform Root { get; }

            public RectTransform ActionTray { get; }

            public RectTransform ActionBar { get; }
        }

        internal static BottomContainerConfig CreateDefaultContainerConfig(string instanceName)
        {
            return new BottomContainerConfig
            {
                InstanceName = instanceName,
                RootAnchoredPosition = Vector2.zero
            };
        }

        internal static BottomContainerRefs CreateBottomContainer(Transform parent, BottomContainerConfig config)
        {
            var rootObject = CreateRectObject(config.InstanceName, parent);
            var root = rootObject.GetComponent<RectTransform>();
            Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.anchoredPosition = config.RootAnchoredPosition;

            var actionBarObject = CreateRectObject("ActionBar", root);
            var actionBar = actionBarObject.GetComponent<RectTransform>();
            actionBar.SetParent(root, false);
            actionBar.anchorMin = new Vector2(0.5f, 0.5f);
            actionBar.anchorMax = new Vector2(0.5f, 0.5f);
            actionBar.pivot = new Vector2(0.5f, 0.5f);
            actionBar.anchoredPosition = new Vector2(0f, 4f);
            actionBar.sizeDelta = new Vector2(216f, 88f);

            var trayShadow = CreateBarBackground("TrayShadow", actionBar, ShadowColor, 34f, ShadowPadding, ShadowYOffset);
            var actionTray = CreateBarBackground("ActionTray", actionBar, TrayColor, 32f, TrayPadding, 0f);

            var layout = actionBarObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 32f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = actionBarObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return new BottomContainerRefs(root, actionTray.rectTransform, actionBar);
        }

        internal static ButtonRefs CreateRestartButton(Transform parent, string instanceName = "RestartButton")
        {
            return CreateActionButton(parent, instanceName, RestartSpriteResourcePath);
        }

        internal static ButtonRefs CreateShuffleButton(Transform parent, string instanceName = "ShuffleButton")
        {
            return CreateActionButton(parent, instanceName, ShuffleSpriteResourcePath);
        }

        internal static ButtonRefs CreateHintButton(Transform parent, string instanceName = "HintButton")
        {
            return CreateActionButton(parent, instanceName, HintSpriteResourcePath);
        }

        internal static ButtonRefs CreateLevelSelectButton(Transform parent, string instanceName = "LevelSelectButton")
        {
            var button = CreateTextActionButton(parent, instanceName, "选关", 116f);
            return new ButtonRefs(button, button.GetComponent<RectTransform>(), null);
        }

        internal static void ConfigureTextActionBar(RectTransform actionBar, float spacing = 14f)
        {
            var layout = actionBar == null ? null : actionBar.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = spacing;
            }
        }

        internal static void AddActionTrayBackground(
            RectTransform parent,
            Vector2 padding,
            float cornerRadius = 32f)
        {
            if (parent == null)
            {
                return;
            }

            var shadow = CreateBarBackground(
                "TrayShadow",
                parent,
                ShadowColor,
                cornerRadius + 2f,
                padding + new Vector2(2f, 2f),
                ShadowYOffset);
            shadow.transform.SetAsFirstSibling();

            var tray = CreateBarBackground(
                "ActionTray",
                parent,
                TrayColor,
                cornerRadius,
                padding,
                0f);
            tray.transform.SetSiblingIndex(1);
        }

        internal static Button CreateTextActionButton(
            Transform parent,
            string instanceName,
            string labelText,
            float width = 112f,
            float height = 72f,
            float fontSize = 22f,
            float cornerRadius = 22f)
        {
            var buttonObject = new GameObject(instanceName, typeof(RectTransform), typeof(Button), typeof(LayoutElement));
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(width, height);

            var layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            layoutElement.preferredHeight = height;
            layoutElement.layoutPriority = 1;

            var background = buttonObject.AddComponent<RoundedRectGraphic>();
            background.CornerRadius = cornerRadius;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;

            var labelObject = CreateRectObject("Label", buttonRect);
            var labelRect = labelObject.GetComponent<RectTransform>();
            Stretch(labelRect, Vector2.zero, Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f));
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = MiniGameFontProvider.DefaultFont;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.color = TextButtonLabelColor;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            label.text = labelText;
            ConfigureTextActionButton(button, label);
            return button;
        }

        internal static void ConfigureTextActionButton(
            Button button,
            TextMeshProUGUI label = null,
            bool selected = false)
        {
            if (button == null)
            {
                return;
            }

            if (button.targetGraphic != null)
            {
                button.targetGraphic.color = selected ? SelectedTextButtonColor : TextButtonColor;
            }

            if (label != null)
            {
                label.font = MiniGameFontProvider.DefaultFont;
                label.fontStyle = FontStyles.Bold;
                label.color = TextButtonLabelColor;
            }

            ConfigureTextActionButtonColors(button);
        }

        internal static void SetTextActionButtonSelected(Button button, bool selected)
        {
            if (button != null && button.targetGraphic != null)
            {
                button.targetGraphic.color = selected ? SelectedTextButtonColor : TextButtonColor;
            }
        }

        private static void ConfigureTextActionButtonColors(Button button)
        {
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.98f, 0.88f, 1f);
            colors.pressedColor = new Color(0.84f, 0.89f, 0.76f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.66f, 0.69f, 0.63f, 0.62f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static ButtonRefs CreateActionButton(Transform parent, string instanceName, string spriteResourcePath)
        {
            var buttonObject = new GameObject(instanceName, typeof(RectTransform), typeof(Button), typeof(LayoutElement));
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(84f, 84f);

            var layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 84f;
            layoutElement.preferredHeight = 84f;
            layoutElement.layoutPriority = 1;

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(buttonRect, false);
            Stretch(iconRect, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));

            var iconImage = iconObject.GetComponent<Image>();
            iconImage.sprite = LoadSprite(spriteResourcePath);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = true;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = iconImage;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            colors.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;

            return new ButtonRefs(button, buttonRect, iconImage);
        }

        private static RoundedRectGraphic CreateRoundedRect(string name, Transform parent, Color color, float cornerRadius)
        {
            var gameObject = CreateRectObject(name, parent);
            gameObject.AddComponent<CanvasRenderer>();
            var graphic = gameObject.AddComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = cornerRadius;
            return graphic;
        }

        private static RoundedRectGraphic CreateBarBackground(
            string name,
            RectTransform parent,
            Color color,
            float cornerRadius,
            Vector2 padding,
            float yOffset)
        {
            var graphic = CreateRoundedRect(name, parent, color, cornerRadius);
            var rect = graphic.rectTransform;
            Stretch(
                rect,
                Vector2.zero,
                Vector2.one,
                new Vector2(-padding.x, -padding.y + yOffset),
                new Vector2(padding.x, padding.y + yOffset));

            var layout = graphic.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            graphic.raycastTarget = false;
            return graphic;
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static Sprite LoadSprite(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return null;
            }

            return Resources.Load<Sprite>(resourcePath);
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }
    }
}
