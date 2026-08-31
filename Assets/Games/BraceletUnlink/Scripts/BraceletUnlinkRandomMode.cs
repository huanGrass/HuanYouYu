using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    internal static class BraceletUnlinkRandomLevelGenerator
    {
        public static BraceletUnlinkLevelData Generate(int seed, int completedCount)
        {
            var random = new System.Random(seed);
            var source = BraceletUnlinkLevelData.LoadSavedFourthLevel();
            source.EnsureInitialGoldLoopGapClearance();
            var data = BraceletUnlinkLevelData.CreateEmpty(5, 4, 5, 4, 5);
            var placeOnRight = ((random.Next(2) + Mathf.Max(0, completedCount)) & 1) == 0;
            var mirror = random.Next(2) == 0;
            var targetRingCount = random.Next(14, 24);
            var extraRingCount = targetRingCount - 12;
            var slotMap = BuildSlotMap(source.RowLengths, data.RowLengths, placeOnRight, mirror);
            var extraGroups = BuildExtraGroups(placeOnRight, mirror, extraRingCount, slotMap, random);
            var extraClosedOwners = new List<int>();
            var closedSlots = new HashSet<int>();

            for (var sourceSlot = 0; sourceSlot < source.SlotCount; sourceSlot++)
            {
                var sourceRing = source.Rings[sourceSlot];
                if (sourceRing.Kind == BraceletRingKind.Empty)
                {
                    continue;
                }
                if (sourceRing.Kind == BraceletRingKind.Closed)
                {
                    closedSlots.Add(slotMap[sourceSlot]);
                }
            }
            for (var groupIndex = 0; groupIndex < extraGroups.Count; groupIndex++)
            {
                if (!extraGroups[groupIndex].ForceMap)
                {
                    extraClosedOwners.Add(extraGroups[groupIndex].Owner);
                }
            }
            Shuffle(extraClosedOwners, random);
            var targetClosedCount = ChooseClosedRingCount(closedSlots.Count + extraClosedOwners.Count, random);
            for (var i = 0; closedSlots.Count < targetClosedCount; i++)
            {
                closedSlots.Add(extraClosedOwners[i]);
            }

            for (var sourceSlot = 0; sourceSlot < source.SlotCount; sourceSlot++)
            {
                var sourceRing = source.Rings[sourceSlot];
                if (sourceRing.Kind == BraceletRingKind.Empty)
                {
                    continue;
                }
                var mappedSlot = slotMap[sourceSlot];
                data.SetRing(mappedSlot, sourceRing.Kind, 0f, random.Next(14));
            }
            for (var groupIndex = 0; groupIndex < extraGroups.Count; groupIndex++)
            {
                var group = extraGroups[groupIndex];
                data.SetRing(
                    group.Owner,
                    closedSlots.Contains(group.Owner) ? BraceletRingKind.Closed : BraceletRingKind.Open,
                    0f,
                    random.Next(14));
                for (var targetIndex = 0; targetIndex < group.Targets.Length; targetIndex++)
                {
                    var target = group.Targets[targetIndex];
                    if (data.Rings[target].Kind == BraceletRingKind.Empty)
                    {
                        data.SetRing(target, BraceletRingKind.Open, 0f, random.Next(14));
                    }
                }
            }

            for (var edgeIndex = 0; edgeIndex < source.Edges.Length; edgeIndex++)
            {
                var sourceEdge = source.Edges[edgeIndex];
                if (sourceEdge.Kind == BraceletEdgeKind.Empty)
                {
                    continue;
                }
                var first = slotMap[sourceEdge.SlotA];
                var second = slotMap[sourceEdge.SlotB];
                if (sourceEdge.Kind == BraceletEdgeKind.Map)
                {
                    data.SetEdge(first, second, BraceletEdgeKind.Map);
                }
                else
                {
                    var ownerIsA = sourceEdge.Kind == BraceletEdgeKind.AttachedToA;
                    data.SetAttachedEdge(ownerIsA ? first : second, ownerIsA ? second : first);
                }
            }

            for (var groupIndex = 0; groupIndex < extraGroups.Count; groupIndex++)
            {
                var group = extraGroups[groupIndex];
                var useMap = group.ForceMap
                    || !group.ForceAttached
                    && !closedSlots.Contains(group.Owner)
                    && random.NextDouble() < 0.45;
                if (useMap)
                {
                    for (var targetIndex = 0; targetIndex < group.Targets.Length; targetIndex++)
                    {
                        data.SetEdge(group.Owner, group.Targets[targetIndex], BraceletEdgeKind.Map);
                        SetGapNearConstraint(data, group.Targets[targetIndex], group.Owner, random);
                    }
                    SetGapNearConstraint(data, group.Owner, group.Targets[0], random);
                    continue;
                }
                for (var targetIndex = 0; targetIndex < group.Targets.Length; targetIndex++)
                {
                    var target = group.Targets[targetIndex];
                    data.SetAttachedEdge(group.Owner, target);
                    SetGapNearConstraint(data, target, group.Owner, random);
                }
            }

            for (var sourceSlot = 0; sourceSlot < source.SlotCount; sourceSlot++)
            {
                var sourceRing = source.Rings[sourceSlot];
                if (sourceRing.Kind == BraceletRingKind.Empty)
                {
                    continue;
                }
                var gapAngle = mirror ? NormalizeAngle(180f - sourceRing.GapAngle) : sourceRing.GapAngle;
                var mappedSlot = slotMap[sourceSlot];
                data.SetRing(mappedSlot, sourceRing.Kind, gapAngle, random.Next(14));
            }
            for (var groupIndex = 0; groupIndex < extraGroups.Count; groupIndex++)
            {
                var group = extraGroups[groupIndex];
                var edge = data.FindEdge(group.Owner, group.Targets[0]);
                if (edge.Kind == BraceletEdgeKind.Map)
                {
                    SetGapNearConstraint(data, group.Owner, group.Targets[0], random);
                }
                for (var targetIndex = 0; targetIndex < group.Targets.Length; targetIndex++)
                {
                    SetGapNearConstraint(data, group.Targets[targetIndex], group.Owner, random);
                }
            }
            data.EnsureInitialGoldLoopGapClearance();
            return data;
        }

        private static int[] BuildSlotMap(
            int[] sourceRows,
            int[] targetRows,
            bool placeOnRight,
            bool mirror)
        {
            var sourceSlotCount = 0;
            for (var row = 0; row < sourceRows.Length; row++)
            {
                sourceSlotCount += sourceRows[row];
            }
            var result = new int[sourceSlotCount];
            var sourceStart = 0;
            var targetStart = 0;
            for (var row = 0; row < sourceRows.Length; row++)
            {
                var offset = placeOnRight ? 1 : 0;
                for (var column = 0; column < sourceRows[row]; column++)
                {
                    var mappedColumn = mirror ? sourceRows[row] - 1 - column : column;
                    result[sourceStart + column] = targetStart + offset + mappedColumn;
                }
                sourceStart += sourceRows[row];
                targetStart += targetRows[row];
            }
            return result;
        }

        private static List<ExtraConstraintGroup> BuildExtraGroups(
            bool placeOnRight,
            bool mirror,
            int extraRingCount,
            int[] slotMap,
            System.Random random)
        {
            var result = new List<ExtraConstraintGroup>();
            var evenPairs = placeOnRight
                ? new[]
                {
                    new[] { 0, 1 }, new[] { 5, 9 }, new[] { 14, 18 },
                    new[] { 19, 20 }, new[] { 21, 22 }
                }
                : new[]
                {
                    new[] { 3, 4 }, new[] { 8, 13 }, new[] { 17, 22 },
                    new[] { 18, 19 }, new[] { 20, 21 }
                };

            if (extraRingCount == 11)
            {
                var isolated = placeOnRight ? 4 : 0;
                var coreSourceSlot = placeOnRight
                    ? (mirror ? 1 : 2)
                    : (mirror ? 2 : 1);
                var coreTarget = slotMap[coreSourceSlot];
                result.Add(new ExtraConstraintGroup(isolated, new[] { coreTarget }, true, false));
                AddPairGroups(result, evenPairs, evenPairs.Length, random);
                return result;
            }

            if (extraRingCount % 2 == 0)
            {
                AddPairGroups(result, evenPairs, extraRingCount / 2, random);
                return result;
            }

            var triangle = placeOnRight ? new[] { 0, 1, 5 } : new[] { 3, 4, 8 };
            Shuffle(triangle, random);
            result.Add(new ExtraConstraintGroup(triangle[0], new[] { triangle[1], triangle[2] }, false, true));
            var oddPairs = placeOnRight
                ? new[] { new[] { 9, 14 }, new[] { 18, 19 }, new[] { 20, 21 } }
                : new[] { new[] { 13, 17 }, new[] { 18, 19 }, new[] { 20, 21 } };
            AddPairGroups(result, oddPairs, (extraRingCount - 3) / 2, random);
            return result;
        }

        private static void AddPairGroups(
            ICollection<ExtraConstraintGroup> groups,
            int[][] pairs,
            int count,
            System.Random random)
        {
            Shuffle(pairs, random);
            for (var pairIndex = 0; pairIndex < count; pairIndex++)
            {
                var firstOwnsLoop = random.Next(2) == 0;
                groups.Add(new ExtraConstraintGroup(
                    firstOwnsLoop ? pairs[pairIndex][0] : pairs[pairIndex][1],
                    new[] { firstOwnsLoop ? pairs[pairIndex][1] : pairs[pairIndex][0] },
                    false,
                    false));
            }
        }

        private static int ChooseClosedRingCount(int maximum, System.Random random)
        {
            return random.Next(3, Mathf.Min(7, maximum) + 1);
        }

        private static void SetGapNearConstraint(
            BraceletUnlinkLevelData data,
            int slot,
            int neighbor,
            System.Random random)
        {
            var direction = data.GetSlotPosition(neighbor) - data.GetSlotPosition(slot);
            var releaseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            data.Rings[slot].GapAngle = NormalizeAngle(
                releaseAngle + (random.Next(2) == 0 ? -60f : 60f));
        }

        private static float NormalizeAngle(float angle)
        {
            return Mathf.Repeat(angle + 180f, 360f) - 180f;
        }

        private static void Shuffle<T>(IList<T> values, System.Random random)
        {
            for (var i = values.Count - 1; i > 0; i--)
            {
                var target = random.Next(i + 1);
                var value = values[i];
                values[i] = values[target];
                values[target] = value;
            }
        }

        private sealed class ExtraConstraintGroup
        {
            public ExtraConstraintGroup(int owner, int[] targets, bool forceAttached, bool forceMap)
            {
                Owner = owner;
                Targets = targets;
                ForceAttached = forceAttached;
                ForceMap = forceMap;
            }

            public int Owner { get; }
            public int[] Targets { get; }
            public bool ForceAttached { get; }
            public bool ForceMap { get; }
        }
    }

    public sealed partial class BraceletUnlinkGameView
    {
        private const string RandomModeSaveKey = "bracelet-unlink.random-mode.v1";
        private static string randomModeSaveKeyOverride;

        [Serializable]
        private sealed class RandomModeSaveData
        {
            public int ActiveSeed;
            public int CompletedCount;
        }

        private Button randomModeButton;
        private bool isRandomMode;
        private RandomModeSaveData randomModeSave;

        private void InitializeRandomMode(Transform actionBar)
        {
            randomModeSave = LoadRandomModeSave();
            randomModeButton = MiniGameShellBottomBarBuilder.CreateTextActionButton(
                actionBar,
                "BraceletRandomModeButton",
                UiTextCatalog.Get("bracelet-unlink.random.entry"),
                116f,
                72f,
                22f);
            randomModeButton.onClick.AddListener(OnRandomModeClicked);
            UpdateRandomModeButton();
        }

        private void UpdateRandomModeButton()
        {
            if (randomModeButton == null)
            {
                return;
            }
            randomModeButton.gameObject.SetActive(IsRandomModeUnlocked());
            MiniGameShellBottomBarBuilder.SetTextActionButtonSelected(randomModeButton, isRandomMode);
        }

        private void OnRandomModeClicked()
        {
            if (randomModeSave == null || !IsRandomModeUnlocked())
            {
                return;
            }
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            if (isRandomMode && !hasGameplayInteraction)
            {
                EnsureRandomSeed();
                randomModeSave.ActiveSeed = AdvanceSeed(randomModeSave.ActiveSeed);
                SaveRandomMode();
            }
            else if (isRandomMode)
            {
                ShowRestartConfirmation(StartRandomMode);
                return;
            }
            StartRandomMode();
        }

        private void StartRandomMode()
        {
            isRandomMode = true;
            CloseLevelSelectView();
            CloseRewardSettlementPanel();
            EnsureRandomSeed();
            levelData = BraceletUnlinkRandomLevelGenerator.Generate(
                randomModeSave.ActiveSeed,
                randomModeSave.CompletedCount);
            UpdateRandomModeButton();
            RebuildLevelObjects();
            ResetGame();
        }

        private void UnlockRandomModeIfCampaignFinished()
        {
            if (isRandomMode)
            {
                return;
            }
            EnsureLevelProgress();
            if (!levelProgress.AreAllLevelsCompleted)
            {
                return;
            }

            EnsureRandomSeed();
            SaveRandomMode();
            UpdateRandomModeButton();
        }

        private bool IsRandomModeUnlocked()
        {
            return levelProgress != null
                && levelProgress.UnlockedLevelCount >= levelProgress.LevelCount
                && levelProgress.AreAllLevelsCompleted;
        }

        private void CompleteRandomRound()
        {
            randomModeSave.CompletedCount += 1;
            randomModeSave.ActiveSeed = AdvanceSeed(randomModeSave.ActiveSeed);
            SaveRandomMode();
        }

        private void ExitRandomModeForCampaign()
        {
            isRandomMode = false;
            UpdateRandomModeButton();
        }

        private static int AdvanceSeed(int seed)
        {
            unchecked
            {
                var value = (uint)seed;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                return value == 0u ? 0x13572468 : (int)value;
            }
        }

        private void EnsureRandomSeed()
        {
            if (randomModeSave.ActiveSeed == 0)
            {
                randomModeSave.ActiveSeed = AdvanceSeed(Environment.TickCount ^ 0x4b1d2a39);
            }
        }

        private static RandomModeSaveData LoadRandomModeSave()
        {
            var key = GetRandomModeSaveKey();
            if (!PlayerPrefs.HasKey(key))
            {
                return new RandomModeSaveData();
            }
            var data = JsonUtility.FromJson<RandomModeSaveData>(PlayerPrefs.GetString(key, string.Empty));
            return data ?? new RandomModeSaveData();
        }

        private void SaveRandomMode()
        {
            PlayerPrefs.SetString(GetRandomModeSaveKey(), JsonUtility.ToJson(randomModeSave));
            PlayerPrefs.Save();
        }

        private static string GetRandomModeSaveKey()
        {
            return string.IsNullOrEmpty(randomModeSaveKeyOverride)
                ? RandomModeSaveKey
                : randomModeSaveKeyOverride;
        }
    }
}
