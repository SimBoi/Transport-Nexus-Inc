using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ConnectableExtender : MonoBehaviour
{
    [HideInInspector] public GameObject connectablePrefab;
    [HideInInspector] public Vector2Int tile;
    [HideInInspector] public Vector2Int orientation;

    public void OnClick()
    {
        GameManager.Instance.AddStructure(tile, orientation, connectablePrefab);
    }

    public void StartDrag(BaseEventData eventData)
    {
        GameManager.Instance.Unfocus(excludeExtenders: new List<ConnectableExtender> { this });
    }

    public void Drag(BaseEventData eventData)
    {
        Ray ray = Camera.main.ScreenPointToRay(((PointerEventData)eventData).position);
        if (Physics.Raycast(ray, out RaycastHit result))
        {
            Vector3 pos = result.point + Vector3.up * 0.125f;
            Vector2Int newTile = GameManager.Vector3ToTile(pos);
            if (newTile != tile + orientation) return;
            if (!GameManager.Instance.AddStructure(tile, orientation, connectablePrefab, switchFocus: false)) {
                EndDrag(eventData);
                return;
            }
            tile = newTile;
        }
    }

    public void EndDrag(BaseEventData eventData)
    {
        GameManager.Instance.UnfocusAll();
        GameManager.Instance.FocusStructure(tile - orientation);
    }
}
