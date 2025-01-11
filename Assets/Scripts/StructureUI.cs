using UnityEngine;

public class StructureUI : MonoBehaviour
{
    [SerializeField] private GameObject ui;

    public void Focus()
    {
        ui.SetActive(true);
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
