using NUnit.Framework;
using UnityEngine;

namespace Tests
{
    public class NeedleHitResourceSmokeTests
    {
        [Test]
        public void NeedleHitRuntimeResourcesExist()
        {
            Assert.IsNotNull(Resources.Load<GameObject>("NeedleHitContent"), "NeedleHitContent prefab should exist.");
            Assert.IsNotNull(Resources.Load<GameObject>("NeedleHitNeedle"), "NeedleHitNeedle prefab should exist.");
            Assert.IsNotNull(Resources.Load<TextAsset>("Text/needlehit.ui_texts.zh-CN"), "NeedleHit text catalog should exist.");
        }
    }
}
