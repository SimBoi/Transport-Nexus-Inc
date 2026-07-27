using UnityEngine;
using System.Collections.Generic;

public enum ControlsMode
{
    Navigation,
    VoidTool,
    DeconstructMode
}

public class MouseControls : MonoBehaviour
{
    public ControlsMode mode;
    [SerializeField] private Color voidToolSelectionColor;
    [SerializeField] private Color deconstructToolSelectionColor;
    private Vector3 viewAnchor;
    private Camera cam;
    private Vector3 viewTarget;
    [SerializeField] private Vector2 viewDistanceRange;
    [SerializeField] private float zoomSensitivity;
    private float viewDistance = 0;
    private Plane worldPlane;
    private Plane viewPlane;
    [SerializeField] private float smoothnessLambda;
    private Vector2 selectionAnchor;
    private Vector2 selectionTarget;
    [SerializeField] private TileSelectionUI selectionUi;
    [SerializeField] private float selectionBoxHeight;
    [SerializeField] private float minSelectionDistance;
    [SerializeField] private LayerMask voidToolLayerMask;
    [SerializeField] private LayerMask deconstructToolFirstLayerMask;
    [SerializeField] private LayerMask deconstructToolSecondLayerMask;

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
            var colliders = SelectArea(voidToolSelectionColor, voidToolLayerMask, minSelectionDistance);
            if (colliders != null)
                for (int i = 0; i < colliders.Length; i++)
                    Debug.Log("Hit : " + colliders[i].name + i);
        }
        else if (mode == ControlsMode.DeconstructMode)
        {
            var colliders = SelectArea(deconstructToolSelectionColor, deconstructToolFirstLayerMask, minSelectionDistance, true);
            if (colliders != null)
            {
                print("----------------------------------------------- layer 1");
                foreach (Collider collider in colliders)
                {
                    if (collider.GetComponentInParent<Train>() is Train train)
                        train.DestroyTrain();
                }
                colliders = SelectArea(deconstructToolSelectionColor, deconstructToolSecondLayerMask, minSelectionDistance, true);
                print("----------------------------------------------- layer 2");
                for (int i = 0; i < colliders.Length; i++)
                    Debug.Log("Hit : " + colliders[i].name + i);
            }
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

    Collider[] SelectArea(Color color, LayerMask layerMask, float minDistance = 0, bool tileMode = false)
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            worldPlane.Raycast(ray, out float enter);
            selectionAnchor = ray.GetPoint(enter);
            if (tileMode)
                selectionAnchor = GameManager.Vector3ToTile(ray.GetPoint(enter));
            else
                selectionAnchor = new Vector2(ray.GetPoint(enter).x, ray.GetPoint(enter).z);
        }

        if (Input.GetMouseButton(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            worldPlane.Raycast(ray, out float enter);
            selectionTarget = ray.GetPoint(enter);
            if (tileMode)
                selectionTarget = GameManager.Vector3ToTile(ray.GetPoint(enter));
            else
                selectionTarget = new Vector2(ray.GetPoint(enter).x, ray.GetPoint(enter).z);
            if (Vector2.Distance(selectionAnchor, selectionTarget) >= minDistance)
            {
                selectionUi.gameObject.SetActive(true);
                selectionUi.UpdateQuad(selectionAnchor, selectionTarget, color, tileMode ? 0.5f : 0);
            }
            else
            {
                selectionUi.gameObject.SetActive(false);
                return null;
            }
        }

        // raycast the selection area when the interactive selection ends
        if (Input.GetMouseButtonUp(0))
        {
            selectionUi.gameObject.SetActive(false);

            var center2d = (selectionAnchor + selectionTarget) / 2;
            var center = new Vector3(center2d.x, 0, center2d.y);
            var delta = selectionAnchor - selectionTarget;
            var halfExtents = new Vector3(Mathf.Abs(delta.x / 2), selectionBoxHeight, Mathf.Abs(delta.y / 2));
            return Physics.OverlapBox(center, halfExtents, Quaternion.identity, layerMask);
        }
        return null;
    }
}
