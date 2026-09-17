using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    internal static class MiniGameZoomControl
    {
        public static Slider Create(Transform parent, string prefix, out Button zoomOutButton, out Button zoomInButton)
        {
            var zoomRoot = CreateRectObject(prefix + "ZoomControl", parent);
            var layoutElement = zoomRoot.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 330f;
            layoutElement.preferredHeight = 52f;
            EnsureRoundedRectGraphic(zoomRoot.gameObject, new Color32(248, 251, 255, 245), 18f, true);

            var layout = zoomRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 6, 6);
            layout.spacing = 9f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            zoomOutButton = CreateZoomIconButton(prefix + "ZoomOutButton", zoomRoot, false);
            var zoomSlider = CreateZoomSlider(zoomRoot, prefix + "ZoomSlider");
            zoomInButton = CreateZoomIconButton(prefix + "ZoomInButton", zoomRoot, true);

            return zoomSlider;
        }
        private static Button CreateZoomIconButton(string name, Transform parent, bool isPlus)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(LayoutElement), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(38f, 38f);
            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 38f;
            layout.preferredHeight = 38f;

            var background = buttonObject.GetComponent<RoundedRectGraphic>();
            background.color = new Color32(255, 255, 255, 255);
            background.CornerRadius = 19f;
            background.raycastTarget = true;

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(ArrowEscapeZoomIconGraphic));
            iconObject.transform.SetParent(rect, false);
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(30f, 30f);
            var icon = iconObject.GetComponent<ArrowEscapeZoomIconGraphic>();
            icon.IsPlus = isPlus;
            icon.color = new Color32(73, 99, 138, 255);
            icon.raycastTarget = false;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            ConfigureButtonColors(button);
            MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.85f);
            return button;
        }

        private static Slider CreateZoomSlider(Transform parent, string name)
        {
            var sliderObject = new GameObject(name, typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            sliderObject.transform.SetParent(parent, false);
            var sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(214f, 38f);
            var layout = sliderObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 214f;
            layout.preferredHeight = 38f;

            var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            backgroundObject.transform.SetParent(sliderRect, false);
            var backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.offsetMin = new Vector2(0f, -4f);
            backgroundRect.offsetMax = new Vector2(0f, 4f);
            var background = backgroundObject.GetComponent<RoundedRectGraphic>();
            background.color = new Color32(148, 148, 148, 255);
            background.CornerRadius = 4f;
            background.raycastTarget = false;

            var fillArea = CreateRectObject("Fill Area", sliderRect);
            Stretch(fillArea, Vector2.zero, Vector2.one, new Vector2(0f, 15f), new Vector2(0f, -15f));
            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            fillObject.transform.SetParent(fillArea, false);
            var fillRect = fillObject.GetComponent<RectTransform>();
            Stretch(fillRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fill = fillObject.GetComponent<RoundedRectGraphic>();
            fill.color = new Color32(28, 219, 99, 255);
            fill.CornerRadius = 4f;
            fill.raycastTarget = false;

            var handleArea = CreateRectObject("Handle Slide Area", sliderRect);
            handleArea.anchorMin = new Vector2(0f, 0.5f);
            handleArea.anchorMax = new Vector2(1f, 0.5f);
            handleArea.pivot = new Vector2(0.5f, 0.5f);
            handleArea.offsetMin = new Vector2(0f, -16f);
            handleArea.offsetMax = new Vector2(0f, 16f);
            var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            handleObject.transform.SetParent(handleArea, false);
            var handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(32f, 0f);
            var handle = handleObject.GetComponent<RoundedRectGraphic>();
            handle.color = new Color32(64, 166, 230, 255);
            handle.CornerRadius = 16f;
            handle.raycastTarget = true;

            var slider = sliderObject.GetComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            slider.SetValueWithoutNotify(0f);
            return slider;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private static RectTransform CreateRectObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private static RoundedRectGraphic EnsureRoundedRectGraphic(GameObject target, Color color, float radius, bool raycastTarget)
        {
            if (target.GetComponent<CanvasRenderer>() == null)
            {
                target.AddComponent<CanvasRenderer>();
            }

            var graphic = target.GetComponent<RoundedRectGraphic>() ?? target.AddComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = radius;
            graphic.raycastTarget = raycastTarget;
            return graphic;
        }

        private static void ConfigureButtonColors(Button button)
        {
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.94f);
            colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.disabledColor = new Color(0.56f, 0.56f, 0.56f, 0.58f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

    }
}