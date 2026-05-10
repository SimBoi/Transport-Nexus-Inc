using System.Collections.Generic;
using Inventories;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BuildingHotbarSlot : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;
    [SerializeField] private Image _image;
    [SerializeField] private Button _button;

    public void Initialize(BuildableEntity buildableEntity, UnityAction SelectBuildableEntity, List<EventTrigger.Entry> eventTriggers = null)
    {
        _text.text = buildableEntity.entityName;
        _image.sprite = buildableEntity.icon;
        _button.onClick.AddListener(SelectBuildableEntity);

        if (eventTriggers != null)
        {
            EventTrigger eventTrigger = _button.gameObject.AddComponent<EventTrigger>();
            eventTrigger.triggers = eventTriggers;
        }
    }
}
