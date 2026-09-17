using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BubblePopGraphic : MaskableGraphic
    {
        private float depth;
        private float pulse;
        private static Vector4[] smallSurface;
        private static Vector4[] largeSurface;
        private static readonly Vector3 Light = new Vector3(-0.45f, 0.55f, 0.7f).normalized;

        public void SetSurface(Color tint, float depression, float impact)
        {
            if (color == tint && depth == depression && pulse == impact) return;
            color = tint;
            depth = depression;
            pulse = impact;
            SetVerticesDirty();
        }

        // Cache only the two fixed resting meshes, independent of size and tint.
        private static Vector4[] BuildRestSurface(int segments, int rings)
        {
            var vertices = new Vector4[(rings + 1) * (segments + 1)];
            for (var ring = 0; ring <= rings; ring++)
            {
                var r = ring / (float)rings;
                for (var step = 0; step <= segments; step++)
                {
                    var angle = step * Mathf.PI * 2f / segments;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Shade(direction * r, 0f, out var shade, out var shine);
                    vertices[ring * (segments + 1) + step] = new Vector4(direction.x * r, direction.y * r, shade, shine);
                }
            }
            return vertices;
        }

        private static void Shade(Vector2 point, float depression, out float shade, out float shine)
        {
            var r = point.magnitude;
            var normal = new Vector3(point.x * (1f - 1.85f * depression),
                point.y * (1f - 1.85f * depression), Mathf.Sqrt(Mathf.Max(0f, 1.05f - r * r))).normalized;
            var lighting = Mathf.Max(0f, Vector3.Dot(normal, Light));
            shade = 0.58f + lighting * 0.42f - depression * (1f - r) * 0.22f;
            shine = Mathf.Pow(lighting, 22f) * Mathf.Lerp(0.48f, 0.16f, depression);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            var small = Mathf.Min(rect.width, rect.height) <= 64f;
            var segments = small ? 24 : 48;
            var rings = small ? 6 : 12;
            var template = small
                ? smallSurface ?? (smallSurface = BuildRestSurface(24, 6))
                : largeSurface ?? (largeSurface = BuildRestSurface(48, 12));
            var radius = Mathf.Min(rect.width, rect.height) * 0.48f;
            mesh.AddVert(rect.center, new Color(0.24f, 0.22f, 0.34f, 0.3f), Vector2.zero);
            for (var step = 0; step <= segments; step++)
            {
                var edge = template[rings * (segments + 1) + step];
                mesh.AddVert(rect.center + new Vector2(edge.x, edge.y) * (radius / 0.96f),
                    new Color(0.24f, 0.22f, 0.34f, 0.3f), Vector2.zero);
                if (step > 0) mesh.AddTriangle(0, step, step + 1);
            }
            var surfaceStart = mesh.currentVertCount;
            for (var ring = 0; ring <= rings; ring++)
            {
                for (var step = 0; step <= segments; step++)
                {
                    var vertex = template[ring * (segments + 1) + step];
                    var point = new Vector2(vertex.x, vertex.y);
                    var shade = vertex.z;
                    var shine = vertex.w;
                    if (depth != 0f) Shade(point, depth, out shade, out shine);
                    var surface = Color.Lerp(new Color(color.r * shade, color.g * shade, color.b * shade, 1f), Color.white, shine);
                    if (ring == rings) surface = Color.Lerp(color, Color.white, 0.24f);
                    mesh.AddVert(rect.center + point * radius, surface, Vector2.zero);
                    if (ring == 0 || step == 0) continue;
                    var current = surfaceStart + ring * (segments + 1) + step;
                    mesh.AddTriangle(current, current - 1, current - segments - 2);
                    mesh.AddTriangle(current, current - segments - 2, current - segments - 1);
                }
            }
            if (pulse <= 0f) return;
            var outer = radius * (1f + 0.22f * (1f - pulse));
            var glow = new Color(color.r, color.g, color.b, pulse * 0.45f);
            var first = mesh.currentVertCount;
            for (var step = 0; step <= segments; step++)
            {
                var edge = template[rings * (segments + 1) + step];
                var direction = new Vector2(edge.x, edge.y);
                mesh.AddVert(rect.center + direction * outer, new Color(glow.r, glow.g, glow.b, 0f), Vector2.zero);
                mesh.AddVert(rect.center + direction * (outer - radius * 0.055f), glow, Vector2.zero);
                if (step == 0) continue;
                var current = first + step * 2;
                mesh.AddTriangle(current, current - 2, current - 1);
                mesh.AddTriangle(current, current - 1, current + 1);
            }
        }
    }
}
