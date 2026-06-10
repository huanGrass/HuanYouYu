using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed class MiniGameTutorialStep
    {
        public RectTransform Target;
        public Func<RectTransform> ResolveTarget;
        public string Title;
        public string TitleKey;
        public string Message;
        public string MessageKey;
        public bool RequireTargetClick;
        public Action OnTargetClick;
        public Vector2 Padding = new Vector2(18f, 18f);
    }

    public sealed class MiniGameTutorialOverlay : IDisposable
    {
        private const float BubbleWidth = 520f;
        private const float BubbleMinHeight = 315f;
        private const float BubbleGap = 34f;
        private const float ScreenMargin = 24f;
        private const float HighlightMinSize = 72f;
        private const float BubbleHorizontalPadding = 38f;
        private const float MessageWidthPadding = BubbleHorizontalPadding * 2f;

        private readonly IList<MiniGameTutorialStep> steps;
        private readonly Action onCompleted;
        private readonly GameObject root;
        private readonly RectTransform rootRect;
        private readonly RectTransform[] dimRects = new RectTransform[4];
        private readonly RoundedHoleDimGraphic dimHoleGraphic;
        private readonly RectTransform targetClickRect;
        private readonly RectTransform bubbleRect;
        private readonly RectTransform pointerRect;
        private readonly TextMeshProUGUI pageText;
        private readonly TextMeshProUGUI titleText;
        private readonly TextMeshProUGUI messageText;
        private readonly TextMeshProUGUI nextLabelText;
        private readonly TextMeshProUGUI skipLabelText;
        private readonly Button nextButton;
        private readonly Button skipButton;
        private readonly Button targetClickButton;
        private readonly TutorialOverlayUpdater updater;
        private int currentIndex;
        private bool disposed;

        private MiniGameTutorialOverlay(Transform parent, IList<MiniGameTutorialStep> tutorialSteps, Action completed)
        {
            steps = tutorialSteps;
            onCompleted = completed;

            root = new GameObject("MiniGameTutorialOverlay", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            updater = root.AddComponent<TutorialOverlayUpdater>();
            updater.Bind(this);

            dimHoleGraphic = CreateRoundedHoleDim(rootRect);
            for (var i = 0; i < dimRects.Length; i++)
            {
                dimRects[i] = CreateImageRect("Dim_" + i, rootRect, new Color(0f, 0f, 0f, 0f), false);
            }

            targetClickRect = CreateTargetClickLayer(rootRect);
            targetClickButton = targetClickRect.GetComponent<Button>();
            targetClickButton.onClick.AddListener(HandleTargetClick);

            bubbleRect = CreateBubble(rootRect);
            pointerRect = bubbleRect.Find("Pointer") as RectTransform;
            pageText = bubbleRect.Find("PageBadge/PageText")?.GetComponent<TextMeshProUGUI>();
            titleText = bubbleRect.Find("Title")?.GetComponent<TextMeshProUGUI>();
            messageText = bubbleRect.Find("Message")?.GetComponent<TextMeshProUGUI>();
            nextButton = bubbleRect.Find("NextButton")?.GetComponent<Button>();
            skipButton = bubbleRect.Find("SkipButton")?.GetComponent<Button>();
            nextLabelText = bubbleRect.Find("NextButton/Label")?.GetComponent<TextMeshProUGUI>();
            skipLabelText = bubbleRect.Find("SkipButton/Label")?.GetComponent<TextMeshProUGUI>();

            nextButton.onClick.AddListener(Advance);
            skipButton.onClick.AddListener(Complete);
            root.transform.SetAsLastSibling();
            ShowCurrentStep();
        }

        public static MiniGameTutorialOverlay Show(Transform parent, IList<MiniGameTutorialStep> steps, Action onCompleted)
        {
            if (parent == null || steps == null || steps.Count == 0)
            {
                onCompleted?.Invoke();
                return null;
            }

            return new MiniGameTutorialOverlay(parent, steps, onCompleted);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        private void ShowCurrentStep()
        {
            if (currentIndex < 0 || currentIndex >= steps.Count)
            {
                Complete();
                return;
            }

            var step = steps[currentIndex];
            var requireTargetClick = step != null && step.RequireTargetClick;
            nextButton.gameObject.SetActive(!requireTargetClick);
            targetClickRect.gameObject.SetActive(requireTargetClick);

            nextLabelText.text = currentIndex + 1 >= steps.Count
                ? UiTextCatalog.GetOrFallback("tutorial.action.done", "Done")
                : UiTextCatalog.GetOrFallback("tutorial.action.next", "Next");
            skipLabelText.text = UiTextCatalog.GetOrFallback("tutorial.action.skip", "Skip");
            pageText.text = (currentIndex + 1) + "/" + steps.Count;
            titleText.text = ResolveTitle(step);
            messageText.text = ResolveMessage(step);
            RefreshCurrentStepLayout();
        }

        private void RefreshCurrentStepLayout()
        {
            if (disposed || currentIndex < 0 || currentIndex >= steps.Count)
            {
                return;
            }

            var step = steps[currentIndex];
            var target = ResolveTarget(step);
            var targetRect = CalculateTargetRect(target, step != null ? step.Padding : Vector2.zero);
            ApplyHole(targetRect);
            ApplyBubble(targetRect);
            if (targetClickRect.gameObject.activeSelf)
            {
                ApplyRect(targetClickRect, targetRect);
            }
        }

        private void Advance()
        {
            currentIndex += 1;
            ShowCurrentStep();
        }

        private void HandleTargetClick()
        {
            var step = currentIndex >= 0 && currentIndex < steps.Count ? steps[currentIndex] : null;
            step?.OnTargetClick?.Invoke();
            Advance();
        }

        private void Complete()
        {
            var completed = onCompleted;
            Dispose();
            completed?.Invoke();
        }

        private static RectTransform ResolveTarget(MiniGameTutorialStep step)
        {
            if (step == null)
            {
                return null;
            }

            if (step.ResolveTarget != null)
            {
                var resolved = step.ResolveTarget();
                if (resolved != null)
                {
                    return resolved;
                }
            }

            return step.Target;
        }

        private string ResolveMessage(MiniGameTutorialStep step)
        {
            if (step == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(step.MessageKey))
            {
                return UiTextCatalog.Get(step.MessageKey);
            }

            return step.Message ?? string.Empty;
        }

        private string ResolveTitle(MiniGameTutorialStep step)
        {
            if (step == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(step.TitleKey))
            {
                return UiTextCatalog.Get(step.TitleKey);
            }

            if (!string.IsNullOrWhiteSpace(step.Title))
            {
                return step.Title;
            }

            return UiTextCatalog.GetOrFallback("tutorial.title.default", "Tutorial");
        }

        private Rect CalculateTargetRect(RectTransform target, Vector2 padding)
        {
            var canvasRect = rootRect.rect;
            if (target == null)
            {
                var fallbackSize = new Vector2(HighlightMinSize * 2f, HighlightMinSize);
                return new Rect(
                    -fallbackSize.x * 0.5f,
                    -fallbackSize.y * 0.5f,
                    fallbackSize.x,
                    fallbackSize.y);
            }

            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < corners.Length; i++)
            {
                var local = rootRect.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            var paddedMin = min - padding;
            var paddedMax = max + padding;
            var width = Mathf.Max(HighlightMinSize, paddedMax.x - paddedMin.x);
            var height = Mathf.Max(HighlightMinSize, paddedMax.y - paddedMin.y);
            width = Mathf.Min(width, canvasRect.width);
            height = Mathf.Min(height, canvasRect.height);
            var center = (paddedMin + paddedMax) * 0.5f;
            var x = Mathf.Clamp(center.x - width * 0.5f, canvasRect.xMin, canvasRect.xMax - width);
            var y = Mathf.Clamp(center.y - height * 0.5f, canvasRect.yMin, canvasRect.yMax - height);
            return new Rect(x, y, width, height);
        }

        private void ApplyHole(Rect target)
        {
            var bounds = rootRect.rect;
            ApplyRect(dimRects[0], new Rect(bounds.xMin, target.yMax, bounds.width, Mathf.Max(0f, bounds.yMax - target.yMax)));
            ApplyRect(dimRects[1], new Rect(bounds.xMin, bounds.yMin, bounds.width, Mathf.Max(0f, target.yMin - bounds.yMin)));
            ApplyRect(dimRects[2], new Rect(bounds.xMin, target.yMin, Mathf.Max(0f, target.xMin - bounds.xMin), target.height));
            ApplyRect(dimRects[3], new Rect(target.xMax, target.yMin, Mathf.Max(0f, bounds.xMax - target.xMax), target.height));
            if (dimHoleGraphic != null)
            {
                dimHoleGraphic.SetHole(target);
            }
        }

        private void ApplyBubble(Rect target)
        {
            messageText.ForceMeshUpdate();

            var bounds = rootRect.rect;
            var bubbleWidth = Mathf.Min(BubbleWidth, Mathf.Max(280f, bounds.width - ScreenMargin * 2f));
            var preferredHeight = messageText.GetPreferredValues(messageText.text, bubbleWidth - MessageWidthPadding, 0f).y + 240f;
            var bubbleHeight = Mathf.Max(BubbleMinHeight, Mathf.Ceil(preferredHeight));
            var showBelow = target.yMin - BubbleGap - bubbleHeight >= bounds.yMin + ScreenMargin;
            var y = showBelow ? target.yMin - BubbleGap - bubbleHeight : target.yMax + BubbleGap;
            y = Mathf.Clamp(y, bounds.yMin + ScreenMargin, bounds.yMax - ScreenMargin - bubbleHeight);
            var centerX = Mathf.Clamp(target.center.x, bounds.xMin + ScreenMargin + bubbleWidth * 0.5f, bounds.xMax - ScreenMargin - bubbleWidth * 0.5f);

            ApplyRect(bubbleRect, new Rect(centerX - bubbleWidth * 0.5f, y, bubbleWidth, bubbleHeight));
            ApplyPointer(target, showBelow);
        }

        private void ApplyPointer(Rect target, bool bubbleBelowTarget)
        {
            if (pointerRect == null)
            {
                return;
            }

            pointerRect.anchorMin = new Vector2(0.5f, bubbleBelowTarget ? 1f : 0f);
            pointerRect.anchorMax = pointerRect.anchorMin;
            pointerRect.pivot = new Vector2(0.5f, bubbleBelowTarget ? 0f : 1f);
            var bubbleWidth = bubbleRect.sizeDelta.x > 0f ? bubbleRect.sizeDelta.x : BubbleWidth;
            pointerRect.anchoredPosition = new Vector2(
                Mathf.Clamp(target.center.x - bubbleRect.anchoredPosition.x, -bubbleWidth * 0.5f + 54f, bubbleWidth * 0.5f - 54f),
                bubbleBelowTarget ? -2f : 2f);
            pointerRect.localRotation = Quaternion.Euler(0f, 0f, bubbleBelowTarget ? 180f : 0f);
            pointerRect.sizeDelta = new Vector2(46f, 32f);
            pointerRect.SetAsFirstSibling();
        }

        private static RectTransform CreateTargetClickLayer(Transform parent)
        {
            var rect = CreateImageRect("TargetClick", parent, new Color(1f, 1f, 1f, 0f), true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Graphic>();
            button.transition = Selectable.Transition.None;
            rect.gameObject.SetActive(false);
            return rect;
        }

        private static RectTransform CreateBubble(Transform parent)
        {
            var bubble = new GameObject(
                "Bubble",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic),
                typeof(Shadow)).GetComponent<RectTransform>();
            bubble.SetParent(parent, false);
            var border = bubble.GetComponent<RoundedRectGraphic>();
            border.color = new Color32(170, 194, 96, 255);
            border.raycastTarget = false;
            border.CornerRadius = 30f;
            var shadow = bubble.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.2f);
            shadow.effectDistance = new Vector2(0f, -5f);

            var pointer = new GameObject("Pointer", typeof(RectTransform), typeof(CanvasRenderer), typeof(TutorialPointerGraphic)).GetComponent<RectTransform>();
            pointer.SetParent(bubble, false);
            var pointerGraphic = pointer.GetComponent<TutorialPointerGraphic>();
            pointerGraphic.color = new Color32(170, 194, 96, 255);
            pointerGraphic.raycastTarget = false;

            var fill = CreateRoundedRect("Fill", bubble, new Color32(255, 251, 236, 252), 27f, false);
            Stretch(fill, Vector2.zero, Vector2.one, new Vector2(5f, 5f), new Vector2(-5f, -5f));

            var pageBadge = CreateRoundedRect("PageBadge", bubble, new Color32(244, 246, 220, 255), 18f, false);
            pageBadge.anchorMin = new Vector2(0.5f, 1f);
            pageBadge.anchorMax = pageBadge.anchorMin;
            pageBadge.pivot = new Vector2(0.5f, 1f);
            pageBadge.anchoredPosition = new Vector2(0f, -24f);
            pageBadge.sizeDelta = new Vector2(82f, 35f);
            var pageText = CreateText("PageText", pageBadge, 21f, FontStyles.Bold, TextAlignmentOptions.Center);
            pageText.color = new Color32(142, 123, 84, 255);
            pageText.fontSizeMin = 18f;

            var title = CreateText("Title", bubble, 34f, FontStyles.Bold, TextAlignmentOptions.Center);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.offsetMin = new Vector2(BubbleHorizontalPadding, -112f);
            title.rectTransform.offsetMax = new Vector2(-BubbleHorizontalPadding, -62f);
            title.color = new Color32(102, 61, 27, 255);
            title.fontSizeMin = 24f;

            CreateDottedDivider(bubble);

            var message = CreateText("Message", bubble, 24f, FontStyles.Normal, TextAlignmentOptions.Center);
            message.rectTransform.anchorMin = new Vector2(0f, 0f);
            message.rectTransform.anchorMax = new Vector2(1f, 1f);
            message.rectTransform.offsetMin = new Vector2(BubbleHorizontalPadding, 92f);
            message.rectTransform.offsetMax = new Vector2(-BubbleHorizontalPadding, -148f);
            message.color = new Color32(80, 72, 61, 255);
            message.fontSizeMin = 20f;

            CreateActionButton(
                "SkipButton",
                bubble,
                new Vector2(42f, 48f),
                new Vector2(156f, 52f),
                new Color32(255, 252, 242, 255),
                new Color32(112, 147, 53, 255),
                new Color32(197, 184, 149, 255));
            CreateActionButton(
                "NextButton",
                bubble,
                new Vector2(-42f, 48f),
                new Vector2(214f, 56f),
                new Color32(143, 192, 71, 255),
                Color.white,
                new Color32(90, 132, 38, 255));
            return bubble;
        }

        private static Button CreateActionButton(string name, Transform parent, Vector2 offset, Vector2 size, Color color, Color labelColor, Color borderColor)
        {
            var rect = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic),
                typeof(Button),
                typeof(Shadow)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var right = offset.x < 0f;
            rect.anchorMin = right ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = right ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;

            var image = rect.GetComponent<RoundedRectGraphic>();
            image.color = borderColor;
            image.raycastTarget = true;
            image.CornerRadius = size.y * 0.5f;
            var shadow = rect.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.16f);
            shadow.effectDistance = new Vector2(0f, -3f);
            var fill = CreateRoundedRect("Fill", rect, color, Mathf.Max(0f, size.y * 0.5f - 4f), false);
            Stretch(fill, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            var button = rect.GetComponent<Button>();
            button.targetGraphic = image;

            var label = CreateText("Label", rect, 22f, FontStyles.Bold, TextAlignmentOptions.Center);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.color = labelColor;
            label.fontSizeMin = 19f;
            return button;
        }

        private static void CreateDottedDivider(Transform parent)
        {
            var root = new GameObject("DividerDots", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = new Vector2(0.5f, 1f);
            root.anchorMax = root.anchorMin;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, -126f);
            root.sizeDelta = new Vector2(130f, 10f);

            for (var i = 0; i < 13; i++)
            {
                var dot = CreateRoundedRect("Dot_" + i, root, new Color32(188, 207, 137, 255), 3f, false);
                dot.anchorMin = new Vector2(0.5f, 0.5f);
                dot.anchorMax = dot.anchorMin;
                dot.pivot = new Vector2(0.5f, 0.5f);
                dot.anchoredPosition = new Vector2(-60f + i * 10f, 0f);
                dot.sizeDelta = new Vector2(4f, 4f);
            }
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var text = rect.GetComponent<TextMeshProUGUI>();
            text.font = MiniGameFontProvider.DefaultFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = fontSize;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static RectTransform CreateImageRect(string name, Transform parent, Color color, bool raycastTarget)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var image = rect.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return rect;
        }

        private static RoundedHoleDimGraphic CreateRoundedHoleDim(Transform parent)
        {
            var rect = new GameObject("DimRoundedHole", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedHoleDimGraphic)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var graphic = rect.GetComponent<RoundedHoleDimGraphic>();
            graphic.color = new Color(0f, 0f, 0f, 0.68f);
            graphic.raycastTarget = true;
            return graphic;
        }

        private static RectTransform CreateRoundedRect(string name, Transform parent, Color color, float radius, bool raycastTarget)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var graphic = rect.GetComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = radius;
            graphic.raycastTarget = raycastTarget;
            return rect;
        }

        private static void ApplyRect(RectTransform rect, Rect target)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = target.center;
            rect.sizeDelta = target.size;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private sealed class TutorialOverlayUpdater : MonoBehaviour
        {
            private MiniGameTutorialOverlay owner;

            public void Bind(MiniGameTutorialOverlay overlay)
            {
                owner = overlay;
            }

            private void LateUpdate()
            {
                owner?.RefreshCurrentStepLayout();
            }
        }

        private sealed class TutorialPointerGraphic : MaskableGraphic
        {
            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                var rect = rectTransform.rect;
                var top = AddVertex(vh, new Vector2(rect.center.x, rect.yMax));
                var left = AddVertex(vh, new Vector2(rect.xMin, rect.yMin));
                var right = AddVertex(vh, new Vector2(rect.xMax, rect.yMin));
                vh.AddTriangle(top, left, right);
            }

            private int AddVertex(VertexHelper vh, Vector2 position)
            {
                var vertex = UIVertex.simpleVert;
                vertex.color = color;
                vertex.position = position;
                vh.AddVert(vertex);
                return vh.currentVertCount - 1;
            }
        }

        private sealed class RoundedHoleDimGraphic : MaskableGraphic
        {
            private const float DefaultHoleRadius = 22f;
            private const int CornerSegments = 8;

            private Rect hole;
            private bool hasHole;

            public void SetHole(Rect value)
            {
                hole = value;
                hasHole = true;
                SetVerticesDirty();
            }

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                var bounds = rectTransform.rect;
                if (!hasHole)
                {
                    AddQuad(vh, bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax);
                    return;
                }

                var target = ClampHole(hole, bounds);
                var radius = Mathf.Min(DefaultHoleRadius, target.width * 0.5f, target.height * 0.5f);
                AddQuad(vh, bounds.xMin, target.yMax, bounds.xMax, bounds.yMax);
                AddQuad(vh, bounds.xMin, bounds.yMin, bounds.xMax, target.yMin);
                AddQuad(vh, bounds.xMin, target.yMin, target.xMin, target.yMax);
                AddQuad(vh, target.xMax, target.yMin, bounds.xMax, target.yMax);

                if (radius > 0.01f)
                {
                    AddCornerPatch(vh, new Vector2(target.xMin + radius, target.yMax - radius), radius, 90f, 180f, target.xMin, target.yMax);
                    AddCornerPatch(vh, new Vector2(target.xMax - radius, target.yMax - radius), radius, 0f, 90f, target.xMax, target.yMax);
                    AddCornerPatch(vh, new Vector2(target.xMin + radius, target.yMin + radius), radius, 180f, 270f, target.xMin, target.yMin);
                    AddCornerPatch(vh, new Vector2(target.xMax - radius, target.yMin + radius), radius, 270f, 360f, target.xMax, target.yMin);
                }
            }

            private static Rect ClampHole(Rect source, Rect bounds)
            {
                var width = Mathf.Clamp(source.width, 0f, bounds.width);
                var height = Mathf.Clamp(source.height, 0f, bounds.height);
                var x = Mathf.Clamp(source.center.x - width * 0.5f, bounds.xMin, bounds.xMax - width);
                var y = Mathf.Clamp(source.center.y - height * 0.5f, bounds.yMin, bounds.yMax - height);
                return new Rect(x, y, width, height);
            }

            private void AddCornerPatch(VertexHelper vh, Vector2 center, float radius, float startDegrees, float endDegrees, float outerX, float outerY)
            {
                var points = new List<Vector2>(CornerSegments + 3)
                {
                    new Vector2(outerX, outerY)
                };

                for (var i = 0; i <= CornerSegments; i++)
                {
                    var angle = Mathf.Lerp(startDegrees, endDegrees, i / (float)CornerSegments) * Mathf.Deg2Rad;
                    points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }

                AddPolygon(vh, points);
            }

            private void AddPolygon(VertexHelper vh, IList<Vector2> points)
            {
                if (points == null || points.Count < 3)
                {
                    return;
                }

                var first = AddVertex(vh, points[0]);
                for (var i = 1; i < points.Count - 1; i++)
                {
                    var current = AddVertex(vh, points[i]);
                    var next = AddVertex(vh, points[i + 1]);
                    vh.AddTriangle(first, current, next);
                }
            }

            private void AddQuad(VertexHelper vh, float xMin, float yMin, float xMax, float yMax)
            {
                if (xMax <= xMin || yMax <= yMin)
                {
                    return;
                }

                var bottomLeft = AddVertex(vh, new Vector2(xMin, yMin));
                var topLeft = AddVertex(vh, new Vector2(xMin, yMax));
                var topRight = AddVertex(vh, new Vector2(xMax, yMax));
                var bottomRight = AddVertex(vh, new Vector2(xMax, yMin));
                vh.AddTriangle(bottomLeft, topLeft, topRight);
                vh.AddTriangle(bottomLeft, topRight, bottomRight);
            }

            private int AddVertex(VertexHelper vh, Vector2 position)
            {
                var vertex = UIVertex.simpleVert;
                vertex.color = color;
                vertex.position = position;
                vh.AddVert(vertex);
                return vh.currentVertCount - 1;
            }
        }
    }
}
