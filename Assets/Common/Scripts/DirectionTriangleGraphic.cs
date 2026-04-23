using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 用于方向按钮的简易三角形图形组件。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DirectionTriangleGraphic : MaskableGraphic
    {
        [SerializeField] private float inset = 4f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var rect = rectTransform.rect;
            var minX = rect.xMin + inset;
            var maxX = rect.xMax - inset;
            var minY = rect.yMin + inset;
            var maxY = rect.yMax - inset;
            var midX = (minX + maxX) * 0.5f;

            var top = AddVertex(vh, new Vector2(midX, maxY));
            var left = AddVertex(vh, new Vector2(minX, minY));
            var right = AddVertex(vh, new Vector2(maxX, minY));
            vh.AddTriangle(top, left, right);
        }

        private int AddVertex(VertexHelper vh, Vector2 position)
        {
            var vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = position;
            vh.AddVert(vertex);
            return vh.currentVertCount - 1;
        }
    }
}
