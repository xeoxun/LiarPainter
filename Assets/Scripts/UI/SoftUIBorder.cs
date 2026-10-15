using UnityEngine;
using UnityEngine.UI;

// A transparent-centred halo. Its colour and spread are authored in the prefab.
[AddComponentMenu("UI/Soft UI Border")]
public sealed class SoftUIBorder : MaskableGraphic
{
    [SerializeField, Min(1f)] private float spread = 12f;
    [SerializeField, Range(4, 24)] private int layers = 12;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        int count = Mathf.Clamp(layers, 4, 24);
        for (int i = 0; i <= count; i++)
        {
            float t = (float)i / count;
            float distance = t * Mathf.Max(1f, spread);
            Color c = color;
            c.a *= Mathf.Pow(1f - t, 2.5f);
            vh.AddVert(new Vector3(r.xMin-distance,r.yMin-distance),c,Vector2.zero);
            vh.AddVert(new Vector3(r.xMin-distance,r.yMax+distance),c,Vector2.zero);
            vh.AddVert(new Vector3(r.xMax+distance,r.yMax+distance),c,Vector2.zero);
            vh.AddVert(new Vector3(r.xMax+distance,r.yMin-distance),c,Vector2.zero);
            if (i == 0) continue;
            int previous = (i-1)*4, current = i*4;
            for (int edge=0;edge<4;edge++)
            {
                int next=(edge+1)%4;
                vh.AddTriangle(previous+edge,current+edge,current+next);
                vh.AddTriangle(previous+edge,current+next,previous+next);
            }
        }
    }
}
