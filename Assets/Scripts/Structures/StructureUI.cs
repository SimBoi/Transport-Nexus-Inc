using UnityEngine;

public class StructureUI : MonoBehaviour
{
    [SerializeField] private GameObject canvas;
    [SerializeField] private Transform screenFocusPoint;
    public Transform focusPoint;

    void Update()
    {
        screenFocusPoint.position = Camera.main.WorldToScreenPoint(focusPoint.position);
    }

    public void Focus()
    {
        canvas.SetActive(true);
    }

    public void Unfocus()
    {
        canvas.SetActive(false);
    }

    public void OnPointerClick()
    {
        GameManager.Instance.FocusStructure(gameObject);
    }
}
