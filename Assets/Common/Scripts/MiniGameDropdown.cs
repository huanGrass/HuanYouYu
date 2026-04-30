using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed class MiniGameDropdown : MonoBehaviour
    {
        private const float PopupGap = 6f;
        private const float PopupPadding = 4f;
        private const float PopupScreenMargin = 12f;

        private readonly List<string> options = new List<string>();
        private readonly List<Button> itemButtons = new List<Button>();

        private RectTransform rectTransform;
        private RoundedRectGraphic background;
        private TextMeshProUGUI captionText;
        private TextMeshProUGUI arrowText;
        private Button button;
        private GameObject layerObject;
        private RectTransform popupRect;
        private Canvas rootCanvas;
        private Action<int> onValueChanged;
        private int value;
        private float popupWidth = 196f;
        private float itemHeight = 36f;
        private int maxVisibleItems = 5;
        private Color textColor = new Color(0.42f, 0.34f, 0.18f, 1f);
        private Color buttonColor = new Color(1f, 0.96f, 0.84f, 0.95f);
        private Color popupColor = new Color(1f, 0.98f, 0.90f, 0.98f);
        private Color selectedItemColor = new Color(0.95f, 0.84f, 0.56f, 0.55f);

        public int Value
        {
            get { return value; }
        }

        public void Configure(
            IList<string> optionTexts,
            int initialValue,
            Action<int> valueChanged,
            float width,
            float height,
            float optionHeight,
            int visibleItemCount,
            Color labelColor)
        {
            EnsureView();

            options.Clear();
            if (optionTexts != null)
            {
                for (var i = 0; i < optionTexts.Count; i++)
                {
                    options.Add(optionTexts[i]);
                }
            }

            popupWidth = Mathf.Max(80f, width);
            itemHeight = Mathf.Max(28f, optionHeight);
            maxVisibleItems = Mathf.Max(1, visibleItemCount);
            textColor = labelColor;
            onValueChanged = valueChanged;

            var layout = gameObject.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<LayoutElement>();
            }

            layout.preferredWidth = width;
            layout.preferredHeight = height;
            background.color = buttonColor;
            captionText.color = textColor;
            arrowText.color = textColor;
            SetValueWithoutNotify(initialValue);
        }

        public void SetValueWithoutNotify(int newValue)
        {
            value = options.Count <= 0 ? 0 : Mathf.Clamp(newValue, 0, options.Count - 1);
            RefreshCaption();
        }

        public void Close()
        {
            if (layerObject == null)
            {
                return;
            }

            Destroy(layerObject);
            layerObject = null;
            popupRect = null;
        }

        private void Awake()
        {
            EnsureView();
        }

        private void OnDisable()
        {
            Close();
        }

        private void OnDestroy()
        {
            Close();
            if (button != null)
            {
                button.onClick.RemoveListener(Toggle);
            }

            for (var i = 0; i < itemButtons.Count; i++)
            {
                if (itemButtons[i] != null)
                {
                    itemButtons[i].onClick.RemoveAllListeners();
                }
            }
        }

        private void EnsureView()
        {
            if (rectTransform != null)
            {
                return;
            }

            rectTransform = gameObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = gameObject.AddComponent<RectTransform>();
            }

            background = gameObject.GetComponent<RoundedRectGraphic>();
            if (background == null)
            {
                background = gameObject.AddComponent<RoundedRectGraphic>();
            }

            background.CornerRadius = 12f;
            background.color = buttonColor;

            button = gameObject.GetComponent<Button>();
            if (button == null)
            {
                button = gameObject.AddComponent<Button>();
            }

            button.targetGraphic = background;
            button.onClick.RemoveListener(Toggle);
            button.onClick.AddListener(Toggle);

            captionText = CreateText("Label", rectTransform, 17f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(captionText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 2f), new Vector2(-30f, -2f));
            captionText.enableAutoSizing = true;
            captionText.fontSizeMin = 14f;
            captionText.fontSizeMax = 17f;

            arrowText = CreateText("Arrow", rectTransform, 16f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(arrowText.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-26f, 2f), new Vector2(-8f, -2f));
            arrowText.text = "v";
        }

        private void Toggle()
        {
            if (layerObject != null)
            {
                Close();
                return;
            }

            Open();
        }

        private void Open()
        {
            if (options.Count <= 0)
            {
                return;
            }

            rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas == null)
            {
                return;
            }

            rootCanvas = rootCanvas.rootCanvas;
            CloseOtherDropdowns();
            var canvasRect = rootCanvas.transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            layerObject = CreateRectObject("MiniGameDropdownLayer", rootCanvas.transform);
            var layerRect = layerObject.GetComponent<RectTransform>();
            Stretch(layerRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            layerObject.transform.SetAsLastSibling();

            var blockerImage = layerObject.AddComponent<Image>();
            blockerImage.color = new Color(1f, 1f, 1f, 0.01f);
            var blockerButton = layerObject.AddComponent<Button>();
            blockerButton.targetGraphic = blockerImage;
            blockerButton.onClick.AddListener(Close);

            var popupObject = CreateRectObject("Popup", layerObject.transform);
            popupRect = popupObject.GetComponent<RectTransform>();
            popupRect.anchorMin = new Vector2(0.5f, 0.5f);
            popupRect.anchorMax = new Vector2(0.5f, 0.5f);
            popupRect.pivot = new Vector2(0f, 1f);
            var popupHeight = (Mathf.Min(options.Count, maxVisibleItems) * itemHeight) + (PopupPadding * 2f);
            popupRect.sizeDelta = new Vector2(popupWidth, popupHeight);

            var popupBackground = popupObject.AddComponent<RoundedRectGraphic>();
            popupBackground.color = popupColor;
            popupBackground.CornerRadius = 12f;

            var viewportObject = CreateRectObject("Viewport", popupRect);
            var viewport = viewportObject.GetComponent<RectTransform>();
            Stretch(viewport, Vector2.zero, Vector2.one, new Vector2(PopupPadding, PopupPadding), new Vector2(-PopupPadding, -PopupPadding));
            var viewportImage = viewportObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
            var mask = viewportObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var contentObject = CreateRectObject("Content", viewport);
            var content = contentObject.GetComponent<RectTransform>();
            Stretch(content, new Vector2(0f, 1f), Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);

            var contentLayout = contentObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = popupObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = options.Count > maxVisibleItems;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.viewport = viewport;
            scrollRect.content = content;

            BuildItems(content);
            PositionPopup(canvasRect, popupHeight);
        }

        private void BuildItems(Transform content)
        {
            itemButtons.Clear();
            for (var i = 0; i < options.Count; i++)
            {
                var optionIndex = i;
                var itemObject = CreateRectObject("Item" + i, content);
                var itemLayout = itemObject.AddComponent<LayoutElement>();
                itemLayout.minHeight = itemHeight;
                itemLayout.preferredHeight = itemHeight;
                itemLayout.flexibleHeight = 0f;

                var itemBackground = itemObject.AddComponent<Image>();
                itemBackground.color = i == value ? selectedItemColor : new Color(1f, 1f, 1f, 0.01f);
                var itemButton = itemObject.AddComponent<Button>();
                itemButton.targetGraphic = itemBackground;
                itemButton.onClick.AddListener(delegate { Select(optionIndex); });
                itemButtons.Add(itemButton);

                var label = CreateText("Label", itemObject.transform, 15f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 1f), new Vector2(-12f, -1f));
                label.text = options[i];
                label.color = textColor;
                label.enableAutoSizing = true;
                label.fontSizeMin = 12f;
                label.fontSizeMax = 15f;
            }
        }

        private void Select(int selectedIndex)
        {
            var clamped = Mathf.Clamp(selectedIndex, 0, options.Count - 1);
            var changed = clamped != value;
            value = clamped;
            RefreshCaption();
            Close();
            if (changed && onValueChanged != null)
            {
                onValueChanged(value);
            }
        }

        private void PositionPopup(RectTransform canvasRect, float popupHeight)
        {
            Canvas.ForceUpdateCanvases();

            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            var camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;

            Vector2 bottomLeft;
            Vector2 topLeft;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                RectTransformUtility.WorldToScreenPoint(camera, corners[0]),
                camera,
                out bottomLeft);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                RectTransformUtility.WorldToScreenPoint(camera, corners[1]),
                camera,
                out topLeft);

            var canvasBounds = canvasRect.rect;
            var openBelow = bottomLeft.y - popupHeight - PopupGap >= canvasBounds.yMin + PopupScreenMargin;
            popupRect.pivot = openBelow ? new Vector2(0f, 1f) : new Vector2(0f, 0f);

            var x = Mathf.Clamp(
                bottomLeft.x,
                canvasBounds.xMin + PopupScreenMargin,
                canvasBounds.xMax - popupWidth - PopupScreenMargin);
            var y = openBelow ? bottomLeft.y - PopupGap : topLeft.y + PopupGap;
            popupRect.anchoredPosition = new Vector2(x, y);
        }

        private void RefreshCaption()
        {
            if (captionText == null)
            {
                return;
            }

            captionText.text = options.Count <= 0 ? string.Empty : options[value];
        }

        private void CloseOtherDropdowns()
        {
            if (rootCanvas == null)
            {
                return;
            }

            var dropdowns = rootCanvas.GetComponentsInChildren<MiniGameDropdown>(true);
            for (var i = 0; i < dropdowns.Length; i++)
            {
                if (dropdowns[i] != null && dropdowns[i] != this)
                {
                    dropdowns[i].Close();
                }
            }
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            float fontSize,
            FontStyles fontStyle,
            TextAlignmentOptions alignment)
        {
            var textObject = CreateRectObject(name, parent);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            var font = MiniGameFontProvider.DefaultFont;
            if (font != null)
            {
                text.font = font;
            }

            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = new Color(0.42f, 0.34f, 0.18f, 1f);
            text.alignment = alignment;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            return text;
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
