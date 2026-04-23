using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    public sealed partial class WaterSortGameView
    {
        private void OnBottleClicked(int index)
        {
            if (settlementShown || index < 0 || index >= bottles.Count)
            {
                return;
            }

            if (selectedBottleIndex < 0 && IsBottleBlockedAsSource(index))
            {
                MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.55f);
                return;
            }

            if (selectedBottleIndex < 0)
            {
                if (bottles[index].Count == 0)
                {
                    MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.55f);
                    return;
                }

                selectedBottleIndex = index;
                MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.82f);
                RefreshBottleSelection();
                return;
            }

            if (selectedBottleIndex == index)
            {
                selectedBottleIndex = -1;
                MiniGameSfxPlayer.Play(MiniGameSfxType.UiBack, 0.55f);
                RefreshBottleSelection();
                return;
            }

            if (lockedSourceBottleIndices.Contains(index))
            {
                MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.55f);
                return;
            }

            var move = TryCreatePourMove(selectedBottleIndex, index);
            if (move != null)
            {
                var sourceBefore = new List<int>(bottles[move.SourceIndex]);
                var targetBefore = new List<int>(bottles[move.TargetIndex]);
                selectedBottleIndex = -1;
                MiniGameSfxPlayer.Play(MiniGameSfxType.MatchSuccess, 0.82f);
                RefreshBottleSelection();
                StartPourAnimation(move, sourceBefore, targetBefore);

                return;
            }

            MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.72f);
            if (bottles[index].Count > 0 && !IsBottleBlockedAsSource(index))
            {
                selectedBottleIndex = index;
            }
            else
            {
                selectedBottleIndex = -1;
            }

            RefreshBottleSelection();
        }

        private bool TryPour(int sourceIndex, int targetIndex)
        {
            var move = TryCreatePourMove(sourceIndex, targetIndex);
            if (move == null)
            {
                return false;
            }

            ApplyPourMove(move);
            return true;
        }

        private PourMove TryCreatePourMove(int sourceIndex, int targetIndex)
        {
            if (sourceIndex == targetIndex || sourceIndex < 0 || sourceIndex >= bottles.Count || targetIndex < 0 || targetIndex >= bottles.Count)
            {
                return null;
            }

            if (IsBottleBlockedAsSource(sourceIndex) || IsBottleBlockedAsTarget(targetIndex))
            {
                return null;
            }

            var source = bottles[sourceIndex];
            var target = bottles[targetIndex];
            if (source.Count == 0 || target.Count >= BottleCapacity)
            {
                return null;
            }

            var color = source[source.Count - 1];
            if (target.Count > 0 && target[target.Count - 1] != color)
            {
                return null;
            }

            var sameColorCount = 0;
            for (var i = source.Count - 1; i >= 0; i--)
            {
                if (source[i] != color)
                {
                    break;
                }

                sameColorCount += 1;
            }

            var amount = Mathf.Min(sameColorCount, BottleCapacity - target.Count);
            if (amount <= 0)
            {
                return null;
            }

            return new PourMove
            {
                SourceIndex = sourceIndex,
                TargetIndex = targetIndex,
                ColorIndex = color,
                Amount = amount
            };
        }

        private void ApplyPourMove(PourMove move)
        {
            if (move == null)
            {
                return;
            }

            var source = bottles[move.SourceIndex];
            var target = bottles[move.TargetIndex];
            ApplyPourMoveToLists(move, source, target);
        }

        private static void ApplyPourMoveToLists(PourMove move, List<int> source, List<int> target)
        {
            if (move == null || source == null || target == null)
            {
                return;
            }

            for (var i = 0; i < move.Amount; i++)
            {
                target.Add(move.ColorIndex);
                source.RemoveAt(source.Count - 1);
            }
        }

        private bool IsBottleBlockedAsSource(int index)
        {
            return lockedSourceBottleIndices.Contains(index) || receiveAnimations.ContainsKey(index) || IsBottleCompletionLocked(index);
        }

        private bool IsBottleBlockedAsTarget(int index)
        {
            return lockedSourceBottleIndices.Contains(index) || IsBottleCompletionLocked(index);
        }

        private bool IsBottleCompletionLocked(int index)
        {
            return index >= 0
                && index < bottles.Count
                && IsCompletedBottle(bottles[index])
                && !receiveAnimations.ContainsKey(index);
        }

        private BottleView GetBottleView(int index)
        {
            if (index < 0 || index >= bottleViews.Count)
            {
                return null;
            }

            return bottleViews[index];
        }

        private WaterStreamGraphic CreatePourStreamGraphic()
        {
            if (streamLayer == null)
            {
                return null;
            }

            var streamObject = CreateRectObject("WaterSortPourStream", streamLayer);
            var graphic = streamObject.AddComponent<WaterStreamGraphic>();
            graphic.raycastTarget = false;
            graphic.SetVisible(false);
            return graphic;
        }

        private void StartPourAnimation(PourMove move, List<int> sourceBefore, List<int> targetBefore)
        {
            if (HostBehaviour == null || move == null)
            {
                ApplyPourMove(move);
                moveCount += 1;
                RefreshHud();
                RefreshBottleViews();
                if (IsPuzzleSolved())
                {
                    CompleteRound();
                }

                return;
            }

            var sourceView = GetBottleView(move.SourceIndex);
            var targetView = GetBottleView(move.TargetIndex);
            if (sourceView == null || targetView == null || sourceView.Root == null || targetView.Root == null)
            {
                ApplyPourMove(move);
                moveCount += 1;
                RefreshHud();
                RefreshBottleViews();
                RefreshBottleSelection();
                if (IsPuzzleSolved())
                {
                    CompleteRound();
                }

                return;
            }

            ApplyPourMove(move);

            var animation = new PourAnimationState
            {
                SourceIndex = move.SourceIndex,
                TargetIndex = move.TargetIndex,
                StreamGraphic = CreatePourStreamGraphic(),
                SourceStartPosition = sourceView.Root.anchoredPosition,
                SourceStartRotation = sourceView.Root.localRotation,
                SourceStartScale = sourceView.Root.localScale,
                HasSourceStartPose = true
            };
            PrepareTargetReceiveAnimation(move.TargetIndex, targetBefore.Count, animation);

            lockedSourceBottleIndices.Add(move.SourceIndex);
            activePourAnimations.Add(animation);
            animation.Routine = HostBehaviour.StartCoroutine(PlayPourAnimationRoutine(animation, move, sourceBefore));
        }

        private IEnumerator PlayPourAnimationRoutine(PourAnimationState animation, PourMove move, List<int> sourceBefore)
        {
            var sourceView = animation != null && animation.SourceIndex >= 0 && animation.SourceIndex < bottleViews.Count ? bottleViews[animation.SourceIndex] : null;
            var targetView = animation != null && animation.TargetIndex >= 0 && animation.TargetIndex < bottleViews.Count ? bottleViews[animation.TargetIndex] : null;
            if (sourceView == null || targetView == null || sourceView.Root == null || targetView.Root == null)
            {
                FinalizePourAnimation(animation, false);
                yield break;
            }

            var sourceRoot = sourceView.Root;
            var targetRoot = targetView.Root;
            var sourceStartPosition = sourceRoot.anchoredPosition;
            var sourceStartRotation = sourceRoot.localRotation;
            var sourceStartScale = sourceRoot.localScale;
            var dockSide = targetRoot.anchoredPosition.x >= sourceRoot.anchoredPosition.x ? -1f : 1f;

            var sourceAfter = new List<int>(sourceBefore);
            for (var i = 0; i < move.Amount && sourceAfter.Count > 0; i++)
            {
                sourceAfter.RemoveAt(sourceAfter.Count - 1);
            }

            var initialSourceFill = sourceBefore.Count;
            var finalSourceFill = sourceAfter.Count;
            var initialPourRotation = ResolveSourcePourRotation(sourceView, dockSide, initialSourceFill);
            var initialPourPosition = ResolveSourcePourPosition(sourceRoot, targetRoot, initialPourRotation, dockSide);
            var finalPourRotation = ResolveSourcePourRotation(sourceView, dockSide, finalSourceFill);
            var finalPourPosition = ResolveSourcePourPosition(sourceRoot, targetRoot, finalPourRotation, dockSide);
            var moveDuration = GetPourMoveDuration(Vector2.Distance(sourceStartPosition, initialPourPosition));
            var pourDuration = GetPourFlowDuration(move.Amount);
            var returnDuration = GetPourMoveDuration(Vector2.Distance(sourceStartPosition, finalPourPosition));
            var receiveSpeed = GetPourReceiveSpeed(move.Amount, pourDuration);
            var moveEndTime = moveDuration;
            var pourStartTime = moveEndTime + PourPreFlowDelay;
            var pourEndTime = pourStartTime + pourDuration;
            var totalDuration = pourEndTime + returnDuration;
            if (animation != null)
            {
                animation.ReceiveSpeed = receiveSpeed;
            }

            SetBottleVisualState(sourceView, sourceBefore, sourceBefore.Count, Mathf.Max(0, sourceBefore.Count - 1), IdleWaveAmplitude);

            if (animation != null && animation.StreamGraphic != null)
            {
                animation.StreamGraphic.color = GetWaterColor(move.ColorIndex);
                animation.StreamGraphic.StreamWidth = 6f;
                animation.StreamGraphic.SetVisible(false);
            }

            var elapsed = 0f;
            while (elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;
                var moveProgress = SmoothStep01(GetStageProgress(elapsed, 0f, moveDuration));
                var rawPourProgress = SmoothStep01(GetStageProgress(elapsed, pourStartTime, pourDuration));
                var returnProgress = SmoothStep01(GetStageProgress(elapsed, pourEndTime, returnDuration));
                var pourProgress = returnProgress > 0f ? 1f : rawPourProgress;
                var isWaterFlowing = rawPourProgress > 0f && returnProgress <= 0f;
                if (isWaterFlowing)
                {
                    BeginTargetReceiveAnimation(animation, move);
                }
                else
                {
                    EndTargetReceiveAnimation(animation);
                }

                var poseProgress = returnProgress > 0f ? 1f - returnProgress : moveProgress;
                var activeWave = Mathf.Lerp(ActiveWaveAmplitude, IdleWaveAmplitude, GetStageProgress(elapsed, pourEndTime, returnDuration));
                var targetWave = isWaterFlowing ? activeWave : IdleWaveAmplitude;
                var sourceFill = Mathf.Lerp(sourceBefore.Count, sourceAfter.Count, pourProgress);
                var sourcePoseRotation = ResolveSourcePourRotation(sourceView, dockSide, sourceFill);
                var sourcePourPosition = ResolveSourcePourPosition(sourceRoot, targetRoot, sourcePoseRotation, dockSide);

                sourceRoot.anchoredPosition = Vector2.Lerp(sourceStartPosition, sourcePourPosition, poseProgress);
                sourceRoot.localRotation = Quaternion.Slerp(sourceStartRotation, sourcePoseRotation, poseProgress);
                sourceRoot.localScale = Vector3.one;
                SetBottleFillRotation(sourceView, sourceRoot.localRotation);
                SetBottleFillRotation(targetView, targetRoot.localRotation);

                SetBottleVisualState(sourceView, sourceBefore, sourceFill, Mathf.Max(0, Mathf.CeilToInt(sourceFill) - 1), activeWave);
                ApplySourceLiquidTopClip(sourceView, sourceRoot);
                SetReceiveAnimationWave(move.TargetIndex, targetWave);

                var mouthPt = GetBottleMouthPoint(sourceRoot, true);
                if (animation != null && animation.StreamGraphic != null)
                {
                    if (isWaterFlowing)
                    {
                        animation.StreamGraphic.SetVisible(true);
                        animation.StreamGraphic.StreamWidth = 6f;
                        var endPt = GetBottlePourTargetPoint(targetRoot, mouthPt.x, GetReceiveVisualFill(move.TargetIndex));
                        animation.StreamGraphic.SetEndpoints(mouthPt, endPt, 1f);
                    }
                    else
                    {
                        animation.StreamGraphic.SetVisible(false);
                    }
                }

                yield return null;
            }

            EndTargetReceiveAnimation(animation);
            sourceRoot.anchoredPosition = sourceStartPosition;
            sourceRoot.localRotation = sourceStartRotation;
            sourceRoot.localScale = sourceStartScale;
            SetBottleFillRotation(sourceView, sourceStartRotation);
            SetBottleFillRotation(targetView, targetRoot.localRotation);

            SetBottleVisualState(sourceView, bottles[animation.SourceIndex], bottles[animation.SourceIndex].Count, -1, IdleWaveAmplitude);

            FinalizePourAnimation(animation, true);
        }

        private void StopPourAnimation()
        {
            for (var i = activePourAnimations.Count - 1; i >= 0; i--)
            {
                var animation = activePourAnimations[i];
                if (animation != null && HostBehaviour != null && animation.Routine != null)
                {
                    HostBehaviour.StopCoroutine(animation.Routine);
                }

                RestorePourSourcePose(animation);
                DestroyPourStreamGraphic(animation);
            }

            activePourAnimations.Clear();
            lockedSourceBottleIndices.Clear();
            receiveAnimations.Clear();

            RestoreBottleRoots();
            RefreshBottleViews();
            RefreshBottleSelection();
            RefreshHud();
        }

        private void AdvanceBottleReceiveAnimations(float deltaTime)
        {
            if (receiveAnimations.Count == 0)
            {
                return;
            }

            var completed = new List<int>();
            foreach (var pair in receiveAnimations)
            {
                var index = pair.Key;
                var receive = pair.Value;
                if (receive == null)
                {
                    completed.Add(index);
                    continue;
                }

                if (receive.ActiveSpeed > PourReceiveMinSpeed)
                {
                    receive.VisualFill = Mathf.MoveTowards(
                        receive.VisualFill,
                        receive.TargetFill,
                        receive.ActiveSpeed * Mathf.Max(0f, deltaTime));
                }

                RefreshReceiveAnimationView(index, receive, ActiveWaveAmplitude);
                if (receive.PendingFlowCount <= 0
                    && receive.ActiveSpeed <= PourReceiveMinSpeed
                    && Mathf.Abs(receive.VisualFill - receive.TargetFill) <= BottleEmptyEpsilon)
                {
                    completed.Add(index);
                }
            }

            var removedAny = false;
            for (var i = 0; i < completed.Count; i++)
            {
                var index = completed[i];
                receiveAnimations.Remove(index);
                RefreshBottleCap(index);
                removedAny = true;
            }

            if (removedAny)
            {
                TryCompleteRoundAfterAnimations();
            }
        }

        private void PrepareTargetReceiveAnimation(int targetIndex, float visualFill, PourAnimationState animation)
        {
            if (targetIndex < 0 || targetIndex >= bottles.Count)
            {
                return;
            }

            if (!receiveAnimations.TryGetValue(targetIndex, out var receive) || receive == null)
            {
                receive = new BottleReceiveAnimationState
                {
                    VisualFill = Mathf.Clamp(visualFill, 0f, BottleCapacity),
                    TargetFill = Mathf.Clamp(visualFill, 0f, BottleCapacity)
                };
                receiveAnimations[targetIndex] = receive;
                RefreshReceiveAnimationView(targetIndex, receive, IdleWaveAmplitude);
            }

            if (animation != null)
            {
                animation.HasPendingReceive = true;
            }
            receive.PendingFlowCount += 1;
        }

        private void BeginTargetReceiveAnimation(PourAnimationState animation, PourMove move)
        {
            if (animation == null || move == null || animation.IsReceiving)
            {
                return;
            }

            if (!receiveAnimations.TryGetValue(animation.TargetIndex, out var receive) || receive == null)
            {
                PrepareTargetReceiveAnimation(move.TargetIndex, bottles[move.TargetIndex].Count - move.Amount, animation);
                receiveAnimations.TryGetValue(move.TargetIndex, out receive);
            }

            if (receive == null)
            {
                return;
            }

            if (animation.HasPendingReceive)
            {
                animation.HasPendingReceive = false;
                receive.PendingFlowCount = Mathf.Max(0, receive.PendingFlowCount - 1);
            }

            receive.TargetFill = Mathf.Clamp(receive.TargetFill + move.Amount, 0f, BottleCapacity);
            receive.ActiveSpeed += Mathf.Max(PourReceiveMinSpeed, animation.ReceiveSpeed);
            animation.IsReceiving = true;
        }

        private void EndTargetReceiveAnimation(PourAnimationState animation)
        {
            if (animation == null || !animation.IsReceiving)
            {
                return;
            }

            if (receiveAnimations.TryGetValue(animation.TargetIndex, out var receive) && receive != null)
            {
                receive.ActiveSpeed = Mathf.Max(0f, receive.ActiveSpeed - Mathf.Max(PourReceiveMinSpeed, animation.ReceiveSpeed));
                if (receive.ActiveSpeed <= PourReceiveMinSpeed)
                {
                    receive.VisualFill = receive.TargetFill;
                    RefreshReceiveAnimationView(animation.TargetIndex, receive, IdleWaveAmplitude);
                }
            }

            animation.IsReceiving = false;
        }

        private float GetReceiveVisualFill(int targetIndex)
        {
            if (targetIndex < 0 || targetIndex >= bottleViews.Count || targetIndex >= bottles.Count || !receiveAnimations.TryGetValue(targetIndex, out var receive) || receive == null)
            {
                return targetIndex >= 0 && targetIndex < bottles.Count ? bottles[targetIndex].Count : 0f;
            }

            return receive.VisualFill;
        }

        private void RefreshReceiveAnimationView(int targetIndex, BottleReceiveAnimationState receive, float waveAmplitude)
        {
            if (targetIndex < 0 || targetIndex >= bottleViews.Count || targetIndex >= bottles.Count || receive == null)
            {
                return;
            }

            SetBottleVisualState(
                bottleViews[targetIndex],
                bottles[targetIndex],
                receive.VisualFill,
                Mathf.Max(0, Mathf.CeilToInt(receive.VisualFill) - 1),
                waveAmplitude);
        }

        private void CancelPendingTargetReceiveAnimation(PourAnimationState animation)
        {
            if (animation == null || !animation.HasPendingReceive)
            {
                return;
            }

            if (receiveAnimations.TryGetValue(animation.TargetIndex, out var receive) && receive != null)
            {
                receive.PendingFlowCount = Mathf.Max(0, receive.PendingFlowCount - 1);
            }

            animation.HasPendingReceive = false;
        }

        private void FinalizePourAnimation(PourAnimationState animation, bool completedNaturally)
        {
            if (animation == null)
            {
                return;
            }

            activePourAnimations.Remove(animation);
            lockedSourceBottleIndices.Remove(animation.SourceIndex);
            CancelPendingTargetReceiveAnimation(animation);
            EndTargetReceiveAnimation(animation);
            DestroyPourStreamGraphic(animation);

            if (completedNaturally)
            {
                moveCount += 1;
            }

            RefreshHud();
            if (activePourAnimations.Count == 0)
            {
                RefreshBottleSelection();
                TryCompleteRoundAfterAnimations();
            }
        }

        private void SetReceiveAnimationWave(int targetIndex, float waveAmplitude)
        {
            if (targetIndex < 0 || targetIndex >= bottleViews.Count || !receiveAnimations.TryGetValue(targetIndex, out var receive) || receive == null)
            {
                return;
            }

            RefreshReceiveAnimationView(targetIndex, receive, waveAmplitude);
        }

        private void DestroyPourStreamGraphic(PourAnimationState animation)
        {
            if (animation == null || animation.StreamGraphic == null)
            {
                return;
            }

            animation.StreamGraphic.SetVisible(false);
            UnityEngine.Object.Destroy(animation.StreamGraphic.gameObject);
            animation.StreamGraphic = null;
        }

        private void RestorePourSourcePose(PourAnimationState animation)
        {
            if (animation == null || !animation.HasSourceStartPose || animation.SourceIndex < 0 || animation.SourceIndex >= bottleViews.Count)
            {
                return;
            }

            var sourceView = bottleViews[animation.SourceIndex];
            if (sourceView == null || sourceView.Root == null)
            {
                return;
            }

            sourceView.Root.anchoredPosition = animation.SourceStartPosition;
            sourceView.Root.localRotation = animation.SourceStartRotation;
            sourceView.Root.localScale = animation.SourceStartScale;
            SetBottleFillRotation(sourceView, animation.SourceStartRotation);
        }

        private void RestoreBottleRoots()
        {
            for (var i = 0; i < bottleViews.Count; i++)
            {
                var root = bottleViews[i].Root;
                if (root == null)
                {
                    continue;
                }

                root.localRotation = Quaternion.identity;
                root.localScale = Vector3.one;
                SetBottleFillRotation(bottleViews[i], Quaternion.identity);
            }
        }

        private static void SetBottleFillRotation(BottleView view, Quaternion bottleRotation)
        {
            if (view == null || view.FillArea == null)
            {
                return;
            }

            view.FillArea.localRotation = Quaternion.Inverse(bottleRotation);
            view.FillArea.localScale = Vector3.one;
            view.FillArea.anchoredPosition = Vector2.zero;
        }

        private Vector2 GetBottleMouthPoint(RectTransform bottleRoot, bool source)
        {
            if (bottleRoot == null || streamLayer == null)
            {
                return Vector2.zero;
            }

            var rect = bottleRoot.rect;
            var z = NormalizeAngle(bottleRoot.localEulerAngles.z);
            var localPoint = source
                ? GetSourcePourMouthLocalPoint(rect, z < 0f ? 1f : -1f)
                : new Vector3(0f, rect.yMax - PourStreamEndInset, 0f);
            var worldPoint = bottleRoot.TransformPoint(localPoint);
            var result = streamLayer.InverseTransformPoint(worldPoint);
            return result;
        }

        private static void ApplySourceLiquidTopClip(BottleView sourceView, RectTransform sourceRoot)
        {
            if (sourceView == null || sourceRoot == null || sourceView.Segments == null)
            {
                return;
            }

            var lipSide = GetSourcePourLipSide(sourceRoot);
            var mouthWorldPoint = sourceRoot.TransformPoint(GetSourcePourMouthLocalPoint(sourceRoot.rect, lipSide));
            for (var i = 0; i < sourceView.Segments.Length; i++)
            {
                var segment = sourceView.Segments[i];
                if (segment == null || !segment.gameObject.activeSelf)
                {
                    continue;
                }

                var segmentLocalMouth = segment.rectTransform.InverseTransformPoint(mouthWorldPoint);
                segment.SetTopClip(segmentLocalMouth.y - PourMouthLiquidClipInset);
            }
        }

        private Quaternion ResolveSourcePourRotation(BottleView sourceView, float dockSide, float sourceFill)
        {
            var adjustedSourceFill = Mathf.Max(sourceFill, BottleEmptyEpsilon);
            if (sourceView == null || sourceView.Root == null || sourceView.FillArea == null)
            {
                return GetSourcePourRotation(dockSide, adjustedSourceFill);
            }

            var root = sourceView.Root;
            var fillArea = sourceView.FillArea;
            var originalRootRotation = root.localRotation;
            var originalFillRotation = fillArea.localRotation;
            var originalFillScale = fillArea.localScale;
            var originalFillPosition = fillArea.anchoredPosition;
            var originalAnchorMin = fillArea.anchorMin;
            var originalAnchorMax = fillArea.anchorMax;
            var originalOffsetMin = fillArea.offsetMin;
            var originalOffsetMax = fillArea.offsetMax;

            var minDegrees = 0f;
            var maxDegrees = MaxPourTiltDegrees;
            var minDelta = MeasureSourceSurfaceToMouthDelta(sourceView, dockSide, adjustedSourceFill, minDegrees);
            var maxDelta = MeasureSourceSurfaceToMouthDelta(sourceView, dockSide, adjustedSourceFill, maxDegrees);
            var resultDegrees = maxDegrees;
            if (minDelta >= -0.5f)
            {
                resultDegrees = minDegrees;
            }
            else if (maxDelta > 0.5f)
            {
                var low = minDegrees;
                var high = maxDegrees;
                for (var i = 0; i < BottlePourTiltSearchIterations; i++)
                {
                    var mid = (low + high) * 0.5f;
                    var delta = MeasureSourceSurfaceToMouthDelta(sourceView, dockSide, adjustedSourceFill, mid);
                    if (delta < 0f)
                    {
                        low = mid;
                    }
                    else
                    {
                        high = mid;
                    }
                }

                resultDegrees = high;
            }

            var visualDegrees = GetPourTiltDegrees(adjustedSourceFill);
            resultDegrees = Mathf.Max(resultDegrees, visualDegrees);

            root.localRotation = originalRootRotation;
            fillArea.localRotation = originalFillRotation;
            fillArea.localScale = originalFillScale;
            fillArea.anchoredPosition = originalFillPosition;
            fillArea.anchorMin = originalAnchorMin;
            fillArea.anchorMax = originalAnchorMax;
            fillArea.offsetMin = originalOffsetMin;
            fillArea.offsetMax = originalOffsetMax;

            return Quaternion.Euler(0f, 0f, dockSide * resultDegrees);
        }

        private float MeasureSourceSurfaceToMouthDelta(BottleView sourceView, float dockSide, float sourceFill, float tiltDegrees)
        {
            sourceView.Root.localRotation = Quaternion.Euler(0f, 0f, dockSide * tiltDegrees);
            SetBottleFillRotation(sourceView, sourceView.Root.localRotation);
            ConfigureWholeLiquidArea(sourceView, sourceFill);

            var lipSide = GetSourcePourLipSide(sourceView.Root);
            var liquidPoint = GetSourceLiquidPourPoint(sourceView, sourceFill, lipSide);
            var mouthPoint = GetBottleMouthPoint(sourceView.Root, true);
            return liquidPoint.y - mouthPoint.y;
        }

        private Vector2 GetSourceLiquidPourPoint(BottleView sourceView, float sourceFill, float lipSide)
        {
            if (sourceView == null || sourceView.FillArea == null || streamLayer == null)
            {
                return Vector2.zero;
            }

            var maskRect = sourceView.LiquidMask != null ? sourceView.LiquidMask.rect : Rect.zero;
            var fillHeight = maskRect.height * Mathf.Max(0f, sourceView.FillArea.anchorMax.y - sourceView.FillArea.anchorMin.y);
            var localPoint = new Vector3(lipSide * maskRect.width * 0.48f, fillHeight, 0f);
            var worldPoint = sourceView.FillArea.TransformPoint(localPoint);
            return streamLayer.InverseTransformPoint(worldPoint);
        }

        private Vector2 GetBottlePourTargetPoint(RectTransform bottleRoot, float streamX, float targetFill)
        {
            if (bottleRoot == null || streamLayer == null)
            {
                return new Vector2(streamX, 0f);
            }

            var rect = bottleRoot.rect;
            var fillHeight = Mathf.Max(1f, rect.height - BottleFillBottomInset - BottleFillTopInset);
            var surfaceY = rect.yMin
                + BottleFillBottomInset
                + Mathf.Clamp(targetFill / BottleCapacity, 0f, BottleFullFillRatio) * fillHeight
                - PourStreamTargetSurfaceInset;
            var worldPoint = bottleRoot.TransformPoint(new Vector3(0f, surfaceY, 0f));
            var result = streamLayer.InverseTransformPoint(worldPoint);
            result.x = streamX;
            return result;
        }

        private static Quaternion GetSourcePourRotation(float dockSide, float sourceFill)
        {
            return Quaternion.Euler(0f, 0f, dockSide * GetPourTiltDegrees(sourceFill));
        }

        private static float GetPourTiltDegrees(float sourceFill)
        {
            var emptyRatio = 1f - Mathf.Clamp01(sourceFill / BottleCapacity);
            return Mathf.Lerp(MinPourTiltDegrees, MaxPourTiltDegrees, emptyRatio);
        }

        private static Vector2 ResolveSourcePourPosition(RectTransform sourceRoot, RectTransform targetRoot, Quaternion sourceRotation, float dockSide)
        {
            var targetRect = targetRoot.rect;
            var sourceRect = sourceRoot.rect;
            var targetStreamX = targetRoot.anchoredPosition.x - dockSide * targetRect.width * 0.08f;
            var targetTopY = targetRoot.anchoredPosition.y + targetRect.yMax;
            var mouthLocal = (Vector2)GetSourcePourMouthLocalPoint(sourceRect, GetSourcePourLipSide(dockSide));
            var rotatedMouth = (Vector2)(sourceRotation * mouthLocal);
            return new Vector2(
                targetStreamX - rotatedMouth.x,
                targetTopY + PourMoveLift + PourExtraLift - rotatedMouth.y);
        }

        private static float GetSourcePourLipSide(RectTransform sourceRoot)
        {
            if (sourceRoot == null)
            {
                return 1f;
            }

            return NormalizeAngle(sourceRoot.localEulerAngles.z) < 0f ? 1f : -1f;
        }

        private static float GetSourcePourLipSide(float dockSide)
        {
            return dockSide < 0f ? 1f : -1f;
        }

        private static Vector3 GetSourcePourMouthLocalPoint(Rect rect, float lipSide)
        {
            var innerHalfWidth = Mathf.Max(1f, rect.width * 0.5f - BottleFillHorizontalInset - 2f);
            return new Vector3(
                lipSide * innerHalfWidth,
                rect.yMax - BottleFillTopInset,
                0f);
        }

        private static float SmoothStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static float GetPourFlowDuration(int amount)
        {
            return (PourBaseDuration + Mathf.Max(1, amount) * PourDurationPerLayer) * PourDurationScale * PourFlowDurationRatio;
        }

        private static float GetPourReceiveSpeed(int amount, float duration)
        {
            if (duration <= BottleEmptyEpsilon)
            {
                return BottleCapacity;
            }

            return Mathf.Max(PourReceiveMinSpeed, Mathf.Max(1, amount) / duration);
        }

        private static float GetPourMoveDuration(float distance)
        {
            return PourMoveBaseDuration + Mathf.Max(0f, distance) / PourMoveSpeed;
        }

        private static float GetStageProgress(float elapsed, float startTime, float duration)
        {
            if (duration <= BottleEmptyEpsilon)
            {
                return elapsed >= startTime ? 1f : 0f;
            }

            return Mathf.Clamp01((elapsed - startTime) / duration);
        }

        private static float NormalizeAngle(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f)
            {
                degrees -= 360f;
            }

            return degrees;
        }
    }
}
