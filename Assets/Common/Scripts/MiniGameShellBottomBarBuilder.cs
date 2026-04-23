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
            return CreateTextButton(parent, instanceName);
        }

        private static ButtonRefs CreateTextButton(Transform parent, string name)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(LayoutElement));
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(116f, 72f);

            var layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 116f;
            layoutElement.preferredHeight = 72f;
            layoutElement.layoutPriority = 1;

            var button = buttonObject.GetComponent<Button>();
            var backgroundObject = CreateRectObject("Background", buttonRect);
            var backgroundRect = backgroundObject.GetComponent<RectTransform>();
            Stretch(backgroundRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var backgroundImage = backgroundObject.AddComponent<Image>();
            backgroundImage.color = new Color32(53, 125, 97, 255);
            button.targetGraphic = backgroundImage;
            ConfigureButtonColors(button);

            var labelObject = CreateRectObject("Label", buttonRect);
            var labelRect = labelObject.GetComponent<RectTransform>();
            Stretch(labelRect, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = 21f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            label.text = "选关";

            return new ButtonRefs(button, buttonRect, backgroundImage);
        }

        private static void ConfigureButtonColors(Button button)
        {
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.98f, 0.98f, 0.98f, 1f);
            colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.58f, 0.58f, 0.58f, 0.65f);
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
