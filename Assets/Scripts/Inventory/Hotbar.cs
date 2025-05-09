using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Inventories;

public class Hotbar : MonoBehaviour
{
    [SerializeField] private List<Item> hotbar = new();
    private int selectedItemindex;

    [Header("Hotbar UI")]
    [SerializeField] private GameObject expanded;
    [SerializeField] private GameObject collapsed;
    [SerializeField] private GameObject hotbarSlotPrefab;
    [SerializeField] private int spacing = 50;

    [Header("Item Placement")]
    private GameObject selectedSlot = null;
    private bool isPointerInsideSelectedSlot = false;
    private GameObject draggedItem = null;
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
            Item item = hotbar[i];
            Vector3 position = expanded.transform.position + new Vector3(i * spacing * canvas.scaleFactor, 0, 0);
            GameObject slot = Instantiate(hotbarSlotPrefab, position, Quaternion.identity, expanded.transform);
            int index = i;
            slot.GetComponent<InventorySlot>().Initialize(item, () => SelectItem(index));
        }
    }

    public void SelectItem(int index)
    {
        selectedItemindex = index;

        // update the selected item ui
        if (selectedSlot != null) Destroy(selectedSlot);
        selectedSlot = Instantiate(hotbarSlotPrefab, collapsed.transform.position, Quaternion.identity, collapsed.transform);

        // add event triggers to the selected item ui
        EventTrigger.Entry pointerExitEvent = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit, callback = new EventTrigger.TriggerEvent() };
        EventTrigger.Entry pointerEnterEvent = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter, callback = new EventTrigger.TriggerEvent() };
        EventTrigger.Entry dragEvent = new EventTrigger.Entry { eventID = EventTriggerType.Drag, callback = new EventTrigger.TriggerEvent() };
        EventTrigger.Entry endDragEvent = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag, callback = new EventTrigger.TriggerEvent() };
        pointerExitEvent.callback.AddListener(PointerExitSelectedItem);
        pointerEnterEvent.callback.AddListener(PointerEnterSelectedItem);
        dragEvent.callback.AddListener(DragSelectedItem);
        endDragEvent.callback.AddListener(EndDragSelectedItem);
        List<EventTrigger.Entry> eventTriggers = new List<EventTrigger.Entry> { pointerExitEvent, pointerEnterEvent, dragEvent, endDragEvent };

        // initialize the selected item ui
        selectedSlot.GetComponent<InventorySlot>().Initialize(hotbar[index], ExpandHotbar, eventTriggers);

        // Instantiate the dragged item
        if (draggedItem != null) Destroy(draggedItem);
        draggedItem = Instantiate(hotbar[index].gameObject, Vector3.zero, Quaternion.identity);
        // set the dragged item and all its children layer to ignore raycast
        InitDraggedItemRecursively(draggedItem);
        draggedItem.SetActive(false);

        CollapseHotbar();
    }

    private void InitDraggedItemRecursively(GameObject obj)
    {
        obj.layer = LayerMask.NameToLayer("Ignore Raycast");

        // Start with the children
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            InitDraggedItemRecursively(child.gameObject);
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

    public void PointerExitSelectedItem(BaseEventData data)
    {
        isPointerInsideSelectedSlot = false;
    }

    public void PointerEnterSelectedItem(BaseEventData data)
    {
        isPointerInsideSelectedSlot = true;
    }

    public void DragSelectedItem(BaseEventData data)
    {
        if (isPointerInsideSelectedSlot)
        {
            if (draggedItem.activeSelf) draggedItem.SetActive(false);
        }
        else
        {
            if (!draggedItem.activeSelf)
            {
                GameManager.Instance.Unfocus();
                draggedItem.SetActive(true);
            }

            // move the dragged item with the pointer
            Ray ray = Camera.main.ScreenPointToRay(((PointerEventData)data).position);
            if (Physics.Raycast(ray, out RaycastHit result))
            {
                draggedItem.transform.position = result.point;
            }
        }
    }

    public void EndDragSelectedItem(BaseEventData data)
    {
        // check if the dragging stopped outside the selected item ui and place the item on the ground
        if (isPointerInsideSelectedSlot) return;
        draggedItem.SetActive(false);

        PointerEventData pointerData = (PointerEventData)data;
        Ray ray = Camera.main.ScreenPointToRay(pointerData.position);
        if (Physics.Raycast(ray, out RaycastHit result))
        {
            Vector3 hitPoint = result.point + result.normal * 0.01f;
            hotbar[selectedItemindex].Place(hitPoint, placementOrientation, result.collider);
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
