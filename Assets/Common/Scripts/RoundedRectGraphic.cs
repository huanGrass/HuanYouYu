using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 基于 UGUI 的运行时圆角矩形图形组件。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoundedRectGraphic : MaskableGraphic
    {
        [SerializeField] private float cornerRadius = 24f;
        [SerializeField] private int cornerSegments = 6;

        public float CornerRadius
        {
            get { return cornerRadius; }
            set
            {
                cornerRadius = Mathf.Max(0f, value);
                SetVerticesDirty();
            }
        }

        /// <summary>
        /// 生成圆角矩形网格；半径为 0 时退化为普通矩形。
        /// </summary>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var rect = rectTransform.rect;
            var radius = Mathf.Min(cornerRadius, rect.width * 0.5f, rect.height * 0.5f);
            if (radius <= 0.01f)
            {
                AddQuad(vh, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMax));
                return;
            }

            var centerIndex = AddVertex(vh, rect.center);
            var stepsPerCorner = Mathf.Max(1, cornerSegments);
            var contour = new System.Collections.Generic.List<Vector2>((stepsPerCorner + 1) * 4);

            AppendCorner(contour, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f, stepsPerCorner, true);
            AppendCorner(contour, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f, stepsPerCorner, false);
            AppendCorner(contour, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f, stepsPerCorner, false);
            AppendCorner(contour, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f, 360f, stepsPerCorner, false);

            var contourIndices = new int[contour.Count];
            for (var i = 0; i < contour.Count; i++)
            {
                contourIndices[i] = AddVertex(vh, contour[i]);
            }

            for (var i = 0; i < contourIndices.Length; i++)
            {
                var nextIndex = (i + 1) % contourIndices.Length;
                vh.AddTriangle(centerIndex, contourIndices[i], contourIndices[nextIndex]);
            }
        }

        private static void AppendCorner(
            System.Collections.Generic.ICollection<Vector2> contour,
            Vector2 cornerCenter,
            float radius,
            float startDegrees,
            float endDegrees,
            int steps,
            bool includeStart)
        {
            if (contour == null)
            {
                return;
            }

            var startStep = includeStart ? 0 : 1;
            for (var step = startStep; step <= steps; step++)
            {
                var angle = Mathf.Lerp(startDegrees, endDegrees, step / (float)steps) * Mathf.Deg2Rad;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                contour.Add(cornerCenter + offset);
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

        private void AddQuad(VertexHelper vh, Vector2 min, Vector2 max)
        {
            var bottomLeft = AddVertex(vh, new Vector2(min.x, min.y));
            var topLeft = AddVertex(vh, new Vector2(min.x, max.y));
            var topRight = AddVertex(vh, new Vector2(max.x, max.y));
            var bottomRight = AddVertex(vh, new Vector2(max.x, min.y));

            vh.AddTriangle(bottomLeft, topLeft, topRight);
            vh.AddTriangle(bottomLeft, topRight, bottomRight);
        }
    }
}

