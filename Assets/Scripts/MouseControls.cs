using UnityEngine;
using System.Collections.Generic;

public enum ControlsMode
{
    Navigation,
    VoidTool
}

public class MouseControls : MonoBehaviour
{
    public ControlsMode mode;
    [SerializeField] private Color voidToolSelectionColor;
    private Vector3 viewAnchor;
    private Camera cam;
    private Vector3 viewTarget;
    [SerializeField] private Vector2 viewDistanceRange;
    [SerializeField] private float zoomSensitivity;
    private float viewDistance = 0;
    private Plane worldPlane;
    private Plane viewPlane;
    [SerializeField] private float smoothnessLambda;
    private Vector2Int selectionAnchor;
    private Vector2Int selectionTarget;
    [SerializeField] private TileSelectionUI selectionUi;

    void Start()
    {
        cam = GetComponent<Camera>();
        worldPlane = new Plane(Vector3.up, Vector3.zero);
        viewTarget = transform.position;
        ChangeViewDistance(0.5f * viewDistanceRange.x + 0.5f * viewDistanceRange.y);
    }

    void Update()
    {
        if (mode == ControlsMode.Navigation)
        {
            ChangeViewDistance(-Input.mouseScrollDelta.y * zoomSensitivity);
            ChangeViewPosition();
            // smoothly interpolate to the target camera position
            transform.position = Vector3.Lerp(transform.position, viewTarget, 1 - Mathf.Exp(-smoothnessLambda * Time.deltaTime));
        }
        else if (mode == ControlsMode.VoidTool)
        {
            SelectArea(voidToolSelectionColor);
        }
    }

    void ChangeViewDistance(float deltaDistance)
    {
        if (deltaDistance == 0) return;
        viewDistance += deltaDistance;
        viewDistance = Mathf.Clamp(viewDistance, viewDistanceRange.x, viewDistanceRange.y);
        viewPlane = new Plane(Vector3.up, viewDistance * Vector3.up);
        Ray targetRay = new(transform.position, transform.forward);
        viewPlane.Raycast(targetRay, out float enter);
        viewTarget = targetRay.GetPoint(enter);
    }

    void ChangeViewPosition()
    {
        // user clicks on the screen, save the 3d point under the cursor
        if (Input.GetMouseButtonDown(2))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            worldPlane.Raycast(ray, out float enter);
            viewAnchor = ray.GetPoint(enter);
        }

        // user starts dragging, update the target camera position to the position where the anchor would be under the new cursor position
        if (Input.GetMouseButton(2))
        {
            Ray targetRay = new(viewAnchor, -cam.ScreenPointToRay(Input.mousePosition).direction);
            viewPlane.Raycast(targetRay, out float enter);
            viewTarget = targetRay.GetPoint(enter);
        }
    }

    bool SelectArea(Color color)
    {
        // user clicks on the screen, save the 3d point under the cursor
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            worldPlane.Raycast(ray, out float enter);
            selectionAnchor = GameManager.Vector3ToTile(ray.GetPoint(enter));
            selectionUi.gameObject.SetActive(true);
        }

        // user starts dragging, update the target position to the position where the selection would be end
        if (Input.GetMouseButton(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            worldPlane.Raycast(ray, out float enter);
            selectionTarget = GameManager.Vector3ToTile(ray.GetPoint(enter));
            selectionUi.UpdateQuad(selectionAnchor, selectionTarget, color);
        }

        if (Input.GetMouseButtonUp(0))
        {
            selectionUi.gameObject.SetActive(false);
            return true;
        }
        return false;
    }
}
