using System.Collections.Generic;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 统一管理小游戏图标资源键、显示名与纹理缓存。
    /// </summary>
    public static class MiniGameIconCatalog
    {
        private const string SharedGameIconResourcePrefix = "GameIcons/";

        private static readonly string[] AllIconKeys =
        {
            "corn",
            "flower",
            "water_drop",
            "tomato",
            "bomb",
            "moon",
            "crystal_drop",
            "feather",
            "pumpkin",
            "strawberry",
            "cabbage",
            "leaf",
            "key",
            "shield",
            "scroll",
            "diamond",
            "carrot",
            "wheat",
            "mushroom",
            "apple",
            "potion",
            "star",
            "chest",
            "coin"
        };

        private static readonly string[] AllIconDisplayNames =
        {
            "玉米",
            "花朵",
            "水滴",
            "番茄",
            "炸弹",
            "月亮",
            "晶滴",
            "羽毛",
            "南瓜",
            "草莓",
            "卷心菜",
            "叶子",
            "钥匙",
            "盾牌",
            "卷轴",
            "钻石",
            "胡萝卜",
            "麦穗",
            "蘑菇",
            "苹果",
            "药水",
            "星星",
            "宝箱",
            "金币"
        };

        private static readonly string[] Match3IconKeys =
        {
            "strawberry",
            "apple",
            "carrot",
            "pumpkin",
            "eggplant",
            "watermelon"
        };

        private static readonly Dictionary<string, Texture2D> TextureCache = new Dictionary<string, Texture2D>();

        public static int ClassicLinkIconCount => AllIconKeys.Length;
        public static int Match3IconCount => Match3IconKeys.Length;

        /// <summary>
        /// 依据数值映射连连看图标纹理（循环取模）。
        /// </summary>
        public static Texture2D GetClassicLinkTexture(int value)
        {
            var key = AllIconKeys[Mathf.Abs(value - 1) % AllIconKeys.Length];
            return LoadTexture(key);
        }

        /// <summary>
        /// 依据数值映射三消图标纹理（循环取模）。
        /// </summary>
        public static Texture2D GetMatch3Texture(int value)
        {
            var key = Match3IconKeys[Mathf.Abs(value - 1) % Match3IconKeys.Length];
            return LoadTexture(key);
        }

        public static Texture2D GetTexture(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? null : LoadTexture(key.Trim());
        }

        /// <summary>
        /// 返回连连看图标对应的中文显示名（循环取模）。
        /// </summary>
        public static string GetClassicLinkDisplayName(int value)
        {
            return AllIconDisplayNames[Mathf.Abs(value - 1) % AllIconDisplayNames.Length];
        }

        /// <summary>
        /// 从 Resources 读取图标并做进程内缓存。
        /// </summary>
        private static Texture2D LoadTexture(string key)
        {
            return LoadTextureByPath(SharedGameIconResourcePrefix + key);
        }

        private static Texture2D LoadTextureByPath(string resourcePath)
        {
            Texture2D texture;
            if (TextureCache.TryGetValue(resourcePath, out texture))
            {
                return texture;
            }

            texture = Resources.Load<Texture2D>(resourcePath);
            TextureCache[resourcePath] = texture;
            return texture;
        }
    }
}
