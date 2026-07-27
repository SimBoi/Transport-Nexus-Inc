using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class TileSelectionUI : Graphic
{
    private readonly Vector2[] canvasCorners = new Vector2[4];
    private readonly Vector3[] worldCorners = new Vector3[4];
    [SerializeField] private Camera cam;

    public void UpdateQuad(Vector2 point1, Vector2 point2, Color color, float extend = 0)
    {
        // world space
        float minX = Mathf.Min(point1.x, point2.x) - extend;
        float minZ = Mathf.Min(point1.y, point2.y) - extend;
        float maxX = Mathf.Max(point1.x, point2.x) + extend;
        float maxZ = Mathf.Max(point1.y, point2.y) + extend;
        worldCorners[0].x = minX;
        worldCorners[0].z = minZ;
        worldCorners[1].x = minX;
        worldCorners[1].z = maxZ;
        worldCorners[2].x = maxX;
        worldCorners[2].z = maxZ;
        worldCorners[3].x = maxX;
        worldCorners[3].z = minZ;
        for (int i = 0; i < 4; i++)
        {
            var screenCorner = cam.WorldToScreenPoint(worldCorners[i]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screenCorner, null, out canvasCorners[i]);
        }
        this.color = color;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        for (int i = 0; i < 4; i++)
            vh.AddVert(canvasCorners[i], color, Vector2.zero);
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(0, 2, 3);
    }
}
