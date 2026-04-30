using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed partial class WaterSortGameView
    {
        private void BuildContentSection()
        {
            var rootObject = CreateRectObject("WaterSortContent", Shell.ContentHost);
            contentRoot = rootObject.GetComponent<RectTransform>();
            Stretch(contentRoot, Vector2.zero, Vector2.one, new Vector2(24f, 16f), new Vector2(-24f, -16f));

            var boardGraphic = EnsureRoundedRectGraphic(rootObject, new Color32(238, 247, 250, 232), 34f, false);
            boardGraphic.raycastTarget = false;

            var gridObject = CreateRectObject("WaterSortBottleGrid", contentRoot);
            bottleGrid = gridObject.GetComponent<RectTransform>();
            Stretch(bottleGrid, Vector2.zero, Vector2.one, new Vector2(20f, 28f), new Vector2(-20f, -28f));

            var grid = gridObject.AddComponent<GridLayoutGroup>();
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.spacing = new Vector2(18f, 18f);

            var streamObject = CreateRectObject("WaterSortPourStreamLayer", contentRoot);
            streamLayer = streamObject.GetComponent<RectTransform>();
            Stretch(streamLayer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            streamLayer.SetAsFirstSibling();
        }

        private void BuildBottomSection()
        {
            var bottomContainerRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("WaterSortBottom"));

            var actionBar = bottomContainerRefs.ActionBar;
            actionBar.sizeDelta = new Vector2(560f, 88f);
            var layout = actionBar.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = 12f;
            }

            levelSelectButton = MiniGameShellBottomBarBuilder.CreateLevelSelectButton(actionBar).Button;
            MiniGameSfxPlayer.Attach(levelSelectButton, MiniGameSfxType.UiTap, 0.95f);
            levelSelectButton.onClick.AddListener(OnLevelSelectClicked);

            restartButton = MiniGameShellBottomBarBuilder.CreateRestartButton(actionBar).Button;
            MiniGameSfxPlayer.Attach(restartButton, MiniGameSfxType.UiTap, 0.95f);
            restartButton.onClick.AddListener(OnRestartClicked);
        }

        private void LoadCurrentPuzzle()
        {
            bottles.Clear();
            var layout = LevelDefinitions[currentLevelIndex].CreateLayout();
            for (var i = 0; i < layout.Length; i++)
            {
                bottles.Add(new List<int>(layout[i]));
            }
        }

        private void BuildBottleViews()
        {
            ClearBottleViews();
            ConfigureGrid();

            for (var i = 0; i < bottles.Count; i++)
            {
                var bottle = CreateBottleView(i);
                bottleViews.Add(bottle);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(bottleGrid);
        }

        private void ConfigureGrid()
        {
            var grid = bottleGrid.GetComponent<GridLayoutGroup>();
            grid.constraintCount = bottles.Count <= 5 ? 3 : 4;

            var columns = grid.constraintCount;
            var rows = Mathf.CeilToInt((float)bottles.Count / columns);
            var contentSize = ResolveContentSize();
            lastContentSize = contentSize;
            var availableWidth = Mathf.Max(1f, contentSize.x - 40f);
            var availableHeight = Mathf.Max(1f, contentSize.y - 56f);
            var cellWidth = (availableWidth - (columns - 1) * grid.spacing.x) / columns;
            var cellHeight = (availableHeight - (rows - 1) * grid.spacing.y) / rows;
            cellWidth = Mathf.Clamp(cellWidth, 96f, MaxCellWidth);
            cellHeight = Mathf.Clamp(cellHeight, 160f, MaxCellHeight);

            if (cellHeight > cellWidth / BottleAspect)
            {
                cellHeight = cellWidth / BottleAspect;
            }
            else if (cellWidth > cellHeight * BottleAspect)
            {
                cellWidth = cellHeight * BottleAspect;
            }

            grid.cellSize = new Vector2(Mathf.Floor(cellWidth), Mathf.Floor(cellHeight));
            LayoutRebuilder.ForceRebuildLayoutImmediate(bottleGrid);
            for (var i = 0; i < bottleViews.Count; i++)
            {
                bottleViews[i].IsLifted = false;
            }

            RefreshBottleSelection();
        }

        private Vector2 ResolveContentSize()
        {
            var width = contentRoot != null ? contentRoot.rect.width : 0f;
            var height = contentRoot != null ? contentRoot.rect.height : 0f;
            if (width > 120f && height > 160f)
            {
                return new Vector2(width, height);
            }

            var parentRect = contentRoot != null ? contentRoot.parent as RectTransform : null;
            width = parentRect != null ? parentRect.rect.width - 48f : 0f;
            height = parentRect != null ? parentRect.rect.height - 32f : 0f;
            if (width > 120f && height > 160f)
            {
                return new Vector2(width, height);
            }

            return new Vector2(FallbackContentWidth, FallbackContentHeight);
        }

        private void RefreshGridIfContentSizeChanged()
        {
            if (contentRoot == null || bottleGrid == null || bottleViews.Count == 0)
            {
                return;
            }

            var currentSize = ResolveContentSize();
            if (Mathf.Abs(currentSize.x - lastContentSize.x) < 1f && Mathf.Abs(currentSize.y - lastContentSize.y) < 1f)
            {
                return;
            }

            ConfigureGrid();
        }

        private BottleView CreateBottleView(int index)
        {
            var rootObject = CreateRectObject("WaterSortBottle_" + index, bottleGrid);
            rootObject.AddComponent<LayoutElement>();

            var root = rootObject.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(140f, 230f);

            var background = EnsureRoundedRectGraphic(rootObject, new Color32(255, 255, 255, 0), 24f, true);
            var button = rootObject.AddComponent<Button>();
            button.targetGraphic = background;
            ConfigureButtonColors(button);
            button.onClick.AddListener(delegate { OnBottleClicked(index); });

            var bottleShapeObject = CreateRectObject("BottleShape", root);
            var bottleShapeRect = bottleShapeObject.GetComponent<RectTransform>();
            Stretch(
                bottleShapeRect,
                Vector2.zero,
                Vector2.one,
                new Vector2(BottleShapeHorizontalInset, BottleShapeTopInset),
                new Vector2(-BottleShapeHorizontalInset, -28f));
            bottleShapeObject.AddComponent<CanvasRenderer>();
            var bottleShape = bottleShapeObject.AddComponent<BottleShapeGraphic>();
            bottleShape.color = new Color32(142, 150, 153, 255);
            bottleShape.raycastTarget = false;

            var liquidMaskObject = CreateRectObject("LiquidMask", root);
            var liquidMask = liquidMaskObject.GetComponent<RectTransform>();
            Stretch(
                liquidMask,
                Vector2.zero,
                Vector2.one,
                new Vector2(BottleFillHorizontalInset, BottleFillBottomInset),
                new Vector2(-BottleFillHorizontalInset, -BottleFillTopInset));
            liquidMaskObject.AddComponent<CanvasRenderer>();
            var liquidMaskGraphic = liquidMaskObject.AddComponent<BottleLiquidMaskGraphic>();
            liquidMaskGraphic.color = Color.white;
            liquidMaskGraphic.raycastTarget = false;
            var liquidMaskComponent = liquidMaskObject.AddComponent<Mask>();
            liquidMaskComponent.showMaskGraphic = false;

            var fillAreaObject = CreateRectObject("FillArea", liquidMask);
            var fillArea = fillAreaObject.GetComponent<RectTransform>();
            fillArea.pivot = new Vector2(0.5f, 0f);
            Stretch(fillArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var segments = new WaterLayerGraphic[BottleCapacity];
            for (var i = 0; i < BottleCapacity; i++)
            {
                var segmentObject = new GameObject("Segment_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(WaterLayerGraphic));
                var segmentRect = segmentObject.GetComponent<RectTransform>();
                segmentRect.SetParent(fillArea, false);
                segmentRect.anchorMin = new Vector2(
                    -BottleLiquidHorizontalOverflow,
                    ResolveSegmentBottomAnchor(i, BottleCapacity));
                segmentRect.anchorMax = new Vector2(1f + BottleLiquidHorizontalOverflow, (i + 1) / (float)BottleCapacity);
                segmentRect.offsetMin = ResolveSegmentOffsetMin(i);
                segmentRect.offsetMax = Vector2.zero;

                var segment = segmentObject.GetComponent<WaterLayerGraphic>();
                segment.raycastTarget = false;
                segment.WaveAmplitude = 0f;
                segment.WavePhase = 0f;
                segments[i] = segment;
            }

            bottleShapeRect.SetAsLastSibling();

            var capObject = CreateRectObject("BottleCap", root);
            var capRect = capObject.GetComponent<RectTransform>();
            capRect.anchorMin = new Vector2(0.5f, 1f);
            capRect.anchorMax = new Vector2(0.5f, 1f);
            capRect.pivot = new Vector2(0.5f, 0.5f);
            capRect.sizeDelta = new Vector2(BottleCapWidth, BottleCapHeight);
            var capFinalPosition = new Vector2(0f, -BottleCapTopInset);
            capRect.anchoredPosition = capFinalPosition;
            var capGraphic = EnsureRoundedRectGraphic(capObject, new Color32(88, 102, 108, 255), BottleCapCornerRadius, false);
            capGraphic.raycastTarget = false;
            capObject.SetActive(false);
            capRect.SetAsLastSibling();

            return new BottleView
            {
                Root = root,
                Button = button,
                Background = background,
                BottleShape = bottleShape,
                Cap = capRect,
                CapFinalPosition = capFinalPosition,
                LiquidMask = liquidMask,
                FillArea = fillArea,
                Segments = segments
            };
        }

        private void RefreshBottleViews()
        {
            for (var i = 0; i < bottleViews.Count && i < bottles.Count; i++)
            {
                SetBottleVisualState(bottleViews[i], bottles[i], bottles[i].Count, -1, IdleWaveAmplitude);
                RefreshBottleCap(i);
            }
        }

        private void SetBottleVisualState(
            BottleView view,
            List<int> colorStack,
            float fillCount,
            int activeLayerIndex,
            float waveAmplitude)
        {
            if (view == null || view.Segments == null)
            {
                return;
            }

            fillCount = Mathf.Clamp(fillCount, 0f, BottleCapacity);
            var topLayerIndex = Mathf.Clamp(Mathf.CeilToInt(fillCount) - 1, 0, BottleCapacity - 1);
            if (activeLayerIndex >= 0)
            {
                topLayerIndex = activeLayerIndex;
            }

            ConfigureWholeLiquidArea(view, fillCount);
            for (var segmentIndex = 0; segmentIndex < view.Segments.Length; segmentIndex++)
            {
                var segment = view.Segments[segmentIndex];
                if (segment == null)
                {
                    continue;
                }

                segment.ClearTopClip();
                var filledAmount = Mathf.Clamp(fillCount - segmentIndex, 0f, 1f);
                var filled = filledAmount > BottleEmptyEpsilon && colorStack != null && segmentIndex < colorStack.Count;
                segment.gameObject.SetActive(filled);
                if (!filled)
                {
                    continue;
                }

                var rect = segment.rectTransform;
                rect.anchorMin = new Vector2(0f, ResolveSegmentBottomAnchor(segmentIndex, fillCount));
                rect.anchorMax = new Vector2(1f, (segmentIndex + filledAmount) / fillCount);
                rect.offsetMin = ResolveSegmentOffsetMin(segmentIndex);
                rect.offsetMax = Vector2.zero;

                var color = GetWaterColor(colorStack[segmentIndex]);
                segment.color = color;
                segment.IsBottomLayer = segmentIndex == 0;
                segment.IsTopLayer = segmentIndex == topLayerIndex;
                segment.WaveAmplitude = segment.IsTopLayer ? waveAmplitude : 0f;
                segment.SurfaceInset = 0f;
                segment.SetVerticesDirty();
            }
        }

        private void RefreshBottleCap(int index)
        {
            if (index < 0 || index >= bottleViews.Count)
            {
                return;
            }

            var view = bottleViews[index];
            if (view == null || view.Cap == null)
            {
                return;
            }

            var shouldShow = IsBottleCompletionLocked(index);
            if (shouldShow)
            {
                if (!view.IsCapVisible)
                {
                    view.IsCapVisible = true;
                    view.IsCapAnimating = true;
                    view.CapAnimationElapsed = 0f;
                    view.Cap.anchoredPosition = view.CapFinalPosition + new Vector2(0f, BottleCapDropOffset);
                    view.Cap.gameObject.SetActive(true);
                    return;
                }

                view.Cap.gameObject.SetActive(true);
                if (!view.IsCapAnimating)
                {
                    view.Cap.anchoredPosition = view.CapFinalPosition;
                }

                return;
            }

            view.IsCapVisible = false;
            view.IsCapAnimating = false;
            view.CapAnimationElapsed = 0f;
            view.Cap.anchoredPosition = view.CapFinalPosition;
            view.Cap.gameObject.SetActive(false);
        }

        private void AdvanceBottleCapAnimations(float deltaTime)
        {
            if (bottleViews.Count == 0)
            {
                return;
            }

            for (var i = 0; i < bottleViews.Count; i++)
            {
                var view = bottleViews[i];
                if (view == null || view.Cap == null || !view.IsCapAnimating)
                {
                    continue;
                }

                view.CapAnimationElapsed += Mathf.Max(0f, deltaTime);
                var progress = SmoothStep01(view.CapAnimationElapsed / BottleCapDropDuration);
                view.Cap.anchoredPosition = Vector2.Lerp(
                    view.CapFinalPosition + new Vector2(0f, BottleCapDropOffset),
                    view.CapFinalPosition,
                    progress);

                if (progress >= 1f)
                {
                    view.IsCapAnimating = false;
                    view.Cap.anchoredPosition = view.CapFinalPosition;
                }
            }
        }

        private static void ConfigureWholeLiquidArea(BottleView view, float fillCount)
        {
            if (view == null || view.FillArea == null)
            {
                return;
            }

            fillCount = Mathf.Clamp(fillCount, 0f, BottleCapacity);
            ApplyLiquidAreaAnchors(view, ResolveAreaPreservingLiquidHeight(view, fillCount));
        }

        private static float ResolveSegmentBottomAnchor(int segmentIndex, float fillCount)
        {
            if (segmentIndex != 0 || fillCount <= BottleEmptyEpsilon)
            {
                return segmentIndex / Mathf.Max(fillCount, BottleEmptyEpsilon);
            }

            return 0f;
        }

        private static Vector2 ResolveSegmentOffsetMin(int segmentIndex)
        {
            return segmentIndex == 0
                ? new Vector2(0f, -BottleBottomLiquidVerticalOverflowPixels)
                : Vector2.zero;
        }

        private static float ResolveAreaPreservingLiquidHeight(BottleView view, float fillCount)
        {
            if (view == null || view.FillArea == null || view.LiquidMask == null || fillCount <= BottleEmptyEpsilon)
            {
                return 0f;
            }

            var maskRect = view.LiquidMask.rect;
            if (maskRect.width <= 0.1f || maskRect.height <= 0.1f)
            {
                return fillCount / BottleCapacity * BottleFullFillRatio;
            }

            var angleDegrees = Mathf.Abs(NormalizeAngle(view.FillArea.localEulerAngles.z));
            return SampleLiquidHeightLookup(fillCount, angleDegrees) / BottleLiquidMaxHeightAnchor;
        }

        private static float SampleLiquidHeightLookup(float fillCount, float angleDegrees)
        {
            var fillPosition = Mathf.Clamp01(fillCount / BottleCapacity) * WaterSortLookupData.FillSamples;
            var fillIndex = Mathf.FloorToInt(fillPosition);
            var fillNextIndex = Mathf.Min(fillIndex + 1, WaterSortLookupData.FillSamples);
            var fillT = fillPosition - fillIndex;

            var maxUsableAngleIndex = ResolveMaxUsableLiquidLookupAngle(fillIndex, fillNextIndex);
            var anglePosition = Mathf.Clamp(angleDegrees, 0f, WaterSortLookupData.MaxAngleDegrees) / WaterSortLookupData.AngleStepDegrees;
            anglePosition = Mathf.Min(anglePosition, maxUsableAngleIndex);
            var angleIndex = Mathf.FloorToInt(anglePosition);
            var angleNextIndex = Mathf.Min(angleIndex + 1, WaterSortLookupData.AngleCount - 1);
            var angleT = anglePosition - angleIndex;

            var rowWidth = WaterSortLookupData.AngleCount;
            var currentRow = fillIndex * rowWidth;
            var nextRow = fillNextIndex * rowWidth;
            var bottomLeft = WaterSortLookupData.LiquidHeightLookup[currentRow + angleIndex];
            var bottomRight = WaterSortLookupData.LiquidHeightLookup[currentRow + angleNextIndex];
            var topLeft = WaterSortLookupData.LiquidHeightLookup[nextRow + angleIndex];
            var topRight = WaterSortLookupData.LiquidHeightLookup[nextRow + angleNextIndex];
            var bottom = Mathf.Lerp(bottomLeft, bottomRight, angleT);
            var top = Mathf.Lerp(topLeft, topRight, angleT);
            return Mathf.Lerp(bottom, top, fillT);
        }

        private static int ResolveMaxUsableLiquidLookupAngle(int fillIndex, int fillNextIndex)
        {
            var rowWidth = WaterSortLookupData.AngleCount;
            var currentRow = fillIndex * rowWidth;
            var nextRow = fillNextIndex * rowWidth;
            for (var angleIndex = WaterSortLookupData.AngleCount - 1; angleIndex >= 0; angleIndex--)
            {
                if (IsUsableLiquidLookupValue(WaterSortLookupData.LiquidHeightLookup[currentRow + angleIndex])
                    && IsUsableLiquidLookupValue(WaterSortLookupData.LiquidHeightLookup[nextRow + angleIndex]))
                {
                    return angleIndex;
                }
            }

            return 0;
        }

        private static bool IsUsableLiquidLookupValue(float value)
        {
            return value < BottleLiquidMaxHeightAnchor - BottleLiquidLookupMaxValueEpsilon;
        }

        private static void ApplyLiquidAreaAnchors(BottleView view, float liquidHeight)
        {
            if (view == null || view.FillArea == null)
            {
                return;
            }

            view.FillArea.anchorMin = new Vector2(-BottleLiquidHorizontalOverflow, 0f);
            view.FillArea.anchorMax = new Vector2(
                1f + BottleLiquidHorizontalOverflow,
                Mathf.Clamp(liquidHeight, 0f, BottleLiquidMaxHeightAnchor));
            view.FillArea.offsetMin = Vector2.zero;
            view.FillArea.offsetMax = Vector2.zero;
            view.FillArea.anchoredPosition = Vector2.zero;
            view.FillArea.ForceUpdateRectTransforms();
        }

        private void RefreshBottleSelection()
        {
            for (var i = 0; i < bottleViews.Count; i++)
            {
                var selected = i == selectedBottleIndex;
                var view = bottleViews[i];
                if (view.Background != null)
                {
                    view.Background.color = new Color32(255, 255, 255, 0);
                }

                if (view.Root != null)
                {
                    if (selected && !view.IsLifted)
                    {
                        view.Root.anchoredPosition += new Vector2(0f, BottleSelectionLift);
                        view.IsLifted = true;
                    }
                    else if (!selected && view.IsLifted)
                    {
                        view.Root.anchoredPosition -= new Vector2(0f, BottleSelectionLift);
                        view.IsLifted = false;
                    }

                    view.Root.localScale = Vector3.one;
                }
            }
        }

        private void RefreshAll()
        {
            RefreshHud();
            RefreshBottleViews();
            RefreshBottleSelection();
        }

        private void RefreshHud()
        {
            if (titleLabel != null)
            {
                titleLabel.text = UiTextCatalog.Get("game.water_sort.name");
            }

            if (scoreLabel != null)
            {
                scoreLabel.text = UiTextCatalog.Format(
                    "water_sort.hud.level_score",
                    currentLevelIndex + 1,
                    LevelDefinitions.Length,
                    moveCount,
                    CountCompletedBottles(),
                    LevelDefinitions[currentLevelIndex].ColorCount);
            }
        }

        private void AdvanceIdleWater(float deltaTime)
        {
            if (bottleViews.Count == 0)
            {
                return;
            }

            for (var i = 0; i < bottleViews.Count; i++)
            {
                var view = bottleViews[i];
                if (view == null || view.Segments == null)
                {
                    continue;
                }

                for (var segmentIndex = 0; segmentIndex < view.Segments.Length; segmentIndex++)
                {
                    var segment = view.Segments[segmentIndex];
                    if (segment != null && segment.gameObject.activeSelf)
                    {
                        segment.WavePhase += deltaTime * (activePourAnimations.Count > 0 ? 8f : 2.2f);
                        segment.SetVerticesDirty();
                    }
                }
            }
        }

        private void ClearBottleViews()
        {
            for (var i = 0; i < bottleViews.Count; i++)
            {
                if (bottleViews[i].Root != null)
                {
                    UnityEngine.Object.Destroy(bottleViews[i].Root.gameObject);
                }
            }

            bottleViews.Clear();
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

        [RequireComponent(typeof(CanvasRenderer))]
        private sealed class BottleShapeGraphic : MaskableGraphic
        {
            private const int ArcSteps = 14;

            public float StrokeWidth { get; set; } = 4f;

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                var rect = rectTransform.rect;
                if (rect.width <= 0.1f || rect.height <= 0.1f)
                {
                    return;
                }

                var halfWidth = rect.width * 0.28f;
                var topY = rect.yMax - 3f;
                var bottomY = rect.yMin + 3f;
                var radius = halfWidth;
                var centerY = bottomY + radius;
                var path = new List<Vector2>(ArcSteps + 4);
                path.Add(new Vector2(-halfWidth, topY));
                path.Add(new Vector2(-halfWidth, centerY));
                for (var i = 1; i <= ArcSteps; i++)
                {
                    var angle = Mathf.Lerp(Mathf.PI, Mathf.PI * 2f, i / (float)ArcSteps);
                    path.Add(new Vector2(Mathf.Cos(angle) * halfWidth, centerY + Mathf.Sin(angle) * radius));
                }

                path.Add(new Vector2(halfWidth, topY));

                var outlineColor = color;
                for (var i = 0; i < path.Count - 1; i++)
                {
                    AddStrokeSegment(vh, path[i], path[i + 1], StrokeWidth, outlineColor);
                }

                var shineColor = new Color(1f, 1f, 1f, 0.34f);
                AddStrokeSegment(
                    vh,
                    new Vector2(-halfWidth * 0.48f, topY - 18f),
                    new Vector2(-halfWidth * 0.48f, centerY + radius * 0.18f),
                    1.6f,
                    shineColor);
            }

            private static void AddStrokeSegment(VertexHelper vh, Vector2 start, Vector2 end, float width, Color strokeColor)
            {
                var direction = end - start;
                if (direction.sqrMagnitude < 0.01f)
                {
                    return;
                }

                var normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
                var first = AddShapeVertex(vh, start + normal, strokeColor);
                var second = AddShapeVertex(vh, end + normal, strokeColor);
                var third = AddShapeVertex(vh, end - normal, strokeColor);
                var fourth = AddShapeVertex(vh, start - normal, strokeColor);
                vh.AddTriangle(first, second, third);
                vh.AddTriangle(first, third, fourth);
            }

            private static int AddShapeVertex(VertexHelper vh, Vector2 position, Color vertexColor)
            {
                var vertex = UIVertex.simpleVert;
                vertex.position = position;
                vertex.color = vertexColor;
                vh.AddVert(vertex);
                return vh.currentVertCount - 1;
            }
        }

        [RequireComponent(typeof(CanvasRenderer))]
        private sealed class BottleLiquidMaskGraphic : MaskableGraphic
        {
            private const int BottomArcSteps = 18;

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                var rect = rectTransform.rect;
                if (rect.width <= 0.1f || rect.height <= 0.1f)
                {
                    return;
                }

                var xMin = rect.xMin;
                var xMax = rect.xMax;
                var topY = rect.yMax;
                var bottomY = rect.yMin;
                var radius = rect.width * 0.5f;
                var centerY = bottomY + radius;
                var points = new List<Vector2>(BottomArcSteps + 4)
                {
                    new Vector2(xMin, topY),
                    new Vector2(xMin, centerY)
                };

                for (var i = 0; i <= BottomArcSteps; i++)
                {
                    var angle = Mathf.Lerp(Mathf.PI, Mathf.PI * 2f, i / (float)BottomArcSteps);
                    points.Add(new Vector2(Mathf.Cos(angle) * radius, centerY + Mathf.Sin(angle) * radius));
                }

                points.Add(new Vector2(xMax, topY));

                TriangulateFan(vh, points, color);
            }

            private static void TriangulateFan(VertexHelper vh, List<Vector2> points, Color fillColor)
            {
                var center = Vector2.zero;
                for (var i = 0; i < points.Count; i++)
                {
                    center += points[i];
                }

                center /= Mathf.Max(1, points.Count);
                var centerIndex = AddVertex(vh, center, fillColor);
                var indices = new int[points.Count];
                for (var i = 0; i < points.Count; i++)
                {
                    indices[i] = AddVertex(vh, points[i], fillColor);
                }

                for (var i = 0; i < indices.Length; i++)
                {
                    vh.AddTriangle(centerIndex, indices[i], indices[(i + 1) % indices.Length]);
                }
            }

            private static int AddVertex(VertexHelper vh, Vector2 position, Color vertexColor)
            {
                var vertex = UIVertex.simpleVert;
                vertex.position = position;
                vertex.color = vertexColor;
                vh.AddVert(vertex);
                return vh.currentVertCount - 1;
            }
        }

        [RequireComponent(typeof(CanvasRenderer))]
        private sealed class WaterLayerGraphic : MaskableGraphic
        {
            private bool hasTopClip;
            private float topClipY;

            public float WaveAmplitude { get; set; }

            public float WavePhase { get; set; }

            public float SurfaceInset { get; set; }

            public bool IsBottomLayer { get; set; }

            public bool IsTopLayer { get; set; }

            public void SetTopClip(float localY)
            {
                hasTopClip = true;
                topClipY = localY;
                SetVerticesDirty();
            }

            public void ClearTopClip()
            {
                if (!hasTopClip)
                {
                    return;
                }

                hasTopClip = false;
                SetVerticesDirty();
            }

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                var rect = rectTransform.rect;
                if (rect.width <= 0.1f || rect.height <= 0.1f)
                {
                    return;
                }

                var topY = hasTopClip ? Mathf.Min(rect.yMax, topClipY) : rect.yMax;
                if (topY <= rect.yMin + 0.1f)
                {
                    return;
                }

                var topColor = IsTopLayer ? Color.Lerp(color, Color.white, 0.08f) : color;
                var bottomColor = Color.Lerp(color, Color.black, 0.03f);

                var bottomLeft = AddWaterVertex(vh, new Vector2(rect.xMin, rect.yMin), bottomColor);
                var topLeft = AddWaterVertex(vh, new Vector2(rect.xMin, topY), topColor);
                var topRight = AddWaterVertex(vh, new Vector2(rect.xMax, topY), topColor);
                var bottomRight = AddWaterVertex(vh, new Vector2(rect.xMax, rect.yMin), bottomColor);
                vh.AddTriangle(bottomLeft, topLeft, topRight);
                vh.AddTriangle(bottomLeft, topRight, bottomRight);
            }

            private static int AddWaterVertex(VertexHelper vh, Vector2 position, Color vertexColor)
            {
                var vertex = UIVertex.simpleVert;
                vertex.position = position;
                vertex.color = vertexColor;
                vh.AddVert(vertex);
                return vh.currentVertCount - 1;
            }
        }

        [RequireComponent(typeof(CanvasRenderer))]
        private sealed class WaterStreamGraphic : MaskableGraphic
        {
            private Vector2 startPoint;
            private Vector2 endPoint;
            private float alpha;

            public float StreamWidth { get; set; }

            public float WaveOffset { get; set; }

            public void SetVisible(bool visible)
            {
                enabled = visible;
                SetVerticesDirty();
            }

            public void SetEndpoints(Vector2 start, Vector2 end, float visibleAlpha)
            {
                startPoint = start;
                endPoint = end;
                alpha = Mathf.Clamp01(visibleAlpha);
                SetVerticesDirty();
            }

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                if (alpha <= 0.01f)
                {
                    return;
                }

                var width = Mathf.Max(2f, StreamWidth);
                var streamColor = color;
                streamColor.a *= alpha;
                var rectX = startPoint.x;
                var yMin = Mathf.Min(startPoint.y, endPoint.y);
                var yMax = Mathf.Max(startPoint.y, endPoint.y);
                if (yMax - yMin <= 0.1f)
                {
                    return;
                }

                var left = rectX - width * 0.5f;
                var right = rectX + width * 0.5f;
                var bright = Color.Lerp(streamColor, Color.white, 0.14f);

                var bottomLeft = AddStreamVertex(vh, new Vector2(left, yMin), streamColor);
                var topLeft = AddStreamVertex(vh, new Vector2(left, yMax), bright);
                var topRight = AddStreamVertex(vh, new Vector2(right, yMax), bright);
                var bottomRight = AddStreamVertex(vh, new Vector2(right, yMin), streamColor);
                vh.AddTriangle(bottomLeft, topLeft, topRight);
                vh.AddTriangle(bottomLeft, topRight, bottomRight);
            }

            private static int AddStreamVertex(VertexHelper vh, Vector2 position, Color vertexColor)
            {
                var vertex = UIVertex.simpleVert;
                vertex.position = position;
                vertex.color = vertexColor;
                vh.AddVert(vertex);
                return vh.currentVertCount - 1;
            }
        }
    }
}
