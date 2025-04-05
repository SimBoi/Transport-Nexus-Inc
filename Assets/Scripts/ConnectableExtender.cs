using UnityEngine;

public class ConnectableExtender : MonoBehaviour
{
    [HideInInspector] public GameObject connectablePrefab;
    [HideInInspector] public Vector2Int tile;
    [HideInInspector] public Vector2Int orientation;

    public void OnClick()
    {
        GameManager.Instance.AddStructure(tile, orientation, connectablePrefab);
    }
}
