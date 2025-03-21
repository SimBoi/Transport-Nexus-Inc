using UnityEngine;

public class RailExtender : MonoBehaviour
{
    [SerializeField] private GameObject railPrefab;
    [HideInInspector] public Vector2Int tile;

    public void OnClick()
    {
        GameManager.Instance.AddStructure(tile, Vector2Int.up, railPrefab);
    }
}
