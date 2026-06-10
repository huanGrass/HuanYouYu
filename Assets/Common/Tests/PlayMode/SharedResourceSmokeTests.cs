using NUnit.Framework;
using System.Linq;
using UnityEngine;

namespace Tests
{
    public class SharedResourceSmokeTests
    {
        [Test]
        public void SharedRuntimePrefabsAndThemeResourcesExist()
        {
            Assert.IsNotNull(Resources.Load<GameObject>("MiniGameShell"), "MiniGameShell prefab should exist.");
            Assert.IsNotNull(Resources.Load<GameObject>("MiniGamePausePopup"), "MiniGamePausePopup prefab should exist.");
            Assert.IsNotNull(Resources.Load<GameObject>("MiniGamePopup"), "MiniGamePopup prefab should exist.");
            Assert.IsNotNull(Resources.Load<Sprite>("HallTheme/hall_bg"), "Hall background sprite should exist.");
            Assert.IsNotNull(Resources.Load<Sprite>("HallTheme/shuffle_button"), "Shuffle sprite should exist.");
            Assert.IsNotNull(Resources.Load<Sprite>("HallTheme/hint_button"), "Hint sprite should exist.");
            Assert.IsNotNull(Resources.Load<Sprite>("HallTheme/pause_button"), "Pause sprite should exist.");
            Assert.IsNotNull(Resources.Load<Texture2D>("GameIcons/game_logo"), "Game logo icon should exist.");
            var textAssets = Resources.LoadAll<TextAsset>("Text");
            Assert.IsTrue(textAssets.Any(asset => asset != null && asset.name == "ui_texts.shared.zh-CN"), "Shared text catalog should exist.");
            Assert.IsTrue(textAssets.Any(asset => asset != null && asset.name == "hall.ui_texts.zh-CN"), "Hall text catalog should exist.");
            Assert.IsTrue(textAssets.Any(asset => asset != null && asset.name.EndsWith(".ui_texts.zh-CN")), "Per-game text catalogs should exist.");
        }
    }
}
