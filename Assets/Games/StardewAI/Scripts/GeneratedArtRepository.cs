using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FarmPrototype
{
    internal static class GeneratedArtRepository
    {
        private static readonly Dictionary<FarmTileArt, string> TileResourcePaths = new Dictionary<FarmTileArt, string>
        {
            { FarmTileArt.GrassA, "Art/Generated/TerrainTiles/GrassA" },
            { FarmTileArt.GrassB, "Art/Generated/TerrainTiles/GrassB" },
            { FarmTileArt.GrassFlowers, "Art/Generated/TerrainTiles/GrassFlowers" },
            { FarmTileArt.Path, "Art/Generated/TerrainTiles/Path" },
            { FarmTileArt.Water, "Art/Generated/TerrainTiles/Water" },
            { FarmTileArt.FieldBase, "Art/Generated/TerrainTiles/FieldBase" },
            { FarmTileArt.SoilDry, "Art/Generated/TerrainTiles/SoilDry" },
            { FarmTileArt.SoilWet, "Art/Generated/TerrainTiles/SoilWet" },
            { FarmTileArt.Fence, "Art/Generated/TerrainTiles/Fence" },
            { FarmTileArt.FlowerOrange, "Art/Generated/TerrainTiles/FlowerOrange" },
            { FarmTileArt.FlowerYellow, "Art/Generated/TerrainTiles/FlowerYellow" },
            { FarmTileArt.CropSeed, "Art/Generated/TerrainTiles/CropSeed" },
            { FarmTileArt.CropSprout, "Art/Generated/TerrainTiles/CropSprout" },
            { FarmTileArt.CropLeafy, "Art/Generated/TerrainTiles/CropLeafy" },
            { FarmTileArt.CropRipe, "Art/Generated/TerrainTiles/CropRipe" }
        };

        private static readonly Dictionary<FarmSpriteArt, string> SpriteResourcePaths = new Dictionary<FarmSpriteArt, string>
        {
            { FarmSpriteArt.Shadow, "Art/Generated/EffectSprites/Shadow" },
            { FarmSpriteArt.TargetOutline, "Art/Generated/EffectSprites/TargetOutline" },
            { FarmSpriteArt.PlayerDown, "Art/Generated/CharacterSprites/PlayerDown" },
            { FarmSpriteArt.PlayerDownStepA, "Art/Generated/CharacterSprites/PlayerDownStepA" },
            { FarmSpriteArt.PlayerDownStepB, "Art/Generated/CharacterSprites/PlayerDownStepB" },
            { FarmSpriteArt.PlayerUp, "Art/Generated/CharacterSprites/PlayerUp" },
            { FarmSpriteArt.PlayerUpStepA, "Art/Generated/CharacterSprites/PlayerUpStepA" },
            { FarmSpriteArt.PlayerUpStepB, "Art/Generated/CharacterSprites/PlayerUpStepB" },
            { FarmSpriteArt.PlayerSide, "Art/Generated/CharacterSprites/PlayerSide" },
            { FarmSpriteArt.PlayerSideStepA, "Art/Generated/CharacterSprites/PlayerSideStepA" },
            { FarmSpriteArt.PlayerSideStepB, "Art/Generated/CharacterSprites/PlayerSideStepB" },
            { FarmSpriteArt.PlayerSideBody, "Art/Generated/CharacterSprites/PlayerSideBody" },
            { FarmSpriteArt.PlayerSideLeg, "Art/Generated/CharacterSprites/PlayerSideLeg" },
            { FarmSpriteArt.PlayerSideBackLeg, "Art/Generated/CharacterSprites/PlayerSideBackLeg" },
            { FarmSpriteArt.ToolHoe, "Art/Generated/ToolSprites/ToolHoe" },
            { FarmSpriteArt.ToolWateringCan, "Art/Generated/ToolSprites/ToolWateringCan" },
            { FarmSpriteArt.ToolSeedBag, "Art/Generated/ToolSprites/ToolSeedBag" },
            { FarmSpriteArt.ToolSickle, "Art/Generated/ToolSprites/ToolSickle" },
            { FarmSpriteArt.EffectHoeHit, "Art/Generated/EffectSprites/EffectHoeHit" },
            { FarmSpriteArt.EffectWaterHit, "Art/Generated/EffectSprites/EffectWaterHit" },
            { FarmSpriteArt.EffectSeedHit, "Art/Generated/EffectSprites/EffectSeedHit" },
            { FarmSpriteArt.EffectHarvestHit, "Art/Generated/EffectSprites/EffectHarvestHit" },
            { FarmSpriteArt.NpcLumi, "Art/Generated/CharacterSprites/NpcLumi" },
            { FarmSpriteArt.NpcXiaoTuanzi, "Art/Generated/CharacterSprites/NpcXiaoTuanzi" },
            { FarmSpriteArt.NpcQianran, "Art/Generated/CharacterSprites/NpcQianran" },
            { FarmSpriteArt.NpcHaiyinAwa, "Art/Generated/CharacterSprites/NpcHaiyinAwa" },
            { FarmSpriteArt.NpcAzhai, "Art/Generated/CharacterSprites/NpcAzhai" },
            { FarmSpriteArt.ShippingBin, "Art/Generated/PropSprites/ShippingBin" },
            { FarmSpriteArt.SeedChest, "Art/Generated/PropSprites/SeedChest" },
            { FarmSpriteArt.Cabin, "Art/Generated/PropSprites/Cabin" },
            { FarmSpriteArt.TreeTall, "Art/Generated/PropSprites/TreeTall" },
            { FarmSpriteArt.TreeRound, "Art/Generated/PropSprites/TreeRound" },
            { FarmSpriteArt.Bush, "Art/Generated/PropSprites/Bush" }
        };

        public static bool TryLoadTile(FarmTileArt art, out TileBase tile)
        {
            tile = null;
            if (!TileResourcePaths.TryGetValue(art, out string resourcePath))
            {
                return false;
            }

            if (!TryLoadSpriteFromResources(resourcePath, out Sprite sprite))
            {
                return false;
            }

            Tile runtimeTile = ScriptableObject.CreateInstance<Tile>();
            runtimeTile.sprite = sprite;
            runtimeTile.colliderType = Tile.ColliderType.None;
            runtimeTile.hideFlags = HideFlags.HideAndDontSave;
            tile = runtimeTile;
            return true;
        }

        public static bool TryLoadSprite(FarmSpriteArt art, out Sprite sprite)
        {
            sprite = null;
            if (!SpriteResourcePaths.TryGetValue(art, out string resourcePath))
            {
                return false;
            }

            return TryLoadSpriteFromResources(resourcePath, out sprite);
        }

        private static bool TryLoadSpriteFromResources(string resourcePath, out Sprite sprite)
        {
            sprite = Resources.Load<Sprite>(resourcePath);
            return sprite != null;
        }
    }
}
