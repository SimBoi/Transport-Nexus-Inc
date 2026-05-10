using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Inventories;

public class Hotbar : MonoBehaviour
{
    [SerializeField] private List<BuildableEntity> hotbar = new();
    private int selectedBuildableEntityindex;

    [Header("Hotbar UI")]
    [SerializeField] private GameObject expanded;
    [SerializeField] private GameObject collapsed;
    [SerializeField] private GameObject hotbarSlotPrefab;
    [SerializeField] private int spacing = 50;

    [Header("Slot Placement")]
    private GameObject selectedSlot = null;
    private bool isPointerInsideSelectedSlot = false;
    private GameObject draggedBuildableEntity = null;
    public Vector2Int placementOrientation = Vector2Int.up;

    private void Start()
    {
        GenerateHotbar();
    }

    private void GenerateHotbar()
    {
        Canvas canvas = gameObject.GetComponentInParent<Canvas>();
        for (int i = 0; i < hotbar.Count; i++)
        {
            BuildableEntity buildableEntity = hotbar[i];
            Vector3 position = expanded.transform.position + new Vector3(i * spacing * canvas.scaleFactor, 0, 0);
            GameObject slot = Instantiate(hotbarSlotPrefab, position, Quaternion.identity, expanded.transform);
            int index = i;
            slot.GetComponent<BuildingHotbarSlot>().Initialize(buildableEntity, () => SelectBuildableEntity(index));
        }
    }

    public void SelectBuildableEntity(int index)
    {
        selectedBuildableEntityindex = index;

        // update the selected buildable entity ui
        if (selectedSlot != null) Destroy(selectedSlot);
        selectedSlot = Instantiate(hotbarSlotPrefab, collapsed.transform.position, Quaternion.identity, collapsed.transform);

        // add event triggers to the selected buildable entity ui
        EventTrigger.Entry pointerExitEvent = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit, callback = new EventTrigger.TriggerEvent() };
        EventTrigger.Entry pointerEnterEvent = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter, callback = new EventTrigger.TriggerEvent() };
        EventTrigger.Entry dragEvent = new EventTrigger.Entry { eventID = EventTriggerType.Drag, callback = new EventTrigger.TriggerEvent() };
        EventTrigger.Entry endDragEvent = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag, callback = new EventTrigger.TriggerEvent() };
        pointerExitEvent.callback.AddListener(PointerExitSelectedBuildableEntity);
        pointerEnterEvent.callback.AddListener(PointerEnterSelectedBuildableEntity);
        dragEvent.callback.AddListener(DragSelectedBuildableEntity);
        endDragEvent.callback.AddListener(EndDragSelectedBuildableEntity);
        List<EventTrigger.Entry> eventTriggers = new List<EventTrigger.Entry> { pointerExitEvent, pointerEnterEvent, dragEvent, endDragEvent };

        // initialize the selected buildable entity ui
        selectedSlot.GetComponent<BuildingHotbarSlot>().Initialize(hotbar[index], ExpandHotbar, eventTriggers);

        // Instantiate the dragged buildable entity
        if (draggedBuildableEntity != null) Destroy(draggedBuildableEntity);
        draggedBuildableEntity = Instantiate(hotbar[index].gameObject, Vector3.zero, Quaternion.identity);
        // set the dragged buildable entity and all its children layer to ignore raycast
        InitDraggedBuildableEntityRecursively(draggedBuildableEntity);
        draggedBuildableEntity.SetActive(false);

        CollapseHotbar();
    }

    private void InitDraggedBuildableEntityRecursively(GameObject obj)
    {
        obj.layer = LayerMask.NameToLayer("Ignore Raycast");

        // Start with the children
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            InitDraggedBuildableEntityRecursively(child.gameObject);
        }

        // Get all components except Transform, MeshRenderer, and MeshFilter
        Component[] components = obj.GetComponents<Component>();
        // Remove components in reverse order to avoid dependency issues
        for (int i = components.Length - 1; i >= 0; i--)
        {
            Component component = components[i];
            if (component is Transform || component is MeshRenderer || component is MeshFilter) continue;
            Destroy(component);
        }
    }

    public void PointerExitSelectedBuildableEntity(BaseEventData data)
    {
        isPointerInsideSelectedSlot = false;
    }

    public void PointerEnterSelectedBuildableEntity(BaseEventData data)
    {
        isPointerInsideSelectedSlot = true;
    }

    public void DragSelectedBuildableEntity(BaseEventData data)
    {
        if (isPointerInsideSelectedSlot)
        {
            if (draggedBuildableEntity.activeSelf) draggedBuildableEntity.SetActive(false);
        }
        else
        {
            if (!draggedBuildableEntity.activeSelf)
            {
                GameManager.Instance.Unfocus();
                draggedBuildableEntity.SetActive(true);
            }

            // move the dragged buildable entity with the pointer
            Ray ray = Camera.main.ScreenPointToRay(((PointerEventData)data).position);
            if (Physics.Raycast(ray, out RaycastHit result))
            {
                draggedBuildableEntity.transform.position = result.point;
            }
        }
    }

    public void EndDragSelectedBuildableEntity(BaseEventData data)
    {
        // check if the dragging stopped outside the selected buildable entity ui and place it on the ground
        if (isPointerInsideSelectedSlot) return;
        draggedBuildableEntity.SetActive(false);

        PointerEventData pointerData = (PointerEventData)data;
        Ray ray = Camera.main.ScreenPointToRay(pointerData.position);
        if (Physics.Raycast(ray, out RaycastHit result))
        {
            Vector3 hitPoint = result.point + result.normal * 0.01f;
            hotbar[selectedBuildableEntityindex].Place(hitPoint, placementOrientation, result.collider);
        }
    }

    public void ExpandHotbar()
    {
        expanded.SetActive(true);
        collapsed.SetActive(false);
    }

    public void CollapseHotbar()
    {
        expanded.SetActive(false);
        collapsed.SetActive(true);
    }
}
