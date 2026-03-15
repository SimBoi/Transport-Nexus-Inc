using UnityEngine;

public class MouseLook : MonoBehaviour
{
    private Vector3 anchor;
    private Camera cam;
    private Vector3 target;
    [SerializeField] private Vector2 viewDistanceRange;
    [SerializeField] private float zoomSensitivity;
    private float viewDistance = 0;
    private Plane worldPlane;
    private Plane viewPlane;
    [SerializeField] private float smoothnessLambda;

    void Start()
    {
        cam = GetComponent<Camera>();
        worldPlane = new Plane(Vector3.up, Vector3.zero);
        target = transform.position;
        ChangeViewDistance(0.5f * viewDistanceRange.x + 0.5f * viewDistanceRange.y);
    }

    void ChangeViewDistance(float deltaDistance)
    {
        if (deltaDistance == 0) return;
        viewDistance += deltaDistance;
        viewDistance = Mathf.Clamp(viewDistance, viewDistanceRange.x, viewDistanceRange.y);
        viewPlane = new Plane(Vector3.up, viewDistance * Vector3.up);
        Ray targetRay = new(transform.position, transform.forward);
        viewPlane.Raycast(targetRay, out float enter);
        target = targetRay.GetPoint(enter);
    }

    void Update()
    {
        // control view distance
        ChangeViewDistance(-Input.mouseScrollDelta.y * zoomSensitivity);

        // user clicks on the screen, save the 3d point under the cursor
        if (Input.GetMouseButtonDown(2))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            worldPlane.Raycast(ray, out float enter);
            anchor = ray.GetPoint(enter);
        }

        // user starts dragging, update the target camera position to the position where the anchor would be under the new cursor position
        if (Input.GetMouseButton(2))
        {
            Ray targetRay = new(anchor, -cam.ScreenPointToRay(Input.mousePosition).direction);
            viewPlane.Raycast(targetRay, out float enter);
            target = targetRay.GetPoint(enter);
        }

        // smoothly interpolate to the target camera position
        transform.position = Vector3.Lerp(transform.position, target, 1 - Mathf.Exp(-smoothnessLambda * Time.deltaTime));
    }
}
