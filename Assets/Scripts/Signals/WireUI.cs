using GogoGaga.OptimizedRopesAndCables;
using UnityEngine;

public class WireUI : MonoBehaviour
{
    public GameObject wire;
    Rope rope;

    void Start()
    {
        rope = wire.GetComponent<Rope>();
    }

    void Update()
    {
        Vector3 position = (rope.StartPoint.position + rope.EndPoint.position) / 2;
        transform.position = Camera.main.WorldToScreenPoint(position);
    }

    public void DisconnectWire()
    {
        GameManager.Instance.signalNetworkGraph.DisconnectWire(wire);
        GameManager.Instance.UnfocusAll();
    }
}
