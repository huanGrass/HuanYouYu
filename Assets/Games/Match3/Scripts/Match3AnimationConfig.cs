using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    [CreateAssetMenu(fileName = "Match3AnimationConfig", menuName = "HuanYouYu/Games/Match3/Match3 Animation Config")]
    /// <summary>
    /// 三消动画时长配置资源；支持通过统一倍率调快/调慢整套动画。
    /// </summary>
    public sealed class Match3AnimationConfig : ScriptableObject
    {
        public const string ResourcePath = "Match3AnimationConfig";

        [SerializeField]
        [Min(0.1f)]
        [Tooltip("1 为基准速度，数值越大越慢，数值越小越快。")]
        private float durationScale = 0.5f;

        [SerializeField]
        [Min(0.01f)]
        private float swapDuration = 0.40f;

        [SerializeField]
        [Min(0f)]
        private float invalidSwapHoldDuration = 0.20f;

        [SerializeField]
        [Min(0f)]
        private float swapSettleDuration = 0.15f;

        [SerializeField]
        [Min(0.01f)]
        private float clearDuration = 0.32f;

        [SerializeField]
        [Min(0f)]
        private float clearHoldDuration = 0.18f;

        [SerializeField]
        [Min(0.01f)]
        private float fallDuration = 0.42f;

        [SerializeField]
        [Min(0f)]
        private float fallSettleDuration = 0.12f;

        [SerializeField]
        [Min(0.01f)]
        private float shuffleFadeDuration = 0.40f;

        public float DurationScale => Mathf.Max(0.1f, durationScale);
        public float SwapDuration => swapDuration * DurationScale;
        public float InvalidSwapHoldDuration => invalidSwapHoldDuration * DurationScale;
        public float SwapSettleDuration => swapSettleDuration * DurationScale;
        public float ClearDuration => clearDuration * DurationScale;
        public float ClearHoldDuration => clearHoldDuration * DurationScale;
        public float FallDuration => fallDuration * DurationScale;
        public float FallSettleDuration => fallSettleDuration * DurationScale;
        public float ShuffleFadeDuration => shuffleFadeDuration * DurationScale;

        /// <summary>
        /// 从 Resources 加载配置，不存在时创建运行时默认实例。
        /// </summary>
        public static Match3AnimationConfig LoadOrCreate()
        {
            var config = Resources.Load<Match3AnimationConfig>(ResourcePath);
            if (config != null)
            {
                return config;
            }

            var fallback = CreateInstance<Match3AnimationConfig>();
            fallback.name = "Match3AnimationConfig (Runtime Default)";
            return fallback;
        }
    }
}

