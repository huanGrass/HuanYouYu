using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    [Serializable]
    internal sealed class BraceletUnlinkLevelData
    {
        private const string SavedFourthLevelResourcePath = "Levels/bracelet-unlink-level-004";
        private const float InitialGoldLoopGapClearance = 55f;
        public const float HorizontalSpacing = 169f;
        public const float VerticalSpacing = 145f;

        private static readonly int[] LegacyRows = { 4, 3, 4, 3 };

        public int LevelId;
        public int[] RowLengths = (int[])LegacyRows.Clone();
        public BraceletRingSlotData[] Rings = Array.Empty<BraceletRingSlotData>();
        public BraceletEdgeSlotData[] Edges = Array.Empty<BraceletEdgeSlotData>();

        public int SlotCount
        {
            get { return Rings != null ? Rings.Length : 0; }
        }

        public BraceletUnlinkLevelData Clone()
        {
            return JsonUtility.FromJson<BraceletUnlinkLevelData>(JsonUtility.ToJson(this));
        }

        public void ApplyLegacyLayoutIfMissing()
        {
            if (RowLengths == null || RowLengths.Length == 0)
            {
                RowLengths = (int[])LegacyRows.Clone();
            }
        }

        public static BraceletUnlinkLevelData[] CreateBuiltInLevels()
        {
            var levels = new[]
            {
                CreateThreeRingMapTutorial(),
                CreateFiveRingMixedLevel(),
                CreateSevenRingDependencyLevel(),
                CreateEightRingHubLevel(),
                CreateTenRingCombinedLevel(),
                LoadSavedFourthLevel()
            };
            var levelIds = new[] { 1, 2, 4, 5, 7, 8 };
            for (var i = 0; i < levels.Length; i++)
            {
                levels[i].LevelId = levelIds[i];
                levels[i].EnsureInitialGoldLoopGapClearance();
            }
            return levels;
        }

        public static BraceletUnlinkLevelData LoadSavedFourthLevel()
        {
            var asset = Resources.Load<TextAsset>(SavedFourthLevelResourcePath);
            if (asset == null)
            {
                throw new InvalidOperationException("Missing bracelet level resource: " + SavedFourthLevelResourcePath);
            }
            var data = JsonUtility.FromJson<BraceletUnlinkLevelData>(asset.text);
            if (data == null || data.Rings == null || data.Edges == null)
            {
                throw new InvalidOperationException("Invalid bracelet level resource: " + SavedFourthLevelResourcePath);
            }
            data.ApplyLegacyLayoutIfMissing();
            return data;
        }

        public static BraceletUnlinkLevelData CreateEmpty()
        {
            return CreateEmpty(LegacyRows);
        }

        public static BraceletUnlinkLevelData CreateEmpty(params int[] rowLengths)
        {
            var safeRows = NormalizeRows(rowLengths);
            var data = new BraceletUnlinkLevelData
            {
                RowLengths = safeRows
            };
            var slotCount = CountSlots(safeRows);
            data.Rings = new BraceletRingSlotData[slotCount];
            for (var i = 0; i < slotCount; i++)
            {
                data.Rings[i] = new BraceletRingSlotData
                {
                    Kind = BraceletRingKind.Empty,
                    GapAngle = 0f,
                    ColorIndex = i
                };
            }

            var pairs = data.BuildNeighborPairs();
            data.Edges = new BraceletEdgeSlotData[pairs.Count];
            for (var i = 0; i < pairs.Count; i++)
            {
                data.Edges[i] = new BraceletEdgeSlotData
                {
                    SlotA = pairs[i].x,
                    SlotB = pairs[i].y,
                    Kind = BraceletEdgeKind.Empty
                };
            }
            return data;
        }

        public Vector2 GetSlotPosition(int slot)
        {
            ApplyLegacyLayoutIfMissing();
            if (slot < 0 || slot >= CountSlots(RowLengths))
            {
                return Vector2.zero;
            }

            var firstSlotInRow = 0;
            for (var row = 0; row < RowLengths.Length; row++)
            {
                var rowLength = RowLengths[row];
                if (slot < firstSlotInRow + rowLength)
                {
                    var column = slot - firstSlotInRow;
                    var x = (column - (rowLength - 1) * 0.5f) * HorizontalSpacing;
                    var y = ((RowLengths.Length - 1) * 0.5f - row) * VerticalSpacing;
                    return new Vector2(x, y);
                }
                firstSlotInRow += rowLength;
            }
            return Vector2.zero;
        }

        public BraceletEdgeSlotData FindEdge(int first, int second)
        {
            if (Edges == null)
            {
                return null;
            }
            for (var i = 0; i < Edges.Length; i++)
            {
                var edge = Edges[i];
                if (edge != null
                    && (edge.SlotA == first && edge.SlotB == second || edge.SlotA == second && edge.SlotB == first))
                {
                    return edge;
                }
            }
            return null;
        }

        public void SetRing(int slot, BraceletRingKind kind, float gapAngle = 0f, int colorIndex = -1)
        {
            if (slot < 0 || slot >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            Rings[slot].Kind = kind;
            Rings[slot].GapAngle = gapAngle;
            if (colorIndex >= 0)
            {
                Rings[slot].ColorIndex = colorIndex;
            }
            if (kind != BraceletRingKind.Empty)
            {
                return;
            }
            for (var i = 0; i < Edges.Length; i++)
            {
                if (Edges[i].SlotA == slot || Edges[i].SlotB == slot)
                {
                    Edges[i].Kind = BraceletEdgeKind.Empty;
                }
            }
        }

        public void SetEdge(int first, int second, BraceletEdgeKind kind)
        {
            var edge = FindEdge(first, second);
            if (edge == null)
            {
                throw new InvalidOperationException("Bracelet edge is not a six-direction neighbor: " + first + "-" + second);
            }
            if (Rings[first].Kind == BraceletRingKind.Empty || Rings[second].Kind == BraceletRingKind.Empty)
            {
                throw new InvalidOperationException("Bracelet edge requires rings on both ends: " + first + "-" + second);
            }
            edge.Kind = kind;
            if (kind != BraceletEdgeKind.Empty)
            {
                EnsureInitialGoldLoopGapClearance();
            }
        }

        public void SetAttachedEdge(int owner, int target)
        {
            var edge = FindEdge(owner, target);
            if (edge == null)
            {
                throw new InvalidOperationException("Bracelet edge is not a six-direction neighbor: " + owner + "-" + target);
            }
            SetEdge(owner, target, edge.SlotA == owner ? BraceletEdgeKind.AttachedToA : BraceletEdgeKind.AttachedToB);
        }

        public bool EnsureInitialGoldLoopGapClearance()
        {
            var changed = false;
            if (Edges == null || Rings == null)
            {
                return false;
            }
            for (var slot = 0; slot < Rings.Length; slot++)
            {
                if (Rings[slot] == null || Rings[slot].Kind != BraceletRingKind.Open)
                {
                    continue;
                }
                var currentAngle = Rings[slot].GapAngle;
                var currentClearance = GetMinimumGoldLoopGapDistance(slot, currentAngle);
                if (currentClearance >= InitialGoldLoopGapClearance)
                {
                    continue;
                }
                var safestAngle = currentAngle;
                var safestClearance = currentClearance;
                var nearestSafeAngle = currentAngle;
                var nearestSafeAdjustment = 360f;
                var foundSafeAngle = false;
                for (var candidate = -180; candidate < 180; candidate++)
                {
                    var clearance = GetMinimumGoldLoopGapDistance(slot, candidate);
                    var adjustment = Mathf.Abs(Mathf.DeltaAngle(currentAngle, candidate));
                    if (clearance >= InitialGoldLoopGapClearance && adjustment < nearestSafeAdjustment)
                    {
                        nearestSafeAngle = candidate;
                        nearestSafeAdjustment = adjustment;
                        foundSafeAngle = true;
                    }
                    if (clearance > safestClearance + 0.01f)
                    {
                        safestAngle = candidate;
                        safestClearance = clearance;
                    }
                }
                var bestAngle = foundSafeAngle ? nearestSafeAngle : safestAngle;
                if (Mathf.Abs(Mathf.DeltaAngle(currentAngle, bestAngle)) > 0.01f)
                {
                    Rings[slot].GapAngle = bestAngle;
                    changed = true;
                }
            }
            return changed;
        }

        private float GetMinimumGoldLoopGapDistance(int slot, float gapAngle)
        {
            var minimum = 180f;
            var hasConstraint = false;
            for (var i = 0; i < Edges.Length; i++)
            {
                var edge = Edges[i];
                if (edge == null
                    || edge.Kind == BraceletEdgeKind.Empty
                    || edge.SlotA != slot && edge.SlotB != slot)
                {
                    continue;
                }
                var other = edge.SlotA == slot ? edge.SlotB : edge.SlotA;
                var direction = GetSlotPosition(other) - GetSlotPosition(slot);
                var loopAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                minimum = Mathf.Min(minimum, Mathf.Abs(Mathf.DeltaAngle(gapAngle, loopAngle)));
                hasConstraint = true;
            }
            return hasConstraint ? minimum : 180f;
        }

        public List<Vector2Int> BuildNeighborPairs()
        {
            var result = new List<Vector2Int>();
            for (var first = 0; first < SlotCount; first++)
            {
                for (var second = first + 1; second < SlotCount; second++)
                {
                    var distance = Vector2.Distance(GetSlotPosition(first), GetSlotPosition(second));
                    if (Mathf.Abs(distance - HorizontalSpacing) < 3f)
                    {
                        result.Add(new Vector2Int(first, second));
                    }
                }
            }
            return result;
        }

        private static BraceletUnlinkLevelData CreateThreeRingMapTutorial()
        {
            var data = CreateEmpty(2, 1);
            data.SetRing(0, BraceletRingKind.Open, 180f, 0);
            data.SetRing(1, BraceletRingKind.Open, 0f, 1);
            data.SetRing(2, BraceletRingKind.Open, -90f, 2);
            data.SetEdge(0, 2, BraceletEdgeKind.Map);
            data.SetEdge(1, 2, BraceletEdgeKind.Map);
            return data;
        }

        private static BraceletUnlinkLevelData CreateFiveRingMixedLevel()
        {
            var data = CreateFilledLevel(new[] { 3, 2 }, BraceletRingKind.Open);
            data.SetAttachedEdge(0, 3);
            data.SetEdge(0, 1, BraceletEdgeKind.Map);
            data.SetEdge(1, 3, BraceletEdgeKind.Map);
            data.SetAttachedEdge(1, 4);
            data.SetEdge(2, 4, BraceletEdgeKind.Map);
            return data;
        }

        private static BraceletUnlinkLevelData CreateSixRingChainLevel()
        {
            var data = CreateFilledLevel(new[] { 3, 2, 3 }, BraceletRingKind.Open, 0, 1, 2, 3, 4, 6);
            data.SetEdge(0, 3, BraceletEdgeKind.Map);
            data.SetEdge(1, 2, BraceletEdgeKind.Map);
            data.SetEdge(1, 3, BraceletEdgeKind.Map);
            data.SetEdge(2, 4, BraceletEdgeKind.Map);
            data.SetEdge(3, 6, BraceletEdgeKind.Map);
            return data;
        }

        private static BraceletUnlinkLevelData CreateSevenRingDependencyLevel()
        {
            var data = CreateFilledLevel(new[] { 4, 3 }, BraceletRingKind.Open);
            data.SetAttachedEdge(0, 4);
            data.SetEdge(0, 1, BraceletEdgeKind.Map);
            data.SetEdge(1, 4, BraceletEdgeKind.Map);
            data.SetAttachedEdge(1, 5);
            data.SetEdge(2, 5, BraceletEdgeKind.Map);
            data.SetEdge(2, 3, BraceletEdgeKind.Map);
            data.SetAttachedEdge(3, 6);
            return data;
        }

        private static BraceletUnlinkLevelData CreateEightRingHubLevel()
        {
            var data = CreateFilledLevel(new[] { 4, 3, 4 }, BraceletRingKind.Open, 0, 1, 2, 4, 5, 6, 7, 8);
            data.SetAttachedEdge(4, 0);
            data.SetAttachedEdge(4, 1);
            data.SetAttachedEdge(1, 5);
            data.SetEdge(2, 5, BraceletEdgeKind.Map);
            data.SetAttachedEdge(2, 6);
            data.SetAttachedEdge(4, 7);
            data.SetAttachedEdge(7, 8);
            return data;
        }

        private static BraceletUnlinkLevelData CreateNineRingBranchLevel()
        {
            var data = CreateFilledLevel(new[] { 4, 3, 4 }, BraceletRingKind.Open, 0, 1, 2, 4, 5, 6, 7, 8, 9);
            data.SetAttachedEdge(4, 0);
            data.SetAttachedEdge(4, 1);
            data.SetAttachedEdge(1, 5);
            data.SetAttachedEdge(5, 2);
            data.SetAttachedEdge(2, 6);
            data.SetAttachedEdge(6, 9);
            data.SetAttachedEdge(4, 7);
            data.SetEdge(7, 8, BraceletEdgeKind.Map);
            return data;
        }

        private static BraceletUnlinkLevelData CreateTenRingCombinedLevel()
        {
            var data = CreateFilledLevel(new[] { 4, 3, 4 }, BraceletRingKind.Open, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9);
            data.SetAttachedEdge(4, 0);
            data.SetAttachedEdge(4, 1);
            data.SetAttachedEdge(1, 5);
            data.SetAttachedEdge(5, 2);
            data.SetAttachedEdge(2, 3);
            data.SetAttachedEdge(3, 6);
            data.SetAttachedEdge(6, 9);
            data.SetAttachedEdge(4, 7);
            data.SetEdge(7, 8, BraceletEdgeKind.Map);
            data.SetEdge(8, 9, BraceletEdgeKind.Map);
            return data;
        }

        private static BraceletUnlinkLevelData CreateFilledLevel(
            int[] rowLengths,
            BraceletRingKind kind,
            params int[] occupiedSlots)
        {
            var data = CreateEmpty(rowLengths);
            var fillEverySlot = occupiedSlots == null || occupiedSlots.Length == 0;
            var slots = fillEverySlot ? new int[data.SlotCount] : occupiedSlots;
            if (fillEverySlot)
            {
                for (var i = 0; i < slots.Length; i++)
                {
                    slots[i] = i;
                }
            }
            for (var i = 0; i < slots.Length; i++)
            {
                var angle = Mathf.Repeat(150f + slots[i] * 73f, 360f) - 180f;
                data.SetRing(slots[i], kind, angle, slots[i]);
            }
            return data;
        }

        private static int[] NormalizeRows(int[] rowLengths)
        {
            if (rowLengths == null || rowLengths.Length == 0)
            {
                return (int[])LegacyRows.Clone();
            }
            var result = new int[rowLengths.Length];
            for (var i = 0; i < rowLengths.Length; i++)
            {
                result[i] = Mathf.Clamp(rowLengths[i], 1, 6);
            }
            return result;
        }

        private static int CountSlots(int[] rows)
        {
            var count = 0;
            if (rows == null)
            {
                return count;
            }
            for (var i = 0; i < rows.Length; i++)
            {
                count += Mathf.Max(0, rows[i]);
            }
            return count;
        }
    }

    [Serializable]
    internal sealed class BraceletRingSlotData
    {
        public BraceletRingKind Kind;
        public float GapAngle;
        public int ColorIndex;
    }

    [Serializable]
    internal sealed class BraceletEdgeSlotData
    {
        public int SlotA;
        public int SlotB;
        public BraceletEdgeKind Kind;
    }

    internal enum BraceletRingKind
    {
        Empty,
        Open,
        Closed
    }

    internal enum BraceletEdgeKind
    {
        Empty,
        Map,
        AttachedToA,
        AttachedToB
    }
}
