using System.Collections.Generic;
using Structures;
using UnityEngine;
using UnityEngine.EventSystems;

public class BuildingUI : MonoBehaviour
{
    public GameObject structure;

    public void RotateStructure(BaseEventData eventData)
    {
        GameManager.Instance.RotateStructureClockwise(structure);
    }

    public void RemoveStructure(BaseEventData eventData)
    {
        GameManager.Instance.RemoveStructure(structure);
    }
}
