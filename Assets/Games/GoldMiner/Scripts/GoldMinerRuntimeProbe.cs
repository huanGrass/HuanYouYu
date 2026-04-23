using System;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    [DisallowMultipleComponent]
    public sealed class GoldMinerRuntimeProbe : MonoBehaviour
    {
        private Func<string> hookStateProvider;
        private Func<int> scoreProvider;
        private Func<int> coinProvider;
        private Func<int> chestProvider;
        private Func<int> remainingProvider;
        private Func<bool> settledProvider;
        private Func<float, bool> launchProvider;
        private Func<float[]> angleProvider;
        private Func<float> swingAngleProvider;
        private Action clearBoardProvider;

        public string HookStateName
        {
            get { return hookStateProvider != null ? hookStateProvider() : string.Empty; }
        }

        public int Score
        {
            get { return scoreProvider != null ? scoreProvider() : 0; }
        }

        public int CoinCount
        {
            get { return coinProvider != null ? coinProvider() : 0; }
        }

        public int ChestCount
        {
            get { return chestProvider != null ? chestProvider() : 0; }
        }

        public int RemainingCount
        {
            get { return remainingProvider != null ? remainingProvider() : 0; }
        }

        public bool IsSettled
        {
            get { return settledProvider != null && settledProvider(); }
        }

        public float SwingAngle
        {
            get { return swingAngleProvider != null ? swingAngleProvider() : 0f; }
        }

        public void Bind(
            Func<string> hookStateGetter,
            Func<int> scoreGetter,
            Func<int> coinGetter,
            Func<int> chestGetter,
            Func<int> remainingGetter,
            Func<bool> settledGetter,
            Func<float, bool> launchAtAngle,
            Func<float[]> suggestedAnglesGetter,
            Func<float> swingAngleGetter,
            Action clearBoardAction)
        {
            hookStateProvider = hookStateGetter;
            scoreProvider = scoreGetter;
            coinProvider = coinGetter;
            chestProvider = chestGetter;
            remainingProvider = remainingGetter;
            settledProvider = settledGetter;
            launchProvider = launchAtAngle;
            angleProvider = suggestedAnglesGetter;
            swingAngleProvider = swingAngleGetter;
            clearBoardProvider = clearBoardAction;
        }

        public bool LaunchAtAngleForTest(float angleDegrees)
        {
            return launchProvider != null && launchProvider(angleDegrees);
        }

        public float[] GetSuggestedLaunchAnglesForTest()
        {
            return angleProvider != null ? angleProvider() : Array.Empty<float>();
        }

        public void ForceClearBoardForTest()
        {
            clearBoardProvider?.Invoke();
        }
    }
}
