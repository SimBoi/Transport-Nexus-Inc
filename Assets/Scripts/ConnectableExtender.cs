using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ConnectableExtender : MonoBehaviour
{
    [HideInInspector] public GameObject connectablePrefab;
    [HideInInspector] public Vector2Int tile;
    [HideInInspector] public Vector2Int orientation;
    [HideInInspector] public bool isReversed;

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
            if (isReversed && newTile != tile - orientation) return;
            else if (!isReversed && newTile != tile + orientation) return;
            if (!GameManager.Instance.AddStructure(tile, orientation, connectablePrefab, switchFocus: false)) {
                EndDrag(eventData);
                return;
            }
            transform.position = GameManager.TileToVector3(newTile);
            tile = newTile;
        }
    }

    public void EndDrag(BaseEventData eventData)
    {
        GameManager.Instance.UnfocusAll();
        if (isReversed)
            GameManager.Instance.FocusStructure(tile + orientation);
        else
            GameManager.Instance.FocusStructure(tile - orientation);
    }
}
