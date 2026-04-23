using NUnit.Framework;
using UnityEngine;

namespace Tests
{
    public sealed class Game2048ResourceSmokeTests
    {
        [Test]
        public void Game2048RuntimeResourcesExist()
        {
            Assert.IsNotNull(Resources.Load<TextAsset>("Text/2048.ui_texts.zh-CN"), "2048 text catalog should exist.");
        }
    }
}
