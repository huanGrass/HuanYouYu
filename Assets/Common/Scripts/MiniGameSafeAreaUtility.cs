using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    internal static class MiniGameSafeAreaUtility
    {
        internal static float GetTopInset(RectTransform reference)
        {
            if (reference == null || Screen.height <= 0)
            {
                return 0f;
            }

            var safeArea = Screen.safeArea;
            var topInsetPixels = Mathf.Max(0f, Screen.height - safeArea.yMax);
            if (topInsetPixels <= 0f)
            {
                return 0f;
            }

            var referenceHeight = reference.rect.height;
            if (referenceHeight <= 0f)
            {
                return topInsetPixels;
            }

            return topInsetPixels * referenceHeight / Screen.height;
        }
    }
}
