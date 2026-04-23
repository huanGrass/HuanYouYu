using System;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    public sealed partial class GameControlPointView
    {
        private static ControlPointLevelDefinition[] LoadLevelDefinitions()
        {
            var asset = Resources.Load<TextAsset>(LevelResourcePath);
            if (asset == null)
            {
                throw new InvalidOperationException("未找到占点攻防关卡配置: Resources/" + LevelResourcePath);
            }

            ControlPointLevelCatalog catalog;
            try
            {
                catalog = JsonUtility.FromJson<ControlPointLevelCatalog>(asset.text);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("解析占点攻防关卡配置失败: " + exception.Message, exception);
            }

            if (catalog == null || catalog.levels == null || catalog.levels.Length == 0)
            {
                throw new InvalidOperationException("占点攻防关卡配置为空: Resources/" + LevelResourcePath);
            }

            var levels = new ControlPointLevelDefinition[catalog.levels.Length];
            for (var i = 0; i < catalog.levels.Length; i++)
            {
                levels[i] = ParseLevelDefinition(catalog.levels[i], i);
            }

            return levels;
        }

        private static ControlPointLevelDefinition ParseLevelDefinition(ControlPointLevelEntry entry, int levelIndex)
        {
            if (entry == null || entry.points == null || entry.points.Length < MinPointCount || entry.points.Length > MaxPointCount)
            {
                throw new InvalidOperationException("占点攻防关卡据点数量错误: " + (levelIndex + 1));
            }

            var pointSetups = new ControlPointPointSetup[entry.points.Length];
            var positions = new Vector2[entry.points.Length];
            var hasPlayer = false;
            var hasEnemy = false;

            for (var pointIndex = 0; pointIndex < entry.points.Length; pointIndex++)
            {
                var point = entry.points[pointIndex];
                if (point == null)
                {
                    throw new InvalidOperationException("占点攻防关卡据点配置为空: " + (levelIndex + 1) + "-" + (pointIndex + 1));
                }

                var owner = ParseOwner(point.owner, levelIndex, pointIndex);
                var units = point.units;
                if (units < 1)
                {
                    throw new InvalidOperationException("占点攻防关卡据点兵力错误: " + (levelIndex + 1) + "-" + (pointIndex + 1));
                }

                var position = new Vector2(point.x, point.y);
                if (position.x < MinPointX || position.x > MaxPointX || position.y < MinPointY || position.y > MaxPointY)
                {
                    throw new InvalidOperationException("占点攻防关卡据点坐标越界: " + (levelIndex + 1) + "-" + (pointIndex + 1));
                }

                for (var previousIndex = 0; previousIndex < pointIndex; previousIndex++)
                {
                    if (Vector2.Distance(position, positions[previousIndex]) < MinPointDistance)
                    {
                        throw new InvalidOperationException("占点攻防关卡据点距离过近: " + (levelIndex + 1) + "-" + (pointIndex + 1));
                    }
                }

                hasPlayer |= owner == ControlPointOwner.Player;
                hasEnemy |= IsEnemyOwner(owner);
                pointSetups[pointIndex] = new ControlPointPointSetup(owner, units);
                positions[pointIndex] = position;
            }

            if (!hasPlayer || !hasEnemy)
            {
                throw new InvalidOperationException("占点攻防关卡必须包含玩家和敌方据点: " + (levelIndex + 1));
            }

            return new ControlPointLevelDefinition(pointSetups, positions);
        }

        private static ControlPointOwner ParseOwner(string owner, int levelIndex, int pointIndex)
        {
            switch (owner)
            {
                case "Neutral":
                    return ControlPointOwner.Neutral;
                case "Player":
                    return ControlPointOwner.Player;
                case "Enemy":
                    return ControlPointOwner.Enemy;
                case "EnemyTwo":
                    return ControlPointOwner.EnemyTwo;
                case "EnemyThree":
                    return ControlPointOwner.EnemyThree;
                default:
                    throw new InvalidOperationException("占点攻防关卡据点归属错误: " + (levelIndex + 1) + "-" + (pointIndex + 1));
            }
        }

        [Serializable]
        private sealed class ControlPointLevelCatalog
        {
            public ControlPointLevelEntry[] levels;
        }

        [Serializable]
        private sealed class ControlPointLevelEntry
        {
            public ControlPointLevelPoint[] points;
        }

        [Serializable]
        private sealed class ControlPointLevelPoint
        {
            public string owner;
            public int units;
            public float x;
            public float y;
        }
    }
}
