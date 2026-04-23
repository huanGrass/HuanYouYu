using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class GomokuCircleGraphic : MaskableGraphic
    {
        [SerializeField] private int segments = 28;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var rect = rectTransform.rect;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (radius <= 0.01f)
            {
                return;
            }

            var center = AddVertex(vh, Vector2.zero);
            var steps = Mathf.Max(12, segments);
            var outerIndices = new int[steps];

            for (var index = 0; index < steps; index++)
            {
                var angle = Mathf.PI * 2f * index / steps;
                var point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                outerIndices[index] = AddVertex(vh, point);
            }

            for (var index = 0; index < steps; index++)
            {
                var next = (index + 1) % steps;
                vh.AddTriangle(center, outerIndices[index], outerIndices[next]);
            }
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
