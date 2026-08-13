using UnityEngine;
using Structures;
using Inventories;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class PlayerControls : MonoBehaviour
{
    Vector3 viewAnchor;
    Camera cam;
    Vector3 viewTarget;
    [SerializeField] Vector2 viewDistanceRange;
    [SerializeField] float zoomSensitivity;
    float viewDistance = 0;
    Plane worldPlane;
    Plane viewPlane;
    [SerializeField] float smoothnessLambda;
    [SerializeField] float minDragDistance;
    [SerializeField] Color voidToolSelectionColor;
    [SerializeField] Color deconstructToolSelectionColor;
    Vector2 selectionAnchor;
    Vector2 selectionTarget;
    [SerializeField] TileSelectionUI selectionUi;
    [SerializeField] float selectionBoxHeight;
    [SerializeField] float minSelectionDistance;
    [SerializeField] LayerMask focusStructureLayerMask;
    [SerializeField] LayerMask voidToolLayerMask;
    [SerializeField] LayerMask deconstructToolFirstLayerMask;
    [SerializeField] LayerMask deconstructToolSecondLayerMask;
    InputActionMap defaultMap;
    InputActionMap deconstructToolMap;
    InputActionMap voidToolMap;
    InputAction pointAction;
    InputAction clickAction;
    InputAction navigateAction;
    InputAction focusStructureAction;
    InputAction zoomAction;
    InputAction deconstructToolSelectAreaAction;
    InputAction voidToolSelectAreaAction;
    InputAction[] uiBlockableActions;

    void Start()
    {
        cam = GetComponent<Camera>();
        worldPlane = new Plane(Vector3.up, Vector3.zero);
        viewTarget = transform.position;
        ChangeViewDistance(0.5f * viewDistanceRange.x + 0.5f * viewDistanceRange.y);

        defaultMap = InputSystem.actions.FindActionMap("Default");
        deconstructToolMap = InputSystem.actions.FindActionMap("DeconstructTool");
        voidToolMap = InputSystem.actions.FindActionMap("VoidTool");

        pointAction = InputSystem.actions.FindAction("UI/Point");
        clickAction = InputSystem.actions.FindAction("UI/Click");

        navigateAction = defaultMap.FindAction("Navigate");
        focusStructureAction = defaultMap.FindAction("FocusStructure");
        zoomAction = defaultMap.FindAction("Zoom");
        deconstructToolSelectAreaAction = deconstructToolMap.FindAction("SelectArea");
        voidToolSelectAreaAction = voidToolMap.FindAction("SelectArea");

        uiBlockableActions = new[] {
            navigateAction,
            focusStructureAction,
            deconstructToolSelectAreaAction,
            voidToolSelectAreaAction
        };

        // set default active InputActionMap
        defaultMap.Enable();
        deconstructToolMap.Disable();
        voidToolMap.Disable();
    }

    public void ChangeInputActionMap(string newMap)
    {
        defaultMap.Disable();
        deconstructToolMap.Disable();
        voidToolMap.Disable();
        InputSystem.actions.FindActionMap(newMap).Enable();
    }

    void Update()
    {
        BlockUIClickThrough();

        if (defaultMap.enabled)
        {
            ChangeViewDistance(-zoomAction.ReadValue<Vector2>().y * zoomSensitivity);
            ChangeViewPosition();
            FocusStructure();
            // smoothly interpolate to the target camera position
            transform.position = Vector3.Lerp(
                transform.position,
                viewTarget,
                1 - Mathf.Exp(-smoothnessLambda * Time.deltaTime)
            );
        }
        else if (deconstructToolMap.enabled)
        {
            var colliders = SelectArea(
                deconstructToolSelectAreaAction,
                deconstructToolSelectionColor,
                deconstructToolFirstLayerMask,
                minSelectionDistance,
                true
            );
            if (colliders != null)
                foreach (Collider collider in colliders)
                    if (collider.GetComponentInParent<Train>() is Train train)
                        train.DestroyTrain();

            colliders = SelectArea(
                deconstructToolSelectAreaAction,
                deconstructToolSelectionColor,
                deconstructToolSecondLayerMask,
                minSelectionDistance,
                true
            );
            if (colliders != null)
                foreach (Collider collider in colliders)
                    if (collider.GetComponentInParent<StructureEntity>() is StructureEntity structure)
                        GameManager.Instance.RemoveStructure(structure.tile);
        }
        else if (voidToolMap.enabled)
        {
            var colliders = SelectArea(
                voidToolSelectAreaAction,
                voidToolSelectionColor,
                voidToolLayerMask,
                minSelectionDistance
            );
            if (colliders != null)
                foreach (Collider collider in colliders)
                    if (collider.GetComponent<ResourceEntity>() is ResourceEntity resource)
                        resource.DestroyResource();
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
        if (navigateAction.WasPressedThisFrame())
        {
            Ray ray = cam.ScreenPointToRay(pointAction.ReadValue<Vector2>());
            worldPlane.Raycast(ray, out float enter);
            viewAnchor = ray.GetPoint(enter);
        }

        // user starts dragging, update the target camera position to the position where the anchor would be under the new cursor position
        if (navigateAction.IsPressed())
        {
            Ray targetRay = new(viewAnchor, -cam.ScreenPointToRay(pointAction.ReadValue<Vector2>()).direction);
            viewPlane.Raycast(targetRay, out float enter);
            viewTarget = targetRay.GetPoint(enter);
        }
    }

    void FocusStructure()
    {
        if (!focusStructureAction.WasPerformedThisFrame())
            return;

        Ray ray = cam.ScreenPointToRay(pointAction.ReadValue<Vector2>());
        Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, focusStructureLayerMask);

        if (hit.collider == null || hit.collider.GetComponentInParent<StructureUI>() is not StructureUI structureUI)
            GameManager.Instance.UnfocusAll();
        else
            structureUI.OnPointerClick();
    }

    Collider[] SelectArea(InputAction action, Color color, LayerMask layerMask, float minDistance = 0, bool tileMode = false)
    {
        if (action.WasPressedThisFrame())
        {
            Ray ray = cam.ScreenPointToRay(pointAction.ReadValue<Vector2>());
            worldPlane.Raycast(ray, out float enter);
            selectionAnchor = ray.GetPoint(enter);
            if (tileMode)
                selectionAnchor = GameManager.Vector3ToTile(ray.GetPoint(enter));
            else
                selectionAnchor = new Vector2(ray.GetPoint(enter).x, ray.GetPoint(enter).z);
        }

        if (action.IsPressed())
        {
            Ray ray = cam.ScreenPointToRay(pointAction.ReadValue<Vector2>());
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
        if (action.WasReleasedThisFrame() && Vector2.Distance(selectionAnchor, selectionTarget) >= minDistance)
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

    void BlockUIClickThrough()
    {
        if (clickAction.WasPressedThisFrame())
        {
            bool isOverUI = false;
            var eventDataCurrentPosition = new PointerEventData(EventSystem.current)
            {
                position = pointAction.ReadValue<Vector2>()
            };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventDataCurrentPosition, results);
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].gameObject.layer != 5) //5 = UI layer
                    continue;
                isOverUI = true;
                break;
            }
            if (!isOverUI) return;

            foreach (InputAction action in uiBlockableActions)
            {
                if (action.actionMap.enabled && action.WasPressedThisFrame())
                {
                    action.Reset();
                    action.Disable();
                }
            }
        }
        else if (clickAction.WasReleasedThisFrame())
        {
            foreach (InputAction action in uiBlockableActions)
                if (action.actionMap.enabled)
                    action.Enable();
        }
    }
}
