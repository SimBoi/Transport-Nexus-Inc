using UnityEngine;

public class UIElementFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = Vector3.zero;

    void Start()
    {
        transform.position = Camera.main.WorldToScreenPoint(target.position + offset);
    }

    void LateUpdate()
    {
        transform.position = Camera.main.WorldToScreenPoint(target.position + offset);
    }
}
