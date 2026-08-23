using Structures;
using UnityEngine;

public class BuildingUI : MonoBehaviour
{
    public StructureEntity structure;
    public StructureUI structureUI;
    public Vector3 offset;

    void Update()
    {
        transform.position = Camera.main.WorldToScreenPoint(structureUI.focusPoint.position + offset);
    }

    public void RemoveFocusedStructure()
    {
        GameManager.Instance.RemoveStructure(structure);
    }

    public void RotateFocusedStructure()
    {
        GameManager.Instance.RotateStructureClockwise(structure);
    }
}
