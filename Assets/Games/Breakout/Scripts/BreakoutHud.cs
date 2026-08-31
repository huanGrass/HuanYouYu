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
            var bottomRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                bottomParent,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("BreakoutBottomHud"));
            bottomRoot = bottomRefs.Root;
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

            actionButton = MiniGameShellBottomBarBuilder.CreateTextActionButton(
                bottomRefs.ActionBar,
                "ActionButton",
                UiTextCatalog.Get("common.action.restart"),
                264f,
                74f,
                28f,
                24f);
            actionLabel = actionButton.GetComponentInChildren<TextMeshProUGUI>();

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

    }
}
