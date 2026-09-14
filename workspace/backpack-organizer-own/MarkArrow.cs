using UnityEngine;
using UnityEngine.UI;

namespace SephiriaBackpackOrganizer
{
    // Vector arrows avoid depending on emoji glyphs in the game's TMP font.
    internal sealed class MarkArrow : Graphic
    {
        internal bool Down;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float cx = r.center.x, cy = r.center.y, w = Mathf.Min(r.width, r.height) * 0.34f, h = r.height * 0.36f;
            float sign = Down ? -1 : 1;
            Add(vh, cx - w * 0.25f, cy - h, sign, cy);
            Add(vh, cx + w * 0.25f, cy - h, sign, cy);
            Add(vh, cx + w * 0.25f, cy + h * 0.15f, sign, cy);
            Add(vh, cx - w * 0.25f, cy + h * 0.15f, sign, cy);
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(0, 2, 3);
            Add(vh, cx - w, cy, sign, cy); Add(vh, cx + w, cy, sign, cy); Add(vh, cx, cy + h, sign, cy);
            vh.AddTriangle(4, 5, 6);
        }
        private void Add(VertexHelper vh, float x, float y, float sign, float centerY)
        {
            var v = UIVertex.simpleVert;
            v.position = new Vector3(x, centerY + (y - centerY) * sign); v.color = color;
            vh.AddVert(v);
        }
    }
}
