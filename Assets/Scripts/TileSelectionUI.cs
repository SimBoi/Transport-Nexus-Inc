using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class TileSelectionUI : Graphic
{
    [SerializeField] private RectTransform canvas;
    [SerializeField] private Camera cam;
    private readonly Vector2[] canvasCorners = new Vector2[4];

    public void UpdateQuad(Vector2 point1, Vector2 point2, Color color)
    {
        float minX = Mathf.Min(point1.x, point2.x) - 0.5f;
        float minZ = Mathf.Min(point1.y, point2.y) - 0.5f;
        float maxX = Mathf.Max(point1.x, point2.x) + 0.5f;
        float maxZ = Mathf.Max(point1.y, point2.y) + 0.5f;
        var corners = new Vector3[4];
        corners[0].x = minX;
        corners[0].z = minZ;
        corners[1].x = minX;
        corners[1].z = maxZ;
        corners[2].x = maxX;
        corners[2].z = maxZ;
        corners[3].x = maxX;
        corners[3].z = minZ;
        for (int i = 0; i < 4; i++)
        {
            corners[i] = cam.WorldToScreenPoint(corners[i]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, corners[i], null, out canvasCorners[i]);
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
