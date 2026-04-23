using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public static class PopupPanelTopDecorUtility
    {
        private const float SourcePanelWidth = 576f;
        private const float SourceDecorWidth = 19f;
        private const float SourceDecorHeight = 22f;
        private const float SourceDecorCenterOffsetX = -77.5f;
        private const float SourceDecorTopOffsetY = -26f;

        public static void CreateMirroredTopDecor(Transform parent, Sprite sprite, float panelWidth)
        {
            if (parent == null || sprite == null)
            {
                return;
            }

            var scale = panelWidth / SourcePanelWidth;
            var size = new Vector2(SourceDecorWidth * scale, SourceDecorHeight * scale);
            var offsetX = SourceDecorCenterOffsetX * scale;
            var offsetY = SourceDecorTopOffsetY * scale;

            CreateTopDecorSide(parent, "TopDecorLeft", sprite, new Vector2(offsetX, offsetY), size, false);
            CreateTopDecorSide(parent, "TopDecorRight", sprite, new Vector2(-offsetX, offsetY), size, true);
        }

        private static void CreateTopDecorSide(
            Transform parent,
            string name,
            Sprite sprite,
            Vector2 anchoredPosition,
            Vector2 size,
            bool mirrored)
        {
            var decor = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement));
            decor.transform.SetParent(parent, false);
            if (mirrored)
            {
                decor.transform.localScale = new Vector3(-1f, 1f, 1f);
            }

            var rect = decor.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = decor.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = false;
            image.raycastTarget = false;
            decor.GetComponent<LayoutElement>().ignoreLayout = true;
        }
    }
}
