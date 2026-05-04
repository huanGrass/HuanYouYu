using System.Collections.Generic;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    internal static class MiniGameHallIconCatalog
    {
        private const string HallCardIconResourcePrefix = "HallCardIcons/";
        private const string DefaultHallIconKey = "hall_default";

        private static readonly Dictionary<string, string> HallIconKeysByGameId = new Dictionary<string, string>
        {
            { "classic-link", "classic_link" },
            { "control-point", "control-point" },
            { "game2048", "game2048" },
            { "tetris", "tetris" },
            { "arrow-escape", "arrow-escape" },
            { "breakout", "breakout" },
            { "blockpuzzle", "blockpuzzle" },
            { "bulls-cows", "bulls-cows" },
            { "goldminer", "goldminer" },
            { "gomoku", "gomoku" },
            { "minesweeper", "minesweeper" },
            { "needlehit", "needlehit" },
            { "reversi", "reversi" },
            { "nonogram", "nonogram" },
            { "match-3", "match_3" },
            { "sudoku", "Sudoku" },
            { "stardewai", "stardewai" },
            { "snake", "snake" },
            { "water-sort", "water-sort" },
            { "watermelon-merge", "watermelon-merge" },
            { "memory-flip", "memory-flip" },
            { "akari", "akari" },
            { "jumpjump", "jumpjump" },
            { "whacamole", "whacamole" },
            { "lightsout", "lightsout" },
            { "rivercrossing", "rivercrossing" },
            { "slidingpuzzle", "slidingpuzzle" },
            { "towerofhanoi", "towerofhanoi" },
            { "waterpouring", "waterpouring" },
            { "stack-match", "stack-match" }
        };

        private static readonly Dictionary<string, Texture2D> TextureCache = new Dictionary<string, Texture2D>();

        public static Texture2D GetTexture(string gameId)
        {
            var iconKey = ResolveIconKey(gameId);
            var hallTexture = LoadTextureByPath(HallCardIconResourcePrefix + iconKey);
            if (hallTexture != null)
            {
                return hallTexture;
            }

            var fallbackHallTexture = LoadTextureByPath(HallCardIconResourcePrefix + DefaultHallIconKey);
            if (fallbackHallTexture != null)
            {
                return fallbackHallTexture;
            }

            return MiniGameIconCatalog.GetTexture(iconKey);
        }

        private static string ResolveIconKey(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                return DefaultHallIconKey;
            }

            string iconKey;
            return HallIconKeysByGameId.TryGetValue(gameId.Trim(), out iconKey)
                ? iconKey
                : DefaultHallIconKey;
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
