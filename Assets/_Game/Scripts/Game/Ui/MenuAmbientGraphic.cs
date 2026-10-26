using UnityEngine;
using UnityEngine.UI;

namespace Fishing.Game
{
    /// <summary>Low-contrast menu water glow, rising bubbles and sparse snow without colliders or input interception.</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class MenuAmbientGraphic : MaskableGraphic
    {
        internal float Clock;
        /// <summary>Draws decorative atmosphere from the menu clock; all placement is deterministic.</summary>
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var area = GetPixelAdjustedRect();
            if (area.width <= 0 || area.height <= 0) return;
            Ellipse(mesh, new Vector2(0, 20), new Vector2(270, 115) * (1 + .035f * Mathf.Sin(Clock * .65f)),
                new Color(.25f, .74f, .84f, .10f), true);
            for (int i = 0; i < 14; i++)
            {
                float x = area.xMin + Mathf.Repeat(i * 83.7f + Mathf.Sin(Clock * .2f + i) * 20, area.width);
                float y = area.yMax - Mathf.Repeat(i * 37.3f + Clock * (8 + i % 4), area.height * .25f);
                Ellipse(mesh, new Vector2(x, y), Vector2.one * (i % 3 == 0 ? 1.6f : .9f), new Color(.87f, .96f, 1, .27f), false);
            }
            for (int i = 0; i < 12; i++)
            {
                float x = Mathf.Sin(i * 2.17f) * area.width * .40f + Mathf.Sin(Clock * .4f + i) * 7;
                float y = -area.height * .14f + Mathf.Repeat(i * 37 + Clock * (10 + i % 4 * 3), area.height * .32f);
                float radius = 2 + i % 4;
                Ring(mesh, new Vector2(x, y), radius, new Color(.61f, .89f, .98f, .14f));
            }
        }
        private static void Ellipse(VertexHelper mesh, Vector2 centre, Vector2 radius, Color tint, bool soft)
        {
            int first = mesh.currentVertCount; mesh.AddVert(centre, tint, Vector2.zero);
            var edge = tint; if (soft) edge.a = 0;
            const int count = 24;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / count;
                mesh.AddVert(centre + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y), edge, Vector2.zero);
            }
            for (int i = 0; i < count; i++) mesh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % count);
        }
        private static void Ring(VertexHelper mesh, Vector2 centre, float radius, Color tint)
        {
            int first = mesh.currentVertCount; const int count = 12;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / count; var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(centre + direction * radius, tint, Vector2.zero);
                mesh.AddVert(centre + direction * (radius - .65f), tint, Vector2.zero);
            }
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                mesh.AddTriangle(first + i * 2, first + next * 2, first + i * 2 + 1);
                mesh.AddTriangle(first + next * 2, first + next * 2 + 1, first + i * 2 + 1);
            }
        }
    }
}
