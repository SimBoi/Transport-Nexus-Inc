using UnityEngine;
using System.Collections.Generic;
using Structures;
using Inventories;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

[System.Serializable]
public enum ControlsMode
{
    Navigation,
    Void,
    Deconstruct
}

[RequireComponent(typeof(Camera))]
public class MouseControls : MonoBehaviour
{
    public ControlsMode mode;
    public int ModeInt
    {
        get { return (int)mode; }
        set { mode = (ControlsMode)value; }
    }
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
    private InputAction pointerInput;
    private InputAction zoomInput;
    private bool wasPointerPressedOverUI;

    void Start()
    {
        pointerInput = InputSystem.actions.FindAction("Pointer");
        zoomInput = InputSystem.actions.FindAction("Zoom");
        cam = GetComponent<Camera>();
        worldPlane = new Plane(Vector3.up, Vector3.zero);
        viewTarget = transform.position;
        ChangeViewDistance(0.5f * viewDistanceRange.x + 0.5f * viewDistanceRange.y);
    }

    void Update()
    {
        if (pointerInput.WasPressedThisFrame())
        {
            wasPointerPressedOverUI = false;

            var eventDataCurrentPosition = new PointerEventData(EventSystem.current)
            {
                position = pointerInput.ReadValue<Vector2>()
            };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventDataCurrentPosition, results);

            for (int i = 0; i < results.Count; i++)
                if (results[i].gameObject.layer == 5) //5 = UI layer
                    wasPointerPressedOverUI = true;
        }

        if (mode == ControlsMode.Navigation)
        {
            ChangeViewDistance(-zoomInput.ReadValue<Vector2>().y * zoomSensitivity);
            ChangeViewPosition();
            // smoothly interpolate to the target camera position
            transform.position = Vector3.Lerp(transform.position, viewTarget, 1 - Mathf.Exp(-smoothnessLambda * Time.deltaTime));
        }
        else if (mode == ControlsMode.Void)
        {
            var colliders = SelectArea(voidToolSelectionColor, voidToolLayerMask, minSelectionDistance);
            if (colliders != null)
                foreach (Collider collider in colliders)
                    if (collider.GetComponent<ResourceEntity>() is ResourceEntity resource)
                        resource.DestroyResource();
        }
        else if (mode == ControlsMode.Deconstruct)
        {
            var colliders = SelectArea(deconstructToolSelectionColor, deconstructToolFirstLayerMask, minSelectionDistance, true);
            if (colliders != null)
            {
                foreach (Collider collider in colliders)
                    if (collider.GetComponentInParent<Train>() is Train train)
                        train.DestroyTrain();
                colliders = SelectArea(deconstructToolSelectionColor, deconstructToolSecondLayerMask, minSelectionDistance, true);
                foreach (Collider collider in colliders)
                    if (collider.GetComponentInParent<StructureEntity>() is StructureEntity structure)
                        GameManager.Instance.RemoveStructure(structure.tile);
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
        if (!wasPointerPressedOverUI && pointerInput.WasPressedThisFrame())
        {
            Ray ray = cam.ScreenPointToRay(pointerInput.ReadValue<Vector2>());
            worldPlane.Raycast(ray, out float enter);
            viewAnchor = ray.GetPoint(enter);
        }

        // user starts dragging, update the target camera position to the position where the anchor would be under the new cursor position
        if (!wasPointerPressedOverUI && pointerInput.IsPressed())
        {
            Ray targetRay = new(viewAnchor, -cam.ScreenPointToRay(pointerInput.ReadValue<Vector2>()).direction);
            viewPlane.Raycast(targetRay, out float enter);
            viewTarget = targetRay.GetPoint(enter);
        }
    }

    Collider[] SelectArea(Color color, LayerMask layerMask, float minDistance = 0, bool tileMode = false)
    {
        if (!wasPointerPressedOverUI && pointerInput.WasPressedThisFrame())
        {
            Ray ray = cam.ScreenPointToRay(pointerInput.ReadValue<Vector2>());
            worldPlane.Raycast(ray, out float enter);
            selectionAnchor = ray.GetPoint(enter);
            if (tileMode)
                selectionAnchor = GameManager.Vector3ToTile(ray.GetPoint(enter));
            else
                selectionAnchor = new Vector2(ray.GetPoint(enter).x, ray.GetPoint(enter).z);
        }

        if (!wasPointerPressedOverUI && pointerInput.IsPressed())
        {
            Ray ray = cam.ScreenPointToRay(pointerInput.ReadValue<Vector2>());
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
        if (!wasPointerPressedOverUI && pointerInput.WasReleasedThisFrame() && Vector2.Distance(selectionAnchor, selectionTarget) >= minDistance)
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
