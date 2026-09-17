using UnityEngine;
using UnityEngine.UI;
namespace HuanYouYu.MiniGameHall
{
    public sealed class ArrowEscapeZoomIconGraphic : MaskableGraphic
    {
        public bool IsPlus { get; set; }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            var radius = Mathf.Min(rect.width, rect.height) * 0.26f;
            var center = new Vector2(rect.center.x - radius * 0.12f, rect.center.y + radius * 0.08f);
            var thickness = Mathf.Max(2.4f, radius * 0.18f);
            AddRing(vh, center, radius, thickness);

            var handleStart = center + new Vector2(radius * 0.58f, -radius * 0.58f);
            var handleEnd = center + new Vector2(radius * 1.22f, -radius * 1.22f);
            AddSegment(vh, handleStart, handleEnd, thickness);

            var markHalf = radius * 0.42f;
            AddSegment(vh, center + Vector2.left * markHalf, center + Vector2.right * markHalf, thickness);
            if (IsPlus)
            {
                AddSegment(vh, center + Vector2.down * markHalf, center + Vector2.up * markHalf, thickness);
            }
        }

        private void AddRing(VertexHelper vh, Vector2 center, float radius, float thickness)
        {
            const int SegmentCount = 28;
            var innerRadius = radius - thickness;
            for (var i = 0; i < SegmentCount; i++)
            {
                var a0 = Mathf.PI * 2f * i / SegmentCount;
                var a1 = Mathf.PI * 2f * (i + 1) / SegmentCount;
                var outer0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
                var outer1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
                var inner1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * innerRadius;
                var inner0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * innerRadius;
                AddQuad(vh, outer0, outer1, inner1, inner0);
            }
        }

        private void AddSegment(VertexHelper vh, Vector2 start, Vector2 end, float thickness)
        {
            var delta = end - start;
            if (delta.sqrMagnitude <= 0.001f)
            {
                return;
            }

            var normal = new Vector2(-delta.y, delta.x).normalized * (thickness * 0.5f);
            AddQuad(vh, start + normal, end + normal, end - normal, start - normal);
        }

        private void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            var startIndex = vh.currentVertCount;
            var vertexColor = color;
            vh.AddVert(a, vertexColor, Vector2.zero);
            vh.AddVert(b, vertexColor, Vector2.zero);
            vh.AddVert(c, vertexColor, Vector2.zero);
            vh.AddVert(d, vertexColor, Vector2.zero);
            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
        }
    }

}
