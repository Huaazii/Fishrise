using UnityEngine;
using UnityEngine.UI;

namespace Fishing.Game
{
    /// <summary>Shared beveled ice button faces using the default UI material.</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class IceButtonGraphic : MaskableGraphic
    {
        internal bool Primary, BaseOnly;
        private float _glint = -1;
        internal Color FaceColor => Primary ? new Color(.08f, .34f, .46f) : new Color(.045f, .225f, .34f);
        internal float Glint
        {
            set { if (Mathf.Abs(_glint - value) < .001f) return; _glint = value; SetVerticesDirty(); }
        }
        /// <summary>Builds a dark lower block, bright bevel and clean gradient face.</summary>
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var area = GetPixelAdjustedRect(); if (area.width <= 0 || area.height <= 0) return;
            if (BaseOnly)
            {
                var shadow = area; shadow.position += Vector2.down * 3;
                Rounded(mesh, shadow, 17, new Color(.003f, .025f, .045f, .4f), new Color(.003f, .025f, .045f, .4f));
                Rounded(mesh, area, 17, new Color(.008f, .07f, .12f), new Color(.04f, .20f, .29f)); return;
            }
            Rounded(mesh, area, 17, new Color(.09f, .34f, .46f), new Color(.68f, .90f, .96f));
            var face = new Rect(area.x + 2, area.y + 2, area.width - 4, area.height - 4);
            var low = FaceColor * .57f; low.a = 1;
            var high = FaceColor * 1.42f; high.a = 1;
            Rounded(mesh, face, 15, low, high);
            Segment(mesh, new Vector2(area.xMin + 24, area.yMax - 4), new Vector2(area.xMax - 24, area.yMax - 4), .9f, Tone(new Color(.82f, .97f, 1, .5f)));
            if (Primary && _glint >= 0 && _glint <= 1)
            {
                float top = area.yMax - 12, bottom = area.yMin + 12;
                float x = Mathf.Lerp(area.xMin + 30, area.xMax - 30, _glint);
                Segment(mesh, new Vector2(x - 15, bottom), new Vector2(x + 15, top), 12,
                    Tone(new Color(.7f, .95f, 1, Mathf.Sin(_glint * Mathf.PI) * .09f)));
            }
        }
        private Color Tone(Color desired)
        {
            if (BaseOnly) return desired * color;
            var basis = FaceColor;
            // Graphic.color describes the actual centre tone, so Selectable tinting and contrast checks remain meaningful.
            return new Color(desired.r * color.r / basis.r, desired.g * color.g / basis.g,
                desired.b * color.b / basis.b, desired.a * color.a);
        }
        private void Rounded(VertexHelper mesh, Rect area, float radius, Color low, Color high)
        {
            radius = Mathf.Min(radius, area.height * .5f); int first = mesh.currentVertCount;
            Color Shade(Vector2 point) => Tone(Color.Lerp(low, high, Mathf.InverseLerp(area.yMin, area.yMax, point.y)));
            mesh.AddVert(area.center, Shade(area.center), Vector2.zero);
            const int steps = 8, count = 4 * (steps + 1);
            for (int corner = 0; corner < 4; corner++)
            {
                float x = corner < 2 ? area.xMax - radius : area.xMin + radius;
                float y = corner == 1 || corner == 2 ? area.yMax - radius : area.yMin + radius;
                for (int i = 0; i <= steps; i++)
                {
                    float angle = (-90 + corner * 90 + i * 90f / steps) * Mathf.Deg2Rad;
                    var point = new Vector2(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius);
                    mesh.AddVert(point, Shade(point), Vector2.zero);
                }
            }
            for (int i = 0; i < count; i++) mesh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % count);
        }
        private static void Segment(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            var direction = (b - a).normalized; var side = new Vector2(-direction.y, direction.x) * width * .5f;
            int first = mesh.currentVertCount;
            mesh.AddVert(a - side, tint, Vector2.zero); mesh.AddVert(a + side, tint, Vector2.zero);
            mesh.AddVert(b + side, tint, Vector2.zero); mesh.AddVert(b - side, tint, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
