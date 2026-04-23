using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    internal sealed class MiniGamePauseToggleView
    {
        private static readonly Color ToggleOnColor = new Color(0.51f, 0.80f, 0.14f, 1f);
        private static readonly Color ToggleOffColor = new Color(0.84f, 0.81f, 0.75f, 1f);
        private static readonly Color ToggleOnTextColor = Color.white;
        private static readonly Color ToggleOffTextColor = new Color(0.96f, 0.95f, 0.91f, 1f);
        private static readonly Vector2 LabelOffsetMinOn = new Vector2(12f, 0f);
        private static readonly Vector2 LabelOffsetMaxOn = new Vector2(-60f, 0f);
        private static readonly Vector2 LabelOffsetMinOff = new Vector2(60f, 0f);
        private static readonly Vector2 LabelOffsetMaxOff = new Vector2(-12f, 0f);
        private const float AnimationDuration = 0.14f;

        private readonly Button button;
        private readonly Graphic trackGraphic;
        private readonly TextMeshProUGUI labelText;
        private readonly RectTransform labelRect;
        private readonly RectTransform knobRect;
        private readonly Vector2 knobOnPosition;
        private readonly Vector2 knobOffPosition;
        private readonly UiTweenRunner tweenRunner;
        private Coroutine activeTween;

        private MiniGamePauseToggleView(
            Button toggleButton,
            Graphic track,
            TextMeshProUGUI label,
            RectTransform labelTransform,
            RectTransform knobTransform,
            Vector2 onPosition,
            Vector2 offPosition,
            UiTweenRunner runner)
        {
            button = toggleButton;
            trackGraphic = track;
            labelText = label;
            labelRect = labelTransform;
            knobRect = knobTransform;
            knobOnPosition = onPosition;
            knobOffPosition = offPosition;
            tweenRunner = runner;
        }

        public bool IsOn { get; private set; }

        public static MiniGamePauseToggleView Create(Transform row, UiTweenRunner runner)
        {
            if (row == null)
            {
                return null;
            }

            var button = row.Find("ToggleButton")?.GetComponent<Button>();
            var track = row.Find("ToggleButton")?.GetComponent<Graphic>();
            var label = row.Find("ToggleButton/Label")?.GetComponent<TextMeshProUGUI>();
            var knobRect = row.Find("ToggleButton/Knob") as RectTransform;
            var labelRect = label != null ? label.rectTransform : null;
            if (button == null || track == null || label == null || knobRect == null || labelRect == null)
            {
                return null;
            }

            var buttonRect = button.GetComponent<RectTransform>();
            var travel = Mathf.Max(0f, ((buttonRect.rect.width - knobRect.rect.width) * 0.5f) - 6f);
            return new MiniGamePauseToggleView(
                button,
                track,
                label,
                labelRect,
                knobRect,
                new Vector2(travel, 0f),
                new Vector2(-travel, 0f),
                runner);
        }

        public void Bind(bool isOn, bool animate)
        {
            IsOn = isOn;
            labelText.text = UiTextCatalog.GetOrFallback(
                isOn ? "popup.pause.toggle_on" : "popup.pause.toggle_off",
                isOn ? "On" : "Off");
            labelText.color = isOn ? ToggleOnTextColor : ToggleOffTextColor;
            trackGraphic.color = isOn ? ToggleOnColor : ToggleOffColor;
            labelRect.offsetMin = isOn ? LabelOffsetMinOn : LabelOffsetMinOff;
            labelRect.offsetMax = isOn ? LabelOffsetMaxOn : LabelOffsetMaxOff;

            var targetPosition = isOn ? knobOnPosition : knobOffPosition;
            if (!animate || tweenRunner == null)
            {
                knobRect.anchoredPosition = targetPosition;
                return;
            }

            if (activeTween != null)
            {
                tweenRunner.StopCoroutine(activeTween);
            }

            activeTween = tweenRunner.Run(AnimateKnob(targetPosition));
        }

        public void BindToggle(Action action, MiniGameSfxType sfxType, float volumeScale)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(delegate
            {
                action?.Invoke();
            });
            MiniGameSfxPlayer.Attach(button, sfxType, volumeScale);
        }

        private IEnumerator AnimateKnob(Vector2 targetPosition)
        {
            var startPosition = knobRect.anchoredPosition;
            if ((startPosition - targetPosition).sqrMagnitude <= 0.0001f)
            {
                knobRect.anchoredPosition = targetPosition;
                activeTween = null;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < AnimationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / AnimationDuration);
                t = 1f - Mathf.Pow(1f - t, 3f);
                knobRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, t);
                yield return null;
            }

            knobRect.anchoredPosition = targetPosition;
            activeTween = null;
        }
    }
}
