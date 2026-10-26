using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Rounded result/catalog surfaces with a restrained rim, gradient and diffuse fish spotlight, using the default masked UI material.</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class ResultCardGraphic : UnityEngine.UI.MaskableGraphic
    {
        internal bool QuantityBadge;
        internal float SpotlightX;

        /// <summary>Rebuilds only when the UI geometry is dirty; no texture, material or per-frame allocation is required.</summary>
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            Rect area = GetPixelAdjustedRect();
            if (area.width <= 0 || area.height <= 0) return;
            float radius = QuantityBadge ? area.height * .5f : 22;
            var shadow = area; shadow.position += Vector2.down * 3;
            Rounded(mesh, shadow, radius, 0);
            Rounded(mesh, area, radius, 1);
            var face = new Rect(area.x + 1.5f, area.y + 1.5f, area.width - 3, area.height - 3);
            Rounded(mesh, face, radius - 1.5f, 2);
        }

        private void Rounded(UnityEngine.UI.VertexHelper mesh, Rect area, float radius, int layer)
        {
            radius = Mathf.Clamp(radius, 0, Mathf.Min(area.width, area.height) * .5f);
            int first = mesh.currentVertCount;
            Vector2 centre = area.center + (QuantityBadge ? Vector2.zero : new Vector2(area.width * SpotlightX,area.height * .16f));
            mesh.AddVert(centre, Shade(centre, area, layer) * color, Vector2.zero);
            const int steps = 8, perimeter = 4 * (steps + 1);
            for (int corner = 0; corner < 4; corner++)
            {
                float x = corner < 2 ? area.xMax - radius : area.xMin + radius;
                float y = corner == 1 || corner == 2 ? area.yMax - radius : area.yMin + radius;
                for (int i = 0; i <= steps; i++)
                {
                    float angle = (-90 + corner * 90 + i * 90f / steps) * Mathf.Deg2Rad;
                    Vector2 point = new Vector2(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius);
                    mesh.AddVert(point, Shade(point, area, layer) * color, Vector2.zero);
                }
            }
            for (int i = 0; i < perimeter; i++) mesh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % perimeter);
        }

        private Color Shade(Vector2 point, Rect area, int layer)
        {
            if (layer == 0) return new Color(.005f, .025f, .045f, .32f);
            float y = Mathf.InverseLerp(area.yMin, area.yMax, point.y);
            if (QuantityBadge)
                return layer == 1
                    ? Color.Lerp(new Color(.55f, .23f, .10f), new Color(1, .77f, .40f), y)
                    : Color.Lerp(new Color(.65f, .28f, .10f), new Color(.93f, .52f, .20f), y);
            if (layer == 1) return Color.Lerp(new Color(.10f, .31f, .44f), new Color(.43f, .72f, .84f), y);
            Color fill = Color.Lerp(new Color(.035f, .16f, .26f), new Color(.12f, .38f, .53f), y);
            float x = (point.x - area.center.x - area.width * SpotlightX) / (area.width * .5f);
            float dy = (point.y - area.center.y) / area.height - .16f;
            float glow = Mathf.Exp(-x * x * 2.4f - dy * dy * 12) * .7f;
            return Color.Lerp(fill, new Color(.24f, .63f, .79f), glow);
        }
    }
}
