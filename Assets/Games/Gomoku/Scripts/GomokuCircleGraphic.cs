using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class GomokuCircleGraphic : MaskableGraphic
    {
        [SerializeField] private int segments = 28;
        [SerializeField] private bool dashedOutline;
        [SerializeField] private float outlineThickness = 3f;

        public void SetDashedOutline(bool enabled)
        {
            dashedOutline = enabled;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var rect = rectTransform.rect;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (radius <= 0.01f)
            {
                return;
            }

            if (dashedOutline)
            {
                PopulateDashedOutline(vh, radius);
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

        private void PopulateDashedOutline(VertexHelper vh, float radius)
        {
            var steps = Mathf.Max(24, segments);
            var innerRadius = Mathf.Max(0f, radius - outlineThickness);
            for (var index = 0; index < steps; index += 2)
            {
                var next = (index + 1) % steps;
                var startAngle = Mathf.PI * 2f * index / steps;
                var endAngle = Mathf.PI * 2f * next / steps;
                var startDirection = new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle));
                var endDirection = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle));

                var outerStart = AddVertex(vh, startDirection * radius);
                var outerEnd = AddVertex(vh, endDirection * radius);
                var innerEnd = AddVertex(vh, endDirection * innerRadius);
                var innerStart = AddVertex(vh, startDirection * innerRadius);
                vh.AddTriangle(outerStart, outerEnd, innerEnd);
                vh.AddTriangle(outerStart, innerEnd, innerStart);
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
