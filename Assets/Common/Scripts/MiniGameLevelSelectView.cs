using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    internal sealed class MiniGameLevelSelectView : IDisposable
    {
        private readonly GameObject root;
        private readonly Button blockerButton;
        private readonly Button closeButton;
        private readonly Button[] levelButtons;

        private MiniGameLevelSelectView(GameObject rootObject, Button blocker, Button close, Button[] buttons)
        {
            root = rootObject;
            blockerButton = blocker;
            closeButton = close;
            levelButtons = buttons ?? Array.Empty<Button>();
        }

        public static MiniGameLevelSelectView Create(
            Transform parent,
            TMP_FontAsset fontAsset,
            int levelCount,
            int currentLevelIndex,
            int unlockedLevelCount,
            string rootName,
            string buttonNamePrefix,
            Action<int> onSelect,
            Action onClose)
        {
            var root = new GameObject(ResolveName(rootName, "MiniGameLevelSelectPanel"), typeof(RectTransform), typeof(CanvasRenderer));
            root.transform.SetParent(parent, false);
            var rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var blockerGraphic = root.AddComponent<RoundedRectGraphic>();
            blockerGraphic.color = new Color(0.13f, 0.28f, 0.16f, 0.42f);
            blockerGraphic.CornerRadius = 0f;
            blockerGraphic.raycastTarget = true;
            var blocker = root.AddComponent<Button>();
            blocker.targetGraphic = blockerGraphic;
            blocker.onClick.AddListener(delegate { onClose?.Invoke(); });

            var dialog = CreateRectObject("Dialog", rootRect);
            var dialogRect = dialog.GetComponent<RectTransform>();
            dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.pivot = new Vector2(0.5f, 0.5f);
            dialogRect.sizeDelta = new Vector2(560f, 640f);
            dialogRect.anchoredPosition = Vector2.zero;
            var dialogGraphic = EnsureRoundedRectGraphic(dialog, new Color32(246, 252, 247, 255), 34f, true);
            dialogGraphic.raycastTarget = true;

            var title = CreatePanelText(dialogRect, "Title", UiTextCatalog.Get("level_select.title"), fontAsset, 36f, FontStyles.Bold, new Color32(50, 132, 63, 255));
            title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(360f, 58f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -34f);
            title.alignment = TextAlignmentOptions.Center;

            var close = CreateLevelButton(
                dialogRect,
                "CloseButton",
                UiTextCatalog.Get("level_select.close"),
                fontAsset,
                new Vector2(0f, -572f),
                new Vector2(206f, 58f),
                true,
                false,
                delegate { onClose?.Invoke(); });

            var viewportObject = CreateRectObject("LevelViewport", dialogRect);
            var viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0.5f, 1f);
            viewportRect.anchorMax = new Vector2(0.5f, 1f);
            viewportRect.pivot = new Vector2(0.5f, 1f);
            viewportRect.sizeDelta = new Vector2(420f, 410f);
            viewportRect.anchoredPosition = new Vector2(0f, -112f);
            var viewportHitArea = EnsureRoundedRectGraphic(viewportObject, new Color(1f, 1f, 1f, 0f), 0f, true);
            viewportHitArea.raycastTarget = true;
            var viewportBlocker = viewportObject.AddComponent<Button>();
            viewportBlocker.targetGraphic = viewportHitArea;
            viewportBlocker.transition = Selectable.Transition.None;
            viewportObject.AddComponent<RectMask2D>();

            var scrollRect = viewportObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.scrollSensitivity = 38f;
            scrollRect.viewport = viewportRect;

            var gridObject = CreateRectObject("LevelGrid", viewportRect);
            var gridRect = gridObject.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0f, 1f);
            gridRect.anchorMax = new Vector2(1f, 1f);
            gridRect.pivot = new Vector2(0.5f, 1f);
            var rowCount = Mathf.CeilToInt(Mathf.Max(0, levelCount) / 5f);
            var contentHeight = Mathf.Max(410f, rowCount * 66f + Mathf.Max(0, rowCount - 1) * 12f);
            gridRect.sizeDelta = new Vector2(0f, contentHeight);
            gridRect.anchoredPosition = Vector2.zero;
            scrollRect.content = gridRect;

            var grid = gridObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.spacing = new Vector2(12f, 12f);
            grid.cellSize = new Vector2(72f, 66f);

            var buttons = new Button[Mathf.Max(0, levelCount)];
            var prefix = ResolveName(buttonNamePrefix, "MiniGameLevelButton_");
            for (var i = 0; i < buttons.Length; i++)
            {
                var levelIndex = i;
                var unlocked = i < unlockedLevelCount;
                var label = (i + 1).ToString();
                buttons[i] = CreateLevelButton(
                    gridRect,
                    prefix + (i + 1),
                    label,
                    fontAsset,
                    Vector2.zero,
                    new Vector2(72f, 66f),
                    unlocked,
                    i == currentLevelIndex,
                    delegate { onSelect?.Invoke(levelIndex); });
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(gridRect);
            ApplyInitialScrollPosition(scrollRect, grid, viewportRect, gridRect, currentLevelIndex, buttons.Length);
            return new MiniGameLevelSelectView(root, blocker, close, buttons);
        }

        public void Dispose()
        {
            if (blockerButton != null)
            {
                blockerButton.onClick.RemoveAllListeners();
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
            }

            for (var i = 0; i < levelButtons.Length; i++)
            {
                if (levelButtons[i] != null)
                {
                    levelButtons[i].onClick.RemoveAllListeners();
                }
            }

            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        private static Button CreateLevelButton(
            RectTransform parent,
            string name,
            string labelText,
            TMP_FontAsset fontAsset,
            Vector2 anchoredPosition,
            Vector2 size,
            bool interactable,
            bool current,
            Action onClick)
        {
            var buttonObject = CreateRectObject(name, parent);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            var backgroundColor = current
                ? new Color32(62, 155, 188, 255)
                : interactable
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(214, 222, 224, 255);
            var background = EnsureRoundedRectGraphic(buttonObject, backgroundColor, 18f, true);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;
            ConfigureButtonColors(button);
            button.interactable = interactable;
            if (interactable)
            {
                button.onClick.AddListener(delegate { onClick?.Invoke(); });
                MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.9f);
            }

            var label = CreatePanelText(rect, "Label", labelText, fontAsset, 24f, FontStyles.Bold, current ? Color.white : new Color32(58, 83, 94, 255));
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
            return button;
        }

        private static TextMeshProUGUI CreatePanelText(
            Transform parent,
            string name,
            string text,
            TMP_FontAsset fontAsset,
            float fontSize,
            FontStyles style,
            Color color)
        {
            var textObject = CreateRectObject(name, parent);
            var label = textObject.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null)
            {
                label.font = fontAsset;
            }

            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            return label;
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

        private static void ApplyInitialScrollPosition(
            ScrollRect scrollRect,
            GridLayoutGroup grid,
            RectTransform viewportRect,
            RectTransform contentRect,
            int currentLevelIndex,
            int levelCount)
        {
            if (scrollRect == null || grid == null || viewportRect == null || contentRect == null || levelCount <= 0)
            {
                return;
            }

            var overflowHeight = contentRect.rect.height - viewportRect.rect.height;
            if (overflowHeight <= 0.01f)
            {
                scrollRect.verticalNormalizedPosition = 1f;
                return;
            }

            var levelIndex = Mathf.Clamp(currentLevelIndex, 0, levelCount - 1);
            var rowIndex = levelIndex / Mathf.Max(1, grid.constraintCount);
            var rowStride = grid.cellSize.y + grid.spacing.y;
            var rowTop = rowIndex * rowStride;
            var centeredTop = rowTop - ((viewportRect.rect.height - grid.cellSize.y) * 0.5f);
            var scrollTop = Mathf.Clamp(centeredTop, 0f, overflowHeight);
            scrollRect.verticalNormalizedPosition = 1f - (scrollTop / overflowHeight);
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static RoundedRectGraphic EnsureRoundedRectGraphic(GameObject target, Color color, float radius, bool raycastTarget)
        {
            if (target.GetComponent<CanvasRenderer>() == null)
            {
                target.AddComponent<CanvasRenderer>();
            }

            var graphic = target.GetComponent<RoundedRectGraphic>();
            if (graphic == null)
            {
                graphic = target.AddComponent<RoundedRectGraphic>();
            }

            graphic.color = color;
            graphic.CornerRadius = radius;
            graphic.raycastTarget = raycastTarget;
            return graphic;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private static string ResolveName(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
