using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    public enum MiniGameShellBottomMode
    {
        DefaultSlot,
        OwnedByContent
    }

    public readonly struct MiniGameShellLayout
    {
        public const float DefaultTopInset = 172f;
        public const float DefaultBottomInset = 156f;
        public const float ContentOwnedBottomInset = 24f;

        public static MiniGameShellLayout Default
        {
            get
            {
                return new MiniGameShellLayout(DefaultTopInset, DefaultBottomInset, MiniGameShellBottomMode.DefaultSlot);
            }
        }

        public MiniGameShellLayout(float topInset, float bottomInset, MiniGameShellBottomMode bottomMode)
        {
            TopInset = Mathf.Max(0f, topInset);
            BottomInset = Mathf.Max(0f, bottomInset);
            BottomMode = bottomMode;
        }

        public float TopInset { get; }

        public float BottomInset { get; }

        public MiniGameShellBottomMode BottomMode { get; }
    }
}
