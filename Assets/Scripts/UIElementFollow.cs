using UnityEngine;

public class UIElementFollow : MonoBehaviour
{
    public GameObject target;

    void Start()
    {
        transform.position = Camera.main.WorldToScreenPoint(target.transform.position);
    }

    void LateUpdate()
    {
        transform.position = Camera.main.WorldToScreenPoint(target.transform.position);
    }
}
