using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmPrototype
{
    internal enum FarmTileArt
    {
        GrassA,
        GrassB,
        GrassFlowers,
        Path,
        Water,
        FieldBase,
        SoilDry,
        SoilWet,
        Fence,
        FlowerOrange,
        FlowerYellow,
        CropSeed,
        CropSprout,
        CropLeafy,
        CropRipe
    }

    internal enum FarmSpriteArt
    {
        Shadow,
        TargetOutline,
        PlayerDown,
        PlayerDownStepA,
        PlayerDownStepB,
        PlayerUp,
        PlayerUpStepA,
        PlayerUpStepB,
        PlayerSide,
        PlayerSideStepA,
        PlayerSideStepB,
        PlayerSideBody,
        PlayerSideLeg,
        PlayerSideBackLeg,
        ToolHoe,
        ToolWateringCan,
        ToolSeedBag,
        ToolSickle,
        EffectHoeHit,
        EffectWaterHit,
        EffectSeedHit,
        EffectHarvestHit,
        NpcLumi,
        NpcXiaoTuanzi,
        NpcQianran,
        NpcHaiyinAwa,
        NpcAzhai,
        ShippingBin,
        SeedChest,
        Cabin,
        TreeTall,
        TreeRound,
        Bush
    }

    internal static class FarmPixelArtFactory
    {
        private static readonly Dictionary<FarmTileArt, TileBase> TileCache = new Dictionary<FarmTileArt, TileBase>();
        private static readonly Dictionary<FarmSpriteArt, Sprite> SpriteCache = new Dictionary<FarmSpriteArt, Sprite>();

        public static TileBase GetTile(FarmTileArt art)
        {
            if (TileCache.TryGetValue(art, out TileBase tile))
            {
                return tile;
            }

            if (!GeneratedArtRepository.TryLoadTile(art, out tile))
            {
                throw new InvalidOperationException("未找到地块贴图资源: " + art);
            }

            TileCache[art] = tile;
            return tile;
        }

        public static Sprite GetSprite(FarmSpriteArt art)
        {
            if (SpriteCache.TryGetValue(art, out Sprite sprite))
            {
                return sprite;
            }

            if (!GeneratedArtRepository.TryLoadSprite(art, out sprite))
            {
                throw new InvalidOperationException("未找到精灵贴图资源: " + art);
            }

            SpriteCache[art] = sprite;
            return sprite;
        }
    }
}
