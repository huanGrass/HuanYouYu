using TMPro;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    public static class MiniGameFontProvider
    {
        private const string DefaultFontResourcePath = "Fonts & Materials/NotoSansCJKsc-Subset SDF";

        private static TMP_FontAsset defaultFont;

        public static TMP_FontAsset DefaultFont
        {
            get
            {
                if (defaultFont == null)
                {
                    defaultFont = Resources.Load<TMP_FontAsset>(DefaultFontResourcePath);
                }

                return defaultFont != null ? defaultFont : TMP_Settings.defaultFontAsset;
            }
        }
    }
}
