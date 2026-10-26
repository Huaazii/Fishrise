using UnityEngine;
using UnityEngine.UI;

namespace Fishing.Game
{
    /// <summary>Colors the menu wordmark from snowy highlights to blue ice using its actual glyph height.</summary>
    [AddComponentMenu("")]
    public sealed class IceTitleGradient : BaseMeshEffect
    {
        /// <summary>Applies vertex colors before the standard outline and depth shadow expand the text mesh.</summary>
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || mesh.currentVertCount == 0) return;
            var vertex = new UIVertex(); float bottom = float.MaxValue, top = float.MinValue;
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                bottom = Mathf.Min(bottom, vertex.position.y); top = Mathf.Max(top, vertex.position.y);
            }
            var low = new Color(.40f, .79f, .92f); var high = new Color(.97f, .995f, 1);
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                var color = Color.Lerp(low, high, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(bottom, top, vertex.position.y)));
                color.a = vertex.color.a / 255f; vertex.color = color; mesh.SetUIVertex(vertex, i);
            }
        }
    }
}
