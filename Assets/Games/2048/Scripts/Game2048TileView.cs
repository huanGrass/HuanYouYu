using System.Collections;
using TMPro;
using UnityEngine;
using HuanYouYu.MiniGameHall;

namespace HuanYouYu.Game2048
{
    public sealed class Game2048TileView : MonoBehaviour
    {
        private const float SpawnPulseDuration = 0.16f;
        private const float MergePulseDuration = 0.20f;
        private const float SpawnPulseScale = 1.12f;
        private const float MergePulseScale = 1.18f;
        private const float SpawnHighlightAmount = 0.16f;
        private const float MergeHighlightAmount = 0.22f;

        [SerializeField] private RoundedRectGraphic background;
        [SerializeField] private TextMeshProUGUI valueLabel;
        private Coroutine activePulseRoutine;
        private Color boundBackgroundColor;

        public void Initialize(RoundedRectGraphic backgroundGraphic, TextMeshProUGUI label)
        {
            background = backgroundGraphic;
            valueLabel = label;
        }

        public void Bind(int value)
        {
            if (background == null || valueLabel == null)
            {
                return;
            }

            boundBackgroundColor = ResolveBackgroundColor(value);
            background.color = boundBackgroundColor;
            valueLabel.text = value <= 0 ? string.Empty : value.ToString();
            valueLabel.color = value <= 4 ? new Color32(119, 110, 101, 255) : Color.white;
            valueLabel.fontSize = ResolveFontSize(value);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void ResetAnimationState()
        {
            if (activePulseRoutine != null)
            {
                StopCoroutine(activePulseRoutine);
                activePulseRoutine = null;
            }

            transform.localScale = Vector3.one;
            if (background != null)
            {
                background.color = boundBackgroundColor;
            }
        }

        public void PlaySpawnPulse()
        {
            StartPulse(SpawnPulseDuration, SpawnPulseScale, SpawnHighlightAmount);
        }

        public void PlayMergePulse()
        {
            StartPulse(MergePulseDuration, MergePulseScale, MergeHighlightAmount);
        }

        private void StartPulse(float duration, float peakScale, float highlightAmount)
        {
            if (!isActiveAndEnabled || background == null)
            {
                return;
            }

            if (activePulseRoutine != null)
            {
                StopCoroutine(activePulseRoutine);
            }

            activePulseRoutine = StartCoroutine(AnimatePulse(duration, peakScale, highlightAmount));
        }

        private IEnumerator AnimatePulse(float duration, float peakScale, float highlightAmount)
        {
            var halfDuration = Mathf.Max(0.01f, duration * 0.5f);
            var highlightColor = Color.Lerp(boundBackgroundColor, Color.white, Mathf.Clamp01(highlightAmount));
            var elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = EaseOutCubic(Mathf.Clamp01(elapsed / halfDuration));
                transform.localScale = Vector3.one * Mathf.Lerp(1f, peakScale, progress);
                background.color = Color.LerpUnclamped(boundBackgroundColor, highlightColor, progress);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = EaseOutCubic(Mathf.Clamp01(elapsed / halfDuration));
                transform.localScale = Vector3.one * Mathf.Lerp(peakScale, 1f, progress);
                background.color = Color.LerpUnclamped(highlightColor, boundBackgroundColor, progress);
                yield return null;
            }

            transform.localScale = Vector3.one;
            background.color = boundBackgroundColor;
            activePulseRoutine = null;
        }

        private static float EaseOutCubic(float value)
        {
            var inverse = 1f - value;
            return 1f - (inverse * inverse * inverse);
        }

        private static float ResolveFontSize(int value)
        {
            if (value < 100)
            {
                return 62f;
            }

            if (value < 1000)
            {
                return 54f;
            }

            return value < 10000 ? 44f : 36f;
        }

        private static Color ResolveBackgroundColor(int value)
        {
            switch (value)
            {
                case 0:
                    return new Color32(205, 193, 180, 255);
                case 2:
                    return new Color32(238, 228, 218, 255);
                case 4:
                    return new Color32(237, 224, 200, 255);
                case 8:
                    return new Color32(242, 177, 121, 255);
                case 16:
                    return new Color32(245, 149, 99, 255);
                case 32:
                    return new Color32(246, 124, 95, 255);
                case 64:
                    return new Color32(246, 94, 59, 255);
                case 128:
                    return new Color32(237, 207, 114, 255);
                case 256:
                    return new Color32(237, 204, 97, 255);
                case 512:
                    return new Color32(237, 200, 80, 255);
                case 1024:
                    return new Color32(237, 197, 63, 255);
                case 2048:
                    return new Color32(237, 194, 46, 255);
                default:
                    return new Color32(60, 58, 50, 255);
            }
        }
    }
}
