using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    internal sealed class BreakoutHud : IDisposable
    {
        private readonly RectTransform topRoot;
        private readonly RectTransform bottomRoot;
        private readonly TextMeshProUGUI titleText;
        private readonly TextMeshProUGUI scoreText;
        private readonly TextMeshProUGUI levelText;
        private readonly TextMeshProUGUI livesText;
        private readonly Button actionButton;
        private readonly TextMeshProUGUI actionLabel;

        public BreakoutHud(Transform topParent, Transform bottomParent)
        {
            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                topParent,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("BreakoutTop"));
            topRoot = topBarRefs.Root;
            bottomRoot = CreateRoot("BreakoutBottomHud", bottomParent);
            titleText = topBarRefs.TitleText;
            scoreText = topBarRefs.ScoreText;
            if (titleText == null || scoreText == null)
            {
                throw new InvalidOperationException("Breakout top structure is incomplete.");
            }

            livesText = CreateOverlayText(
                topRoot,
                "Lives",
                new Vector2(1f, 1f),
                new Vector2(-34f, -18f),
                new Vector2(1f, 1f),
                TextAlignmentOptions.Right,
                24f,
                new Color32(74, 99, 55, 255),
                UiTextCatalog.Format("breakout.hud.lives", 3));
            levelText = CreateOverlayText(
                scoreText.rectTransform,
                "Level",
                new Vector2(0.5f, 0f),
                new Vector2(0f, -16f),
                new Vector2(0.5f, 1f),
                TextAlignmentOptions.Center,
                16f,
                new Color32(126, 143, 112, 255),
                UiTextCatalog.Format(
                    "breakout.hud.level",
                    UiTextCatalog.Get("breakout.level.classic")));
            levelText.rectTransform.sizeDelta = new Vector2(220f, 24f);

            actionButton = CreateActionButton(bottomRoot, UiTextCatalog.Get("common.action.restart"));
            actionLabel = actionButton.GetComponentInChildren<TextMeshProUGUI>();
            actionLabel.color = new Color32(20, 28, 36, 255);

            actionButton.onClick.AddListener(OnActionClicked);
            MiniGameSfxPlayer.Attach(actionButton, MiniGameSfxType.UiTap, 0.92f);
        }

        public event Action ActionRequested;

        public RectTransform TopRoot
        {
            get { return topRoot; }
        }

        public RectTransform BottomRoot
        {
            get { return bottomRoot; }
        }

        public void SetTitle(string text)
        {
            titleText.text = string.IsNullOrEmpty(text) ? UiTextCatalog.Get("game.breakout.name") : text;
        }

        public void SetScore(int score)
        {
            scoreText.text = UiTextCatalog.Format("breakout.hud.score", score);
        }

        public void SetLevel(string levelName)
        {
            levelText.text = UiTextCatalog.Format("breakout.hud.level", levelName);
        }

        public void SetLives(int lives)
        {
            livesText.text = UiTextCatalog.Format("breakout.hud.lives", lives);
        }

        public void SetAction(string label, bool interactable, bool visible)
        {
            actionButton.gameObject.SetActive(visible);
            actionButton.interactable = interactable;
            actionLabel.text = label;
            var background = actionButton.targetGraphic as Image;
            if (background != null)
            {
                background.color = interactable
                    ? new Color32(245, 203, 94, 255)
                    : new Color32(136, 148, 166, 255);
            }
        }

        public void Dispose()
        {
            if (topRoot != null)
            {
                UnityEngine.Object.Destroy(topRoot.gameObject);
            }

            if (bottomRoot != null)
            {
                UnityEngine.Object.Destroy(bottomRoot.gameObject);
            }
        }

        private void OnActionClicked()
        {
            ActionRequested?.Invoke();
        }

        private static RectTransform CreateRoot(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static TextMeshProUGUI CreateOverlayText(
            RectTransform parent,
            string name,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 pivot,
            TextAlignmentOptions alignment,
            float fontSize,
            Color color,
            string fallbackText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(160f, 40f);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = alignment;
            text.color = color;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.text = fallbackText;
            return text;
        }

        private static Button CreateActionButton(RectTransform parent, string labelText)
        {
            var go = new GameObject("ActionButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 18f);
            rect.sizeDelta = new Vector2(264f, 74f);

            var image = go.GetComponent<Image>();
            image.color = new Color32(245, 203, 94, 255);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            labelRect.pivot = new Vector2(0.5f, 0.5f);

            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.fontSize = 28f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color32(20, 28, 36, 255);
            label.text = labelText;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            return button;
        }
    }
}
