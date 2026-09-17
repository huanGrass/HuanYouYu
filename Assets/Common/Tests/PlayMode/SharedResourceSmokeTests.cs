using NUnit.Framework;
using System.Linq;
using UnityEngine;

namespace Tests
{
    public class SharedResourceSmokeTests
    {
        [Test]
        public void SharedSoundEffectsHaveCleanBoundariesAndHeadroom()
        {
            var build = typeof(HuanYouYu.MiniGameHall.MiniGameSfxPlayer).GetMethod(
                "BuildClip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                null, new[] { typeof(HuanYouYu.MiniGameHall.MiniGameSfxType) }, null);
            foreach (HuanYouYu.MiniGameHall.MiniGameSfxType type in System.Enum.GetValues(typeof(HuanYouYu.MiniGameHall.MiniGameSfxType)))
            {
                var clip = (AudioClip)build.Invoke(null, new object[] { type });
                try
                {
                    Assert.AreEqual(44100, clip.frequency, type.ToString());
                    Assert.AreEqual(1, clip.channels, type.ToString());
                    Assert.That(clip.length, Is.InRange(0.05f, 0.7f), type.ToString());
                    var samples = new float[clip.samples];
                    Assert.IsTrue(clip.GetData(samples, 0));
                    Assert.Less(Mathf.Abs(samples[0]), 0.001f, type + " onset");
                    Assert.Less(Mathf.Abs(samples[samples.Length - 1]), 0.001f, type + " tail");
                    float peak = 0f;
                    float energy = 0f;
                    foreach (var sample in samples)
                    {
                        Assert.IsFalse(float.IsNaN(sample) || float.IsInfinity(sample), type.ToString());
                        peak = Mathf.Max(peak, Mathf.Abs(sample));
                        energy += sample * sample;
                    }
                    Assert.That(peak, Is.InRange(0.04f, 0.45f), type + " mix headroom");
                    Assert.Greater(Mathf.Sqrt(energy / samples.Length), 0.005f, type + " audible signal");
                }
                finally
                {
                    Object.DestroyImmediate(clip);
                }
            }
        }

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
