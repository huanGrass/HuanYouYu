using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    [CreateAssetMenu(fileName = "HallAdConfig", menuName = "HuanYouYu/Hall/Ad Config")]
    public sealed class HallAdConfig : ScriptableObject
    {
        public const string ResourcePath = "HallAdConfig";

        [SerializeField]
        private string nativeTemplateAdUnitId = string.Empty;

        [SerializeField]
        private string interstitialAdUnitId = string.Empty;

        [SerializeField]
        private string rewardedVideoAdUnitId = string.Empty;

        public string NativeTemplateAdUnitId => Normalize(nativeTemplateAdUnitId);

        public string InterstitialAdUnitId => Normalize(interstitialAdUnitId);

        public string RewardedVideoAdUnitId => Normalize(rewardedVideoAdUnitId);

        public static HallAdConfig Load()
        {
            return Resources.Load<HallAdConfig>(ResourcePath);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
