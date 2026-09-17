using UnityEngine;
using UnityEngine.EventSystems;

namespace HuanYouYu.MiniGameHall
{
    public sealed class BubblePopBoard : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const int Columns = 6;
        public const int Rows = 7;
        public int ColumnCount { get; private set; } = Columns;
        public int RowCount { get; private set; } = Rows;
        public int ColorMode { get; private set; }
        public int ReboundMode { get; private set; }
        private float[] reboundRemaining;
        private RoundedRectGraphic tray;
        private BubblePopGraphic[] faces;
        private readonly System.Collections.Generic.List<BubblePopGraphic> bubblePool = new System.Collections.Generic.List<BubblePopGraphic>();
        private float[] refillAnimation;
        private readonly AudioClip[] popClips = new AudioClip[5];
        private readonly AudioClip[] reboundClips = new AudioClip[5];
        private float reboundSoundCooldown;
        private bool initialized;
        private bool[] pressed;
        private float[] animation;
        private bool paused;
        private int? pointer;
        private Vector2 previous;

        private int palette;
        public int PressedCount { get; private set; }

        public void Build()
        {
            tray = gameObject.AddComponent<RoundedRectGraphic>();
            tray.color = new Color(0.92f, 0.9f, 0.96f);
            tray.CornerRadius = 38f;
            tray.raycastTarget = true;
            for (var i = 0; i < popClips.Length; i++)
            {
                var tone = 0.94f + i * 0.03f;
                popClips[i] = CreatePopClip(tone);
                reboundClips[i] = CreateReboundClip(tone);
            }
            BuildBubbles();
        }

        private void BuildBubbles()
        {
            var wasActive = gameObject.activeSelf;
            gameObject.SetActive(false);
            try
            {
                faces = new BubblePopGraphic[ColumnCount * RowCount];
                refillAnimation = new float[faces.Length];
                pressed = new bool[faces.Length];
                animation = new float[faces.Length];
                reboundRemaining = new float[faces.Length];
                for (var i = 0; i < faces.Length; i++)
                {
                    if (i == bubblePool.Count)
                    {
                        BubblePopGraphic graphic;
                        if (bubblePool.Count == 0)
                        {
                            var bubble = new GameObject("Bubble_0", typeof(RectTransform));
                            bubble.transform.SetParent(transform, false);
                            graphic = bubble.AddComponent<BubblePopGraphic>();
                            graphic.raycastTarget = false;
                        }
                        else
                        {
                            graphic = Instantiate(bubblePool[0], transform, false);
                            graphic.name = "Bubble_" + i;
                        }
                        bubblePool.Add(graphic);
                    }
                    faces[i] = bubblePool[i];
                    var rect = faces[i].rectTransform;
                    var center = new Vector2((i % ColumnCount + 0.5f) / ColumnCount,
                        1f - (i / ColumnCount + 0.5f) / RowCount);
                    rect.anchorMin = center - new Vector2(0.475f / ColumnCount, 0.475f / RowCount);
                    rect.anchorMax = center + new Vector2(0.475f / ColumnCount, 0.475f / RowCount);
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    faces[i].gameObject.SetActive(true);
                }
                for (var i = faces.Length; i < bubblePool.Count; i++) bubblePool[i].gameObject.SetActive(false);
            }
            finally { gameObject.SetActive(wasActive); }
        }
        public void SetSize(int columns, int rows)
        {
            if (!((columns == 4 && rows == 5) || (columns == 6 && rows == 7) || (columns == 8 && rows == 9)
                || (columns == 12 && rows == 16) || (columns == 20 && rows == 24)))
                throw new System.ArgumentOutOfRangeException(nameof(columns));
            if (columns == ColumnCount && rows == RowCount) return;
            CancelGesture();
            ColumnCount = columns;
            RowCount = rows;
            BuildBubbles();
            ResetBoard();
        }

        public void SetReboundMode(int mode)
        {
            if (mode < 0 || mode > 3) throw new System.ArgumentOutOfRangeException(nameof(mode));
            ReboundMode = mode;
            for (var i = 0; i < pressed.Length; i++)
                reboundRemaining[i] = pressed[i] ? ReboundDelay : 0f;
        }

        private float ReboundDelay => ReboundMode == 1 ? 5f : ReboundMode == 2 ? 1f : ReboundMode == 3 ? 0.28f : 0f;
        public void SetColorMode(int mode)
        {
            if (mode < 0 || mode > 6) throw new System.ArgumentOutOfRangeException(nameof(mode));
            ColorMode = mode;
            tray.color = mode < 2 ? new Color(0.92f, 0.9f, 0.96f)
                : Color.Lerp(BubbleColor(0), Color.white, 0.7f);
            for (var i = 0; i < faces.Length; i++) RenderBubble(i);
        }

        private Color BubbleColor(int index)
        {
            var hue = ((index / ColumnCount + palette) % RowCount) / (float)RowCount;
            switch (ColorMode)
            {
                case 1: hue = ((index * 3 + index / ColumnCount + palette) % 7) / 7f; break;
                case 2: hue = 0.56f; break;
                case 3: hue = 0.40f; break;
                case 4: hue = 0.91f; break;
                case 5: hue = 0.73f; break;
                case 6: hue = 0.13f; break;
            }
            return Color.HSVToRGB(hue, 0.38f, 0.96f);
        }
        public void ResetBoard()
        {
            pointer = null;
            PressedCount = 0;
            reboundSoundCooldown = 0f;

            palette = (palette + 1) % RowCount;
            for (var i = 0; i < pressed.Length; i++)
            {
                pressed[i] = false;
                reboundRemaining[i] = 0f;
                animation[i] = 0f;
                refillAnimation[i] = initialized ? 0.42f + (i / ColumnCount) * 0.035f + (i % ColumnCount) * 0.018f : 0f;
                RenderBubble(i);
            }
            initialized = true;
        }

        public void CancelGesture() { pointer = null; }
        public void SetPaused(bool value) { paused = value; pointer = null; }
        private void OnDisable() { pointer = null; }
        private void OnApplicationFocus(bool focused) { if (!focused) pointer = null; }
        public void Tick(float deltaTime)
        {
            if (paused || deltaTime <= 0f) return;
            reboundSoundCooldown = Mathf.Max(0f, reboundSoundCooldown - deltaTime);
            var reboundIndex = -1;
            for (var i = 0; i < animation.Length; i++)
            {
                var redraw = animation[i] > 0f || refillAnimation[i] > 0f;
                animation[i] = Mathf.Max(0f, animation[i] - deltaTime);
                refillAnimation[i] = Mathf.Max(0f, refillAnimation[i] - deltaTime);
                if (pressed[i] && ReboundMode != 0)
                {
                    reboundRemaining[i] = Mathf.Max(0f, reboundRemaining[i] - deltaTime);
                    if (reboundRemaining[i] <= 0f)
                    {
                        pressed[i] = false;
                        PressedCount--;
                        reboundIndex = i;
                        animation[i] = 0f;
                        refillAnimation[i] = 0.2f;
                        redraw = true;
                    }
                }
                if (redraw) RenderBubble(i);
            }
            if (reboundIndex >= 0 && reboundSoundCooldown <= 0f)
            {
                MiniGameSfxPlayer.Play(reboundClips[SoundVariant(reboundIndex)], ReboundMode == 3 ? 0.28f : 0.42f);
                reboundSoundCooldown = 0.07f;
            }
        }
        private void RenderBubble(int i)
        {
            var tint = BubbleColor(i);
            var elapsed = 0.28f - animation[i];
            var depression = pressed[i] ? Mathf.SmoothStep(0f, 1f, elapsed / 0.075f) : 0f;
            var recoil = pressed[i] ? Mathf.Sin(Mathf.Clamp01(elapsed / 0.28f) * Mathf.PI) : 0f;
            var refill = Mathf.Clamp01(1f - refillAnimation[i] / 0.42f);
            var reveal = refillAnimation[i] > 0f && !pressed[i]
                ? 0.9f + 0.1f * refill + 0.06f * Mathf.Sin(refill * Mathf.PI) : 1f;
            faces[i].SetSurface(tint, depression, pressed[i] ? animation[i] / 0.28f : 0f);
            faces[i].rectTransform.localScale = new Vector3(
                (1f - 0.045f * depression + recoil * 0.045f) * reveal,
                (1f - 0.045f * depression - recoil * 0.07f) * reveal, 1f);
            faces[i].rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(2f, -2f, depression));
        }

        private int SoundVariant(int index) { return (index * 7 + index / ColumnCount * 3) % popClips.Length; }

        // 微信适配层以 44100 Hz 返回 PCM 长度；其他采样率会使 SetData 写入长度不匹配。
        private static AudioClip CreatePopClip(float tone)
        {
            const int rate = 44100;
            var samples = new float[Mathf.CeilToInt(rate * 0.09f)];
            var phase = 0f;
            uint noise = 731u;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)rate;
                phase += 2f * Mathf.PI * (170f + 650f * Mathf.Exp(-t * 85f)) * tone / rate;
                noise = unchecked(noise * 1664525u + 1013904223u);
                var click = ((noise >> 8) / 8388607.5f - 1f) * Mathf.Exp(-t * 380f) * 0.16f;
                var envelope = Mathf.Min(1f, t * 1400f) * Mathf.Exp(-t * 65f)
                    * Mathf.Clamp01((0.09f - t) / 0.012f);
                samples[i] = (Mathf.Sin(phase) * 0.65f + click) * envelope;
            }
            var clip = AudioClip.Create("BubblePopSoftPop", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateReboundClip(float tone)
        {
            const int rate = 44100;
            var samples = new float[Mathf.CeilToInt(rate * 0.08f)];
            var phase = 0f;
            uint noise = 193u;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)rate;
                phase += 2f * Mathf.PI * (380f + 400f * (1f - Mathf.Exp(-t * 65f))) * tone / rate;
                noise = unchecked(noise * 1664525u + 1013904223u);
                var air = ((noise >> 8) / 8388607.5f - 1f) * Mathf.Exp(-t * 100f) * 0.07f;
                var envelope = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 48f)
                    * Mathf.Clamp01((0.08f - t) / 0.015f);
                samples[i] = (Mathf.Sin(phase) * 0.55f + air) * envelope;
            }
            var clip = AudioClip.Create("BubblePopSoftRebound", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        private void OnDestroy()
        {
            for (var i = 0; i < popClips.Length; i++)
            {
                if (popClips[i] != null) Destroy(popClips[i]);
                if (reboundClips[i] != null) Destroy(reboundClips[i]);
            }
        }
        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold = false; }
        public void OnBeginDrag(PointerEventData e) { }
        public void OnEndDrag(PointerEventData e) { OnPointerUp(e); }
        public void OnPointerDown(PointerEventData e)
        {
            if (paused || pointer.HasValue || e.button != PointerEventData.InputButton.Left) return;
            if (!LocalPoint(e, out previous)) return;
            pointer = e.pointerId;
            PressSegment(previous, previous);
        }
        public void OnDrag(PointerEventData e)
        {
            if (paused || pointer != e.pointerId || !LocalPoint(e, out var next)) return;
            PressSegment(previous, next);
            previous = next;
        }
        public void OnPointerUp(PointerEventData e)
        { if (pointer == e.pointerId) pointer = null; }
        private bool LocalPoint(PointerEventData e, out Vector2 point)
        { return RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,
            e.position, e.pressEventCamera, out point); }

        private void PressSegment(Vector2 start, Vector2 end)
        {
            var rect = ((RectTransform)transform).rect;
            var radius = Mathf.Min(rect.width / ColumnCount, rect.height / RowCount) * 0.445f;
            var segment = end - start;
            var soundIndex = -1;
            for (var i = 0; i < pressed.Length; i++)
            {
                if (pressed[i]) continue;
                var center = (Vector2)faces[i].rectTransform.localPosition;
                var t = segment.sqrMagnitude > 0f
                    ? Mathf.Clamp01(Vector2.Dot(center - start, segment) / segment.sqrMagnitude) : 0f;
                if ((center - (start + segment * t)).sqrMagnitude > radius * radius) continue;
                pressed[i] = true;
                PressedCount++;
                animation[i] = 0.28f;
                reboundRemaining[i] = ReboundDelay;
                RenderBubble(i);
                soundIndex = i;
            }
            if (soundIndex >= 0) MiniGameSfxPlayer.Play(popClips[SoundVariant(soundIndex)], 0.65f);
        }
    }
}
