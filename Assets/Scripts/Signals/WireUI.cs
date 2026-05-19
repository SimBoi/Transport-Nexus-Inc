using System.Collections.Generic;
using Signals;
using UnityEngine;
using UnityEngine.EventSystems;

public class WireUI : MonoBehaviour
{
    public GameObject wire;

    public void DisconnectWire()
    {
        GameManager.Instance.signalNetworkGraph.DisconnectWire(wire);
        GameManager.Instance.UnfocusAll();
    }
}
