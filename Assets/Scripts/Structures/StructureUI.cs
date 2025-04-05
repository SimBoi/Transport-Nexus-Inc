using UnityEngine;

public class StructureUI : MonoBehaviour
{
    [SerializeField] private GameObject ui;
    public Transform focusPoint;

    public void Focus()
    {
        ui.SetActive(true);
        ui.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward, Vector3.up);
    }

    public void Unfocus()
    {
        ui.SetActive(false);
    }

    public void OnPointerClick()
    {
        GameManager.Instance.FocusStructure(gameObject);
    }
}
