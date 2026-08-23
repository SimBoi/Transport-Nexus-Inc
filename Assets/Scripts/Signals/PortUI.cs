using System.Collections.Generic;
using Signals;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class PortUI : MonoBehaviour
{
    public Port port;
    public GameObject wirePrefab;
    private AutoWireResizer _draggedWireResizer = null;

    void Update()
    {
        transform.position = Camera.main.WorldToScreenPoint(port.transform.position);
    }

    public void StartDrag(BaseEventData _)
    {
        GameManager.Instance.Unfocus(excludePorts: new List<Port> { port });
        GameManager.Instance.HighlightDisconnectedPorts(port.transform.position, 5, new List<Port> { port });
        _draggedWireResizer = Instantiate(wirePrefab, port.transform.position, Quaternion.identity).GetComponent<AutoWireResizer>();
        _draggedWireResizer.SetStart(port.transform.position);
        _draggedWireResizer.transform.parent = transform;
        GetComponent<Image>().enabled = false;
    }

    public void Drag(BaseEventData eventData)
    {
        Ray ray = Camera.main.ScreenPointToRay(((PointerEventData)eventData).position);
        if (Physics.Raycast(ray, out RaycastHit result))
        {
            _draggedWireResizer.SetEnd(result.point + Vector3.up * 0.125f);
        }
    }

    public void EndDrag(BaseEventData eventData)
    {
        if (GameManager.Instance.IsFocused()) return;

        // raycast to find the end port
        var raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll((PointerEventData)eventData, raycastResults);
        foreach (var raycastResult in raycastResults)
        {
            if (raycastResult.gameObject.layer != 5)
                continue;

            PortUI endPortUI = raycastResult.gameObject.GetComponent<PortUI>();
            if (endPortUI == null || endPortUI.port == port)
                continue;

            _draggedWireResizer.SetEnd(endPortUI.port.transform.position);
            _draggedWireResizer.transform.parent = null;
            GameManager.Instance.ConnectWire(port, endPortUI.port, _draggedWireResizer.gameObject);
            _draggedWireResizer = null;
            break;
        }
        if (_draggedWireResizer != null)
        {
            Destroy(_draggedWireResizer.gameObject);
            _draggedWireResizer = null;
        }

        GameManager.Instance.UnhighlightDisconnectedPorts();
        GameObject portStructure = port.GetComponentInParent<StructureUI>().gameObject;
        GameManager.Instance.FocusStructure(portStructure);
    }
}
