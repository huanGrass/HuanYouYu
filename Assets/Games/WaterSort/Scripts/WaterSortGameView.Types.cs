using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed partial class WaterSortGameView
    {
        private sealed class WaterSortLevelDefinition
        {
            private readonly int[][] layout;

            public WaterSortLevelDefinition(int[][] layout, int colorCount, int emptyBottleCount)
            {
                this.layout = CopyLayout(layout);
                ColorCount = colorCount;
                EmptyBottleCount = emptyBottleCount;
            }

            public int ColorCount { get; }

            public int EmptyBottleCount { get; }

            public int BottleCount
            {
                get { return layout.Length; }
            }

            public int[][] CreateLayout()
            {
                return CopyLayout(layout);
            }

            private static int[][] CopyLayout(int[][] source)
            {
                if (source == null)
                {
                    return new int[0][];
                }

                var copy = new int[source.Length][];
                for (var i = 0; i < source.Length; i++)
                {
                    copy[i] = source[i] == null ? new int[0] : (int[])source[i].Clone();
                }

                return copy;
            }
        }

        private static WaterSortLevelDefinition[] LoadLevelDefinitions()
        {
            var asset = Resources.Load<TextAsset>(LevelResourcePath);
            if (asset == null)
            {
                throw new InvalidOperationException("未找到水排序关卡配置: Resources/" + LevelResourcePath);
            }

            WaterSortLevelCatalog catalog;
            try
            {
                catalog = JsonUtility.FromJson<WaterSortLevelCatalog>(asset.text);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("解析水排序关卡配置失败: " + exception.Message, exception);
            }

            if (catalog == null || catalog.levels == null || catalog.levels.Length == 0)
            {
                throw new InvalidOperationException("水排序关卡配置为空: Resources/" + LevelResourcePath);
            }

            var levels = new WaterSortLevelDefinition[catalog.levels.Length];
            for (var i = 0; i < catalog.levels.Length; i++)
            {
                levels[i] = ParseLevelDefinition(catalog.levels[i], i);
            }

            return levels;
        }

        private static WaterSortLevelDefinition ParseLevelDefinition(WaterSortLevelEntry entry, int levelIndex)
        {
            if (entry == null || entry.bottles == null || entry.bottles.Length < 3)
            {
                throw new InvalidOperationException("水排序关卡瓶子数量错误: " + (levelIndex + 1));
            }

            var layout = new int[entry.bottles.Length][];
            var colorCounts = new int[MaxWaterColorCount];
            var emptyBottleCount = 0;
            var maxColorIndex = -1;
            for (var bottleIndex = 0; bottleIndex < entry.bottles.Length; bottleIndex++)
            {
                var bottle = entry.bottles[bottleIndex];
                var layers = bottle == null || bottle.layers == null ? new int[0] : bottle.layers;
                if (layers.Length > BottleCapacity)
                {
                    throw new InvalidOperationException("水排序关卡瓶子容量错误: " + (levelIndex + 1) + "-" + (bottleIndex + 1));
                }

                if (layers.Length == 0)
                {
                    emptyBottleCount += 1;
                }

                layout[bottleIndex] = new int[layers.Length];
                for (var layerIndex = 0; layerIndex < layers.Length; layerIndex++)
                {
                    var colorIndex = layers[layerIndex];
                    if (colorIndex < 0 || colorIndex >= MaxWaterColorCount)
                    {
                        throw new InvalidOperationException("水排序关卡颜色值越界: " + (levelIndex + 1));
                    }

                    layout[bottleIndex][layerIndex] = colorIndex;
                    colorCounts[colorIndex] += 1;
                    maxColorIndex = Mathf.Max(maxColorIndex, colorIndex);
                }
            }

            var colorCount = maxColorIndex + 1;
            if (colorCount < 1)
            {
                throw new InvalidOperationException("水排序关卡颜色或空瓶数量错误: " + (levelIndex + 1));
            }

            for (var colorIndex = 0; colorIndex < colorCount; colorIndex++)
            {
                if (colorCounts[colorIndex] != BottleCapacity)
                {
                    throw new InvalidOperationException("水排序关卡每种颜色必须正好四层: " + (levelIndex + 1));
                }
            }

            for (var colorIndex = colorCount; colorIndex < colorCounts.Length; colorIndex++)
            {
                if (colorCounts[colorIndex] != 0)
                {
                    throw new InvalidOperationException("水排序关卡颜色编号必须连续: " + (levelIndex + 1));
                }
            }

            return new WaterSortLevelDefinition(layout, colorCount, emptyBottleCount);
        }

        [Serializable]
        private sealed class WaterSortLevelCatalog
        {
            public WaterSortLevelEntry[] levels;
        }

        [Serializable]
        private sealed class WaterSortLevelEntry
        {
            public WaterSortLevelBottle[] bottles;
        }

        [Serializable]
        private sealed class WaterSortLevelBottle
        {
            public int[] layers;
        }

        private sealed class BottleView
        {
            public RectTransform Root;
            public Button Button;
            public RoundedRectGraphic Background;
            public BottleShapeGraphic BottleShape;
            public RectTransform Cap;
            public Vector2 CapFinalPosition;
            public float CapAnimationElapsed;
            public bool IsCapVisible;
            public bool IsCapAnimating;
            public RectTransform LiquidMask;
            public RectTransform FillArea;
            public WaterLayerGraphic[] Segments;
            public bool IsLifted;
        }

        private sealed class PourMove
        {
            public int SourceIndex;
            public int TargetIndex;
            public int ColorIndex;
            public int Amount;
        }

        private sealed class PourAnimationState
        {
            public int SourceIndex;
            public int TargetIndex;
            public Coroutine Routine;
            public WaterStreamGraphic StreamGraphic;
            public Vector2 SourceStartPosition;
            public Quaternion SourceStartRotation;
            public Vector3 SourceStartScale;
            public float ReceiveSpeed;
            public bool HasSourceStartPose;
            public bool HasPendingReceive;
            public bool IsReceiving;
        }

        private sealed class BottleReceiveAnimationState
        {
            public float VisualFill;
            public float TargetFill;
            public float ActiveSpeed;
            public int PendingFlowCount;
        }
    }
}
