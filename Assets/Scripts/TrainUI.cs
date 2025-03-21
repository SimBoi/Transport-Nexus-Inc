using UnityEngine;
using UnityEngine.EventSystems;

public class TrainUI : MonoBehaviour
{
    [SerializeField] private GameObject ui;

    public void Focus()
    {
        ui.SetActive(true);
        ui.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward, Vector3.up);
    }

    public void Unfocus()
    {
        ui.SetActive(false);
    }

    public void OnPointerClick(BaseEventData _)
    {
        GameManager.Instance.FocusTrain(gameObject);
    }

    public void DestroyTrain()
    {
        GameManager.Instance.DestroyTrain(GetComponent<Train>());
    }
}
