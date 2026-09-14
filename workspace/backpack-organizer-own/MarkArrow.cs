using UnityEngine;
using UnityEngine.UI;

namespace SephiriaBackpackOrganizer
{
    // Vector arrows avoid depending on emoji glyphs in the game's TMP font.
    internal sealed class MarkArrow : Graphic
    {
        internal bool Down;
        internal bool Double;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float cx = r.center.x, cy = r.center.y, w = Mathf.Min(r.width, r.height) * 0.34f, h = r.height * 0.36f;
            if (Double)
            {
                float offset = Mathf.Min(r.width * 0.23f, r.height * 0.40f);
                w = Mathf.Min(w, offset * 0.8f);
                Draw(vh, cx - offset, cy, w, h);
                Draw(vh, cx + offset, cy, w, h);
            }
            else Draw(vh, cx, cy, w, h);
        }
        private void Draw(VertexHelper vh, float cx, float cy, float w, float h)
        {
            int start = vh.currentVertCount;
            float sign = Down ? -1 : 1;
            Add(vh, cx - w * 0.25f, cy - h, sign, cy);
            Add(vh, cx + w * 0.25f, cy - h, sign, cy);
            Add(vh, cx + w * 0.25f, cy + h * 0.15f, sign, cy);
            Add(vh, cx - w * 0.25f, cy + h * 0.15f, sign, cy);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
            Add(vh, cx - w, cy, sign, cy); Add(vh, cx + w, cy, sign, cy); Add(vh, cx, cy + h, sign, cy);
            vh.AddTriangle(start + 4, start + 5, start + 6);
        }
        private void Add(VertexHelper vh, float x, float y, float sign, float centerY)
        {
            var v = UIVertex.simpleVert;
            v.position = new Vector3(x, centerY + (y - centerY) * sign); v.color = color;
            vh.AddVert(v);
        }
    }
}
