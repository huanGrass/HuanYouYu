using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    internal sealed class MinesweeperCellView : MonoBehaviour
    {
        private static readonly Color CoveredColor = new Color(0.53f, 0.7f, 0.38f, 1f);
        private static readonly Color RevealedColor = new Color(0.95f, 0.92f, 0.82f, 1f);
        private static readonly Color FlagColor = new Color(0.95f, 0.74f, 0.31f, 1f);
        private static readonly Color MineColor = new Color(0.83f, 0.34f, 0.28f, 1f);
        private static readonly Color ExplodedMineColor = new Color(0.66f, 0.17f, 0.13f, 1f);
        private static readonly Color EmptyTextColor = new Color(0.31f, 0.38f, 0.17f, 1f);
        private static readonly Color ExplodedHighlightColor = new Color(0.94f, 0.46f, 0.38f, 1f);
        private const float RevealPulseDuration = 0.12f;
        private const float FlagPulseDuration = 0.10f;
        private const float ExplodedPulseDuration = 0.28f;
        private const float RevealPulseScale = 1.08f;
        private const float FlagPulseScale = 1.12f;
        private const float ExplodedPulseScale = 1.18f;
        private static readonly Color[] NumberColors =
        {
            new Color(0.22f, 0.43f, 0.8f, 1f),
            new Color(0.16f, 0.58f, 0.32f, 1f),
            new Color(0.75f, 0.28f, 0.22f, 1f),
            new Color(0.41f, 0.22f, 0.68f, 1f),
            new Color(0.63f, 0.18f, 0.19f, 1f),
            new Color(0.19f, 0.55f, 0.6f, 1f),
            new Color(0.4f, 0.4f, 0.4f, 1f),
            new Color(0.18f, 0.18f, 0.18f, 1f)
        };

        private Image background;
        private Button button;
        private TextMeshProUGUI label;
        private Action<int, int> clickHandler;
        private int x;
        private int y;
        private VisualState currentState;
        private int currentRevealedNumber = -1;
        private Coroutine activePulseRoutine;

        private enum VisualState
        {
            Covered,
            Flagged,
            Revealed,
            Mine,
            ExplodedMine
        }

        public static MinesweeperCellView Create(Transform parent, int x, int y, TMP_FontAsset fontAsset, Action<int, int> onPressed)
        {
            var cellObject = new GameObject("Cell_" + x + "_" + y, typeof(RectTransform), typeof(Image), typeof(Button), typeof(MinesweeperCellView));
            var rectTransform = cellObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.localScale = Vector3.one;

            var image = cellObject.GetComponent<Image>();
            image.color = CoveredColor;

            var button = cellObject.GetComponent<Button>();
            button.targetGraphic = image;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelTransform = labelObject.GetComponent<RectTransform>();
            labelTransform.SetParent(rectTransform, false);
            labelTransform.anchorMin = Vector2.zero;
            labelTransform.anchorMax = Vector2.one;
            labelTransform.offsetMin = Vector2.zero;
            labelTransform.offsetMax = Vector2.zero;

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = fontAsset;
            label.fontSize = 30f;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;

            var view = cellObject.GetComponent<MinesweeperCellView>();
            view.Initialize(image, button, label, x, y, onPressed);
            view.RenderCovered();
            return view;
        }

        public void SetInteractable(bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        public void RenderCovered()
        {
            ApplyVisualState(VisualState.Covered, AnimateMode.None);
            background.color = CoveredColor;
            label.text = string.Empty;
            label.color = EmptyTextColor;
        }

        public void RenderFlag()
        {
            ApplyVisualState(VisualState.Flagged, currentState == VisualState.Flagged ? AnimateMode.None : AnimateMode.PulseFlag);
            background.color = FlagColor;
            label.text = "F";
            label.color = new Color(0.41f, 0.23f, 0.07f, 1f);
        }

        public void RenderRevealed(int adjacentMineCount)
        {
            var shouldAnimate = currentState != VisualState.Revealed || currentRevealedNumber != adjacentMineCount;
            ApplyVisualState(VisualState.Revealed, shouldAnimate ? AnimateMode.PulseReveal : AnimateMode.None);
            background.color = RevealedColor;
            currentRevealedNumber = adjacentMineCount;
            if (adjacentMineCount <= 0)
            {
                label.text = string.Empty;
                label.color = EmptyTextColor;
                return;
            }

            label.text = adjacentMineCount.ToString();
            label.color = NumberColors[Mathf.Clamp(adjacentMineCount - 1, 0, NumberColors.Length - 1)];
        }

        public void RenderMine(bool exploded)
        {
            var targetState = exploded ? VisualState.ExplodedMine : VisualState.Mine;
            var animateMode = currentState == targetState
                ? AnimateMode.None
                : exploded ? AnimateMode.PulseExploded : AnimateMode.PulseReveal;
            ApplyVisualState(targetState, animateMode);
            background.color = exploded ? ExplodedMineColor : MineColor;
            label.text = "*";
            label.color = Color.white;
        }

        private void Initialize(Image backgroundImage, Button cellButton, TextMeshProUGUI textLabel, int cellX, int cellY, Action<int, int> onPressed)
        {
            background = backgroundImage;
            button = cellButton;
            label = textLabel;
            clickHandler = onPressed;
            x = cellX;
            y = cellY;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClicked);
        }

        private void ApplyVisualState(VisualState state, AnimateMode animateMode)
        {
            if (currentState == state && animateMode == AnimateMode.None)
            {
                return;
            }

            currentState = state;
            if (state != VisualState.Revealed)
            {
                currentRevealedNumber = -1;
            }

            StartPulse(animateMode);
        }

        private void StartPulse(AnimateMode animateMode)
        {
            if (activePulseRoutine != null)
            {
                StopCoroutine(activePulseRoutine);
                activePulseRoutine = null;
            }

            transform.localScale = Vector3.one;
            switch (animateMode)
            {
                case AnimateMode.PulseReveal:
                    activePulseRoutine = StartCoroutine(AnimatePulse(RevealPulseDuration, RevealPulseScale, null, default(Color), false));
                    break;
                case AnimateMode.PulseFlag:
                    activePulseRoutine = StartCoroutine(AnimatePulse(FlagPulseDuration, FlagPulseScale, null, default(Color), false));
                    break;
                case AnimateMode.PulseExploded:
                    activePulseRoutine = StartCoroutine(AnimatePulse(ExplodedPulseDuration, ExplodedPulseScale, background, ExplodedHighlightColor, true));
                    break;
            }
        }

        private IEnumerator AnimatePulse(float duration, float peakScale, Graphic targetGraphic, Color targetColor, bool restoreColor)
        {
            var halfDuration = Mathf.Max(0.01f, duration * 0.5f);
            var originalColor = targetGraphic != null ? targetGraphic.color : default;
            var elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = EaseOutCubic(Mathf.Clamp01(elapsed / halfDuration));
                transform.localScale = Vector3.one * Mathf.Lerp(1f, peakScale, progress);
                if (targetGraphic != null)
                {
                    targetGraphic.color = Color.LerpUnclamped(originalColor, targetColor, progress);
                }

                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = EaseOutCubic(Mathf.Clamp01(elapsed / halfDuration));
                transform.localScale = Vector3.one * Mathf.Lerp(peakScale, 1f, progress);
                if (targetGraphic != null && restoreColor)
                {
                    targetGraphic.color = Color.LerpUnclamped(targetColor, originalColor, progress);
                }

                yield return null;
            }

            transform.localScale = Vector3.one;
            if (targetGraphic != null)
            {
                targetGraphic.color = restoreColor ? originalColor : targetGraphic.color;
            }

            activePulseRoutine = null;
        }

        private static float EaseOutCubic(float value)
        {
            var inverse = 1f - value;
            return 1f - (inverse * inverse * inverse);
        }

        private enum AnimateMode
        {
            None,
            PulseReveal,
            PulseFlag,
            PulseExploded
        }

        private void HandleClicked()
        {
            clickHandler?.Invoke(x, y);
        }
    }
}
