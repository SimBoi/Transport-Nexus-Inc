using System.Collections.Generic;
using Inventories;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;
    [SerializeField] private Image _image;
    [SerializeField] private Button _button;

    public void Initialize(Item item, UnityAction SelectItem, List<EventTrigger.Entry> eventTriggers = null)
    {
        _text.text = item.itemName;
        _image.sprite = item.icon;
        _button.onClick.AddListener(SelectItem);

        if (eventTriggers != null)
        {
            EventTrigger eventTrigger = _button.gameObject.AddComponent<EventTrigger>();
            eventTrigger.triggers = eventTriggers;
        }
    }
}
