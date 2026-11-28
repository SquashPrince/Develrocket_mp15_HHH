using UnityEngine;
using UnityEngine.UI;

// 흰색 UI Image에 위아래 색을 입힌다. 하프톤과 문양은 별도 오브젝트에서 표시한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class CutsceneBackgroundGradient : BaseMeshEffect
{
    [SerializeField] private Color _topColor = Color.white;
    [SerializeField] private Color _bottomColor = Color.black;

    public override void ModifyMesh(VertexHelper vertices)
    {
        if(!IsActive() || vertices.currentVertCount == 0) return;

        Rect rect = graphic.rectTransform.rect;
        if(rect.height <= 0f) return;

        UIVertex vertex = default;
        for(int i = 0; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);
            float height = Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y);
            vertex.color = (Color)vertex.color * Color.Lerp(_bottomColor, _topColor, height);
            vertices.SetUIVertex(vertex, i);
        }
    }
}
