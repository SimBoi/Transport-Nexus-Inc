using System.Collections.Generic;
using Signals;
using UnityEngine;
using UnityEngine.EventSystems;

public class PortUI : MonoBehaviour
{
    public Port port;
    public GameObject wirePrefab;
    private AutoWireResizer _draggedWireResizer = null;

    public void StartDrag(BaseEventData eventData)
    {
        GameManager.Instance.UnfocusStructure(excludePorts: new List<Port> { port });
        GameManager.Instance.HighlightDisconnectedPorts(port.transform.position, 5, new List<Port> { port });
        _draggedWireResizer = Instantiate(wirePrefab, port.transform.position, Quaternion.identity).GetComponent<AutoWireResizer>();
        _draggedWireResizer.SetStart(port.transform.position);
        _draggedWireResizer.transform.parent = transform;
        GetComponent<SpriteRenderer>().enabled = false;
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
        Ray ray = Camera.main.ScreenPointToRay(((PointerEventData)eventData).position);
        if (Physics.Raycast(ray, out RaycastHit result))
        {
            PortUI endPortUI = result.collider.GetComponent<PortUI>();
            if (endPortUI != null && endPortUI.port != port)
            {
                _draggedWireResizer.SetEnd(endPortUI.port.transform.position);
                _draggedWireResizer.transform.parent = null;
                GameManager.Instance.ConnectWire(port, endPortUI.port, _draggedWireResizer.gameObject);
            }
            else
            {
                Destroy(_draggedWireResizer.gameObject);
            }
        }
        GameManager.Instance.UnhighlightDisconnectedPorts();

        // refocus the structure containing the port
        GameObject portStructure = port.GetComponentInParent<StructureUI>().gameObject;
        GameManager.Instance.FocusStructure(portStructure);
    }
}
