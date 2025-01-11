using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private Dictionary<Vector2Int, (Vector2Int orientation, object structure)> _tiles = new();

    public Components.SignalNetworkGraph signalNetworkGraph { get; private set; } = new();
    private Dictionary<Vector2Int, Components.Sensor> _sensors = new();
    private Dictionary<Vector2Int, Components.Processor> _processors = new();
    private Dictionary<Vector2Int, Components.Actuator> _actuators = new();

    private GameObject _focusedStructure;
    [SerializeField] private GameObject portUIPrefab;
    private List<GameObject> _highlightedPorts = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void FixedUpdate()
    {
        Tick();
    }

    private void Tick()
    {
        // Step 1: Reset all signal channels
        signalNetworkGraph.ResetSignalChannels();

        // Step 2: Read all sensors
        foreach (Components.Sensor sensor in _sensors.Values) sensor.Read();

        // Step 3: Advance all processor output queues
        foreach (Components.Processor processor in _processors.Values) processor.AdvanceOutputQueue();

        // Step 4: Process all processors and write to output queues
        foreach (Components.Processor processor in _processors.Values) processor.Process();

        // Step 5: Write all actuators
        foreach (Components.Actuator actuator in _actuators.Values) actuator.Write();
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////
    //////////////////////////////////// Tile Management ///////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////

    public void AddStructure(Vector2Int tile, Vector2Int orientation, GameObject structurePrefab)
    {
        Vector3 position = new Vector3(tile.x, 0, tile.y);
        Quaternion rotation = Quaternion.LookRotation(new Vector3(orientation.x, 0, orientation.y), Vector3.up);

        if (structurePrefab.GetComponent<Components.Sensor>())
        {
            Components.Sensor sensor = Instantiate(structurePrefab, position, rotation).GetComponent<Components.Sensor>();
            AddTile(tile, orientation, sensor);
            _sensors.Add(tile, sensor);
            sensor.Initialize(signalNetworkGraph);
        }
        else if (structurePrefab.GetComponent<Components.Processor>())
        {
            Components.Processor processor = Instantiate(structurePrefab, position, rotation).GetComponent<Components.Processor>();
            AddTile(tile, orientation, processor);
            _processors.Add(tile, processor);
            processor.Initialize(signalNetworkGraph);

            // chain input and output processors
            Vector2Int behindTile = tile - orientation;
            Vector2Int frontTile = tile + orientation;
            if (_tiles.ContainsKey(behindTile))
            {
                (Vector2Int orientation, object structure) behindStructure = _tiles[behindTile];
                if (behindStructure.structure is Components.Processor inputProcessor && behindStructure.orientation == orientation)
                {
                    processor.Chain(inputProcessor);
                }
            }
            if (_tiles.ContainsKey(frontTile))
            {
                (Vector2Int orientation, object structure) frontStructure = _tiles[frontTile];
                if (frontStructure.structure is Components.Processor outputProcessor && frontStructure.orientation == orientation)
                {
                    outputProcessor.Chain(processor);
                }
            }
        }
        else if (structurePrefab.GetComponent<Components.Actuator>())
        {
            Components.Actuator actuator = Instantiate(structurePrefab, position, rotation).GetComponent<Components.Actuator>();
            AddTile(tile, orientation, actuator);
            _actuators.Add(tile, actuator);
            actuator.Initialize(signalNetworkGraph);
        }
        else
        {
            throw new System.Exception("Invalid structure type");
        }
    }

    public void RemoveStructure(Vector2Int tile)
    {
        if (_sensors.ContainsKey(tile))
        {
            _sensors.Remove(tile);
        }
        else if (_processors.ContainsKey(tile))
        {
            Components.Processor processor = _processors[tile];

            if (processor.IsInputChained()) processor.Unchain();
            if (processor.IsOutputChained()) processor.UnchainOutput();

            // TODO: remove wiring connections

            _processors.Remove(tile);
        }
        else if (_actuators.ContainsKey(tile))
        {
            _actuators.Remove(tile);
        }
        else
        {
            throw new System.Exception("No structure found at position: " + tile);
        }

        RemoveTile(tile);
    }

    private void AddTile(Vector2Int tile, Vector2Int orientation, object structure)
    {
        if (_tiles.ContainsKey(tile)) throw new System.Exception("Tile already exists at position: " + tile);
        _tiles.Add(tile, (orientation, structure));
    }

    private void RemoveTile(Vector2Int tile)
    {
        if (!_tiles.ContainsKey(tile)) throw new System.Exception("Tile does not exist at position: " + tile);
        _tiles.Remove(tile);
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////// UI /////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////

    public void FocusStructure(GameObject structure)
    {
        if (_focusedStructure != null) UnfocusStructure();
        _focusedStructure = structure;
        _focusedStructure.GetComponent<StructureUI>().Focus();
        HighlightDisconnectedPorts(structure.transform.position);
    }

    public void UnfocusStructure()
    {
        if (_focusedStructure == null) return;
        _focusedStructure.GetComponent<StructureUI>().Unfocus();
        _focusedStructure = null;
        UnhighlightDisconnectedPorts();
    }

    public void HighlightDisconnectedPorts(Vector3 center, float radius = 0, List<Components.Port> excludePorts = null)
    {
        // go through all the tiles in a 2*radius square around the center and highlight the ports
        for (int x = -Mathf.FloorToInt(radius); x <= Mathf.FloorToInt(radius); x++)
        {
            for (int y = -Mathf.FloorToInt(radius); y <= Mathf.FloorToInt(radius); y++)
            {
                Vector2Int tile = new Vector2Int(x, y) + new Vector2Int(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.z));
                if (!_tiles.ContainsKey(tile)) continue;

                (Vector2Int _, object structure) = _tiles[tile];
                if (structure is Components.Sensor sensor)
                {
                    if (excludePorts != null && excludePorts.Contains(sensor.outputPort)) continue;
                    HighlightDisconnectedPort(sensor.outputPort);
                }
                else if (structure is Components.Processor processor)
                {
                    foreach (Components.Port port in processor.inputPorts)
                    {
                        if (excludePorts != null && excludePorts.Contains(port)) continue;
                        HighlightDisconnectedPort(port);
                    }
                    if (excludePorts != null && excludePorts.Contains(processor.outputPort)) continue;
                    HighlightDisconnectedPort(processor.outputPort);
                }
                else if (structure is Components.Actuator actuator)
                {
                    foreach (Components.Port port in actuator.inputPorts)
                    {
                        if (excludePorts != null && excludePorts.Contains(port)) continue;
                        HighlightDisconnectedPort(port);
                    }
                }
            }
        }
    }

    public void HighlightDisconnectedPort(Components.Port port)
    {
        if (port.isConnected) return;

        Vector3 cameraDirection = Camera.main.transform.forward;
        Vector3 position = port.transform.position - cameraDirection * 0.5f;
        Quaternion rotation = Quaternion.LookRotation(-cameraDirection, Vector3.up);
        GameObject portUI = Instantiate(portUIPrefab, position, rotation);
        portUI.GetComponent<PortUI>().port = port;
        _highlightedPorts.Add(portUI);
    }

    public void UnhighlightDisconnectedPorts(List<Components.Port> excludePorts = null)
    {
        List<GameObject> portsToKeep = new();
        foreach (GameObject portUI in _highlightedPorts)
        {
            if (excludePorts != null && excludePorts.Contains(portUI.GetComponent<PortUI>().port)) portsToKeep.Add(portUI);
            else Destroy(portUI);
        }
        _highlightedPorts = portsToKeep;
    }

    public void ConnectWire(Components.Port port1, Components.Port port2, GameObject wire)
    {
        signalNetworkGraph.ConnectWire(wire, port1, port2);
    }

    public void DisconnectWire(GameObject wire)
    {
        signalNetworkGraph.DisconnectWire(wire);
        Destroy(wire);
    }
}
