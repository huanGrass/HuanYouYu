using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    internal static class MiniGameShellTopBarBuilder
    {
        private static readonly Color HeaderColor = new Color(1f, 0.98f, 0.92f, 0.68f);
        private static readonly Color HeaderShadowColor = new Color(0.31f, 0.42f, 0.26f, 0.1f);
        private static readonly Color TitleColor = new Color(0.29f, 0.39f, 0.22f, 1f);
        private static readonly Color ScoreColor = new Color(0.82f, 0.58f, 0.25f, 1f);
        private static readonly Color ExtraColor = new Color(0.2f, 0.26f, 0.17f, 0.92f);

        internal sealed class TopBarRefs
        {
            public TopBarRefs(RectTransform root, RectTransform headerRoot, TextMeshProUGUI titleText, TextMeshProUGUI scoreText, TextMeshProUGUI extraText)
            {
                Root = root;
                HeaderRoot = headerRoot;
                TitleText = titleText;
                ScoreText = scoreText;
                ExtraText = extraText;
            }

            public RectTransform Root { get; }

            public RectTransform HeaderRoot { get; }

            public TextMeshProUGUI TitleText { get; }

            public TextMeshProUGUI ScoreText { get; }

            public TextMeshProUGUI ExtraText { get; }
        }

        internal sealed class TextStyle
        {
            public string Name { get; set; }

            public string DefaultText { get; set; }

            public Color Color { get; set; }

            public float FontSize { get; set; }

            public FontStyles FontStyle { get; set; }

            public float PreferredHeight { get; set; }
        }

        internal sealed class TopBarConfig
        {
            public string InstanceName { get; set; }

            public Vector2 RootAnchoredPosition { get; set; }

            public Vector2 ShadowAnchorMin { get; set; }

            public Vector2 ShadowAnchorMax { get; set; }

            public Vector2 ShadowOffsetMin { get; set; }

            public Vector2 ShadowOffsetMax { get; set; }

            public Vector2 HeaderAnchorMin { get; set; }

            public Vector2 HeaderAnchorMax { get; set; }

            public Vector2 HeaderOffsetMin { get; set; }

            public Vector2 HeaderOffsetMax { get; set; }

            public RectOffset HeaderPadding { get; set; }

            public float HeaderSpacing { get; set; }

            public float PreferredHeight { get; set; }

            public TextStyle TitleStyle { get; set; }

            public TextStyle ScoreStyle { get; set; }

            public TextStyle ExtraStyle { get; set; }
        }

        internal static TopBarConfig CreateDefaultConfig(string instanceName)
        {
            return new TopBarConfig
            {
                InstanceName = instanceName,
                RootAnchoredPosition = Vector2.zero,
                ShadowAnchorMin = new Vector2(0.23f, 0.22f),
                ShadowAnchorMax = new Vector2(0.77f, 0.84f),
                ShadowOffsetMin = Vector2.zero,
                ShadowOffsetMax = new Vector2(0f, -4f),
                HeaderAnchorMin = new Vector2(0.22f, 0.24f),
                HeaderAnchorMax = new Vector2(0.78f, 0.86f),
                HeaderOffsetMin = Vector2.zero,
                HeaderOffsetMax = Vector2.zero,
                HeaderPadding = new RectOffset(22, 22, 14, 14),
                HeaderSpacing = 2f,
                PreferredHeight = 96f,
                TitleStyle = CreateTextStyle("Title", string.Empty, TitleColor, 33f, FontStyles.Bold, 40f),
                ScoreStyle = CreateTextStyle("Score", string.Empty, ScoreColor, 24f, FontStyles.Bold, 28f),
                ExtraStyle = null
            };
        }

        internal static TopBarConfig CreateReversiConfig(string instanceName)
        {
            var config = CreateDefaultConfig(instanceName);
            config.ExtraStyle = CreateTextStyle("Turn", string.Empty, ExtraColor, 24f, FontStyles.Normal, 28f);
            return config;
        }

        internal static TextStyle CreateTextStyle(string name, string defaultText, Color color, float fontSize, FontStyles fontStyle, float preferredHeight)
        {
            return new TextStyle
            {
                Name = name,
                DefaultText = defaultText,
                Color = color,
                FontSize = fontSize,
                FontStyle = fontStyle,
                PreferredHeight = preferredHeight
            };
        }

        internal static TopBarRefs CreateTopBar(Transform parent, TopBarConfig config)
        {
            var rootObject = CreateRectObject(config.InstanceName, parent);
            var root = rootObject.GetComponent<RectTransform>();
            Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.anchoredPosition = config.RootAnchoredPosition;
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = config.PreferredHeight;

            var headerShadow = CreateGraphicObject<RoundedRectGraphic>("HeaderShadow", root);
            var headerShadowRect = headerShadow.GetComponent<RectTransform>();
            Stretch(
                headerShadowRect,
                config.ShadowAnchorMin,
                config.ShadowAnchorMax,
                config.ShadowOffsetMin,
                config.ShadowOffsetMax);
            headerShadow.color = HeaderShadowColor;
            headerShadow.CornerRadius = 28f;
            headerShadow.raycastTarget = false;

            var header = CreateGraphicObject<RoundedRectGraphic>("Header", root);
            var headerRect = header.GetComponent<RectTransform>();
            Stretch(
                headerRect,
                config.HeaderAnchorMin,
                config.HeaderAnchorMax,
                config.HeaderOffsetMin,
                config.HeaderOffsetMax);
            header.color = HeaderColor;
            header.CornerRadius = 28f;
            header.raycastTarget = false;

            var headerLayout = header.gameObject.AddComponent<VerticalLayoutGroup>();
            headerLayout.padding = config.HeaderPadding;
            headerLayout.spacing = config.HeaderSpacing;
            headerLayout.childAlignment = TextAnchor.MiddleCenter;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = false;
            headerLayout.childForceExpandWidth = true;
            headerLayout.childForceExpandHeight = false;

            var fontAsset = MiniGameFontProvider.DefaultFont;

            var title = CreateTextObject("Title", headerRect, fontAsset);
            ConfigureText(title, config.TitleStyle);

            var score = CreateTextObject("Score", headerRect, fontAsset);
            ConfigureText(score, config.ScoreStyle);

            TextMeshProUGUI extra = null;
            if (config.ExtraStyle != null)
            {
                extra = CreateTextObject(config.ExtraStyle.Name, headerRect, fontAsset);
                ConfigureText(extra, config.ExtraStyle);
            }

            return new TopBarRefs(root, headerRect, title, score, extra);
        }

        private static void ConfigureText(TextMeshProUGUI text, TextStyle style)
        {
            if (text == null || style == null)
            {
                return;
            }

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(200f, 50f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            text.name = style.Name;
            text.text = style.DefaultText;
            text.color = style.Color;
            text.fontSize = style.FontSize;
            text.fontStyle = style.FontStyle;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.raycastTarget = false;

            var layout = text.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = style.PreferredHeight;
        }

        private static TextMeshProUGUI CreateTextObject(string name, Transform parent, TMP_FontAsset fontAsset)
        {
            var textObject = CreateRectObject(name, parent);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null)
            {
                text.font = fontAsset;
            }

            return text;
        }

        private static TGraphic CreateGraphicObject<TGraphic>(string name, Transform parent)
            where TGraphic : Graphic
        {
            var graphicObject = CreateRectObject(name, parent);
            graphicObject.AddComponent<CanvasRenderer>();
            return graphicObject.AddComponent<TGraphic>();
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
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
