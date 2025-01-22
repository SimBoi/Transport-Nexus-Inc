using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private Dictionary<Vector2Int, (Vector2Int orientation, object structure)> _tiles = new();

    public Structures.SignalNetworkGraph signalNetworkGraph { get; private set; } = new();
    private Dictionary<Vector2Int, Structures.Sensor> _sensors = new();
    private Dictionary<Vector2Int, Structures.Processor> _processors = new();
    private Dictionary<Vector2Int, Structures.Actuator> _actuators = new();

    private bool _isFocused = false;
    private GameObject _focusedStructure;
    [SerializeField] private GameObject portUIPrefab;
    private List<GameObject> _highlightedPorts = new();
    [SerializeField] private GameObject buildingUIPrefab;
    private GameObject _buildingUI;

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
        foreach (Structures.Sensor sensor in _sensors.Values) sensor.Read();

        // Step 3: Advance all processor output queues
        foreach (Structures.Processor processor in _processors.Values) processor.AdvanceOutputQueue();

        // Step 4: Process all processors and write to output queues
        foreach (Structures.Processor processor in _processors.Values) processor.Process();

        // Step 5: Write all actuators
        foreach (Structures.Actuator actuator in _actuators.Values) actuator.Write();
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////
    //////////////////////////////////// Tile Management ///////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////

    public void AddStructure(Vector2Int tile, Vector2Int orientation, GameObject structurePrefab)
    {
        Vector3 position = new Vector3(tile.x, 0, tile.y);
        Quaternion rotation = Quaternion.LookRotation(new Vector3(orientation.x, 0, orientation.y), Vector3.up);
        GameObject instantiatedStructure;

        if (structurePrefab.GetComponent<Structures.Sensor>())
        {
            instantiatedStructure = Instantiate(structurePrefab, position, rotation);
            Structures.Sensor sensor = instantiatedStructure.GetComponent<Structures.Sensor>();
            sensor.prefab = structurePrefab;
            _tiles.Add(tile, (orientation, sensor));
            _sensors.Add(tile, sensor);
            sensor.Initialize(signalNetworkGraph);
        }
        else if (structurePrefab.GetComponent<Structures.Processor>())
        {
            instantiatedStructure = Instantiate(structurePrefab, position, rotation);
            Structures.Processor processor = instantiatedStructure.GetComponent<Structures.Processor>();
            processor.prefab = structurePrefab;
            _tiles.Add(tile, (orientation, processor));
            _processors.Add(tile, processor);
            processor.Initialize(signalNetworkGraph);

            // chain input and output processors
            Vector2Int behindTile = tile - orientation;
            Vector2Int frontTile = tile + orientation;
            if (_tiles.ContainsKey(behindTile))
            {
                (Vector2Int orientation, object structure) behindStructure = _tiles[behindTile];
                if (behindStructure.structure is Structures.Processor inputProcessor && behindStructure.orientation == orientation)
                {
                    processor.Chain(inputProcessor);
                }
            }
            if (_tiles.ContainsKey(frontTile))
            {
                (Vector2Int orientation, object structure) frontStructure = _tiles[frontTile];
                if (frontStructure.structure is Structures.Processor outputProcessor && frontStructure.orientation == orientation)
                {
                    outputProcessor.Chain(processor);
                }
            }
        }
        else if (structurePrefab.GetComponent<Structures.Actuator>())
        {
            instantiatedStructure = Instantiate(structurePrefab, position, rotation);
            Structures.Actuator actuator = instantiatedStructure.GetComponent<Structures.Actuator>();
            actuator.prefab = structurePrefab;
            _tiles.Add(tile, (orientation, actuator));
            _actuators.Add(tile, actuator);
            actuator.Initialize(signalNetworkGraph);
        }
        else
        {
            throw new System.Exception("Invalid structure type");
        }

        FocusStructure(instantiatedStructure);
    }

    public void RotateStructure(GameObject structure, Vector2Int orientation = default)
    {
        Vector2Int tile = new Vector2Int(Mathf.RoundToInt(structure.transform.position.x), Mathf.RoundToInt(structure.transform.position.z));
        RotateStructure(tile, orientation);
    }

    public void RotateStructure(Vector2Int tile, Vector2Int orientation = default)
    {
        if (!_tiles.ContainsKey(tile)) throw new System.Exception("No structure found at position: " + tile);

        (Vector2Int oldOrientation, object structure) = _tiles[tile];
        if (oldOrientation == orientation) return;
        Vector2Int newOrientation = orientation == default ? new Vector2Int(oldOrientation.y, -oldOrientation.x) : orientation;
        GameObject prefab = structure switch
        {
            Structures.Sensor _ => _sensors[tile].prefab,
            Structures.Processor _ => _processors[tile].prefab,
            Structures.Actuator _ => _actuators[tile].prefab,
            _ => throw new System.Exception("Invalid structure type")
        };

        RemoveStructure(tile);
        AddStructure(tile, newOrientation, prefab);
    }

    public void RemoveStructure(GameObject structure)
    {
        Vector2Int tile = new Vector2Int(Mathf.RoundToInt(structure.transform.position.x), Mathf.RoundToInt(structure.transform.position.z));
        RemoveStructure(tile);
    }

    public void RemoveStructure(Vector2Int tile)
    {
        GameObject structure;

        if (_sensors.ContainsKey(tile))
        {
            structure = _sensors[tile].gameObject;
            _sensors[tile].outputPort.RemoveFromNetwork();
            _sensors.Remove(tile);
        }
        else if (_processors.ContainsKey(tile))
        {
            structure = _processors[tile].gameObject;
            Structures.Processor processor = _processors[tile];

            if (processor.IsInputChained()) processor.Unchain();
            if (processor.IsOutputChained()) processor.UnchainOutput();

            foreach (Structures.Port inputPort in processor.inputPorts) inputPort.RemoveFromNetwork();
            processor.outputPort.RemoveFromNetwork();

            _processors.Remove(tile);
        }
        else if (_actuators.ContainsKey(tile))
        {
            structure = _actuators[tile].gameObject;
            foreach (Structures.Port inputPort in _actuators[tile].inputPorts) inputPort.RemoveFromNetwork();
            _actuators.Remove(tile);
        }
        else
        {
            throw new System.Exception("No structure found at position: " + tile);
        }

        _tiles.Remove(tile);

        if (_isFocused && _focusedStructure == structure) UnfocusStructure();

        Destroy(structure);
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////// UI /////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////

    public void FocusStructure(GameObject structure, bool structureUI = true, bool portUI = true, List<Structures.Port> excludePorts = null, bool buildingUI = true)
    {
        if (_isFocused) UnfocusStructure();
        _isFocused = true;
        _focusedStructure = structure;

        if (structureUI)
        {
            _focusedStructure.GetComponent<StructureUI>().Focus();
        }
        if (portUI)
        {
            HighlightDisconnectedPorts(structure.transform.position, excludePorts: excludePorts);
        }
        if (buildingUI)
        {
            if (_buildingUI != null) Destroy(_buildingUI);
            Vector3 cameraDirection = Camera.main.transform.forward;
            Vector3 position = structure.transform.position - cameraDirection * 0.5f + new Vector3(-0.75f, 0, -0.75f);
            Quaternion rotation = Quaternion.LookRotation(-cameraDirection, Vector3.up);
            _buildingUI = Instantiate(buildingUIPrefab, position, rotation);
            _buildingUI.GetComponent<BuildingUI>().structure = structure;
        }
    }

    public void UnfocusStructure(bool structureUI = true, bool portUI = true, List<Structures.Port> excludePorts = null, bool buildingUI = true)
    {
        if (!_isFocused) return;
        _isFocused = false;

        if (structureUI && _focusedStructure != null)
        {
            _focusedStructure.GetComponent<StructureUI>().Unfocus();
            _focusedStructure = null;
        }
        if (portUI)
        {
            UnhighlightDisconnectedPorts(excludePorts);
        }
        if (buildingUI && _buildingUI != null)
        {
            Destroy(_buildingUI);
            _buildingUI = null;
        }
    }

    public void UnfocusAll()
    {
        UnfocusStructure();
    }

    public bool IsFocused()
    {
        return _isFocused;
    }

    public void HighlightDisconnectedPorts(Vector3 center, float radius = 0, List<Structures.Port> excludePorts = null)
    {
        // go through all the tiles in a 2*radius square around the center and highlight the ports
        for (int x = -Mathf.FloorToInt(radius); x <= Mathf.FloorToInt(radius); x++)
        {
            for (int y = -Mathf.FloorToInt(radius); y <= Mathf.FloorToInt(radius); y++)
            {
                Vector2Int tile = new Vector2Int(x, y) + new Vector2Int(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.z));
                if (!_tiles.ContainsKey(tile)) continue;

                (Vector2Int _, object structure) = _tiles[tile];
                if (structure is Structures.Sensor sensor)
                {
                    if (excludePorts != null && excludePorts.Contains(sensor.outputPort)) continue;
                    HighlightDisconnectedPort(sensor.outputPort);
                }
                else if (structure is Structures.Processor processor)
                {
                    foreach (Structures.Port port in processor.inputPorts)
                    {
                        if (excludePorts != null && excludePorts.Contains(port)) continue;
                        HighlightDisconnectedPort(port);
                    }
                    if (excludePorts != null && excludePorts.Contains(processor.outputPort)) continue;
                    HighlightDisconnectedPort(processor.outputPort);
                }
                else if (structure is Structures.Actuator actuator)
                {
                    foreach (Structures.Port port in actuator.inputPorts)
                    {
                        if (excludePorts != null && excludePorts.Contains(port)) continue;
                        HighlightDisconnectedPort(port);
                    }
                }
            }
        }
    }

    public void HighlightDisconnectedPort(Structures.Port port)
    {
        if (port.isConnected) return;

        Vector3 cameraDirection = Camera.main.transform.forward;
        Vector3 position = port.transform.position - cameraDirection * 0.5f;
        Quaternion rotation = Quaternion.LookRotation(-cameraDirection, Vector3.up);
        GameObject portUI = Instantiate(portUIPrefab, position, rotation);
        portUI.GetComponent<PortUI>().port = port;
        _highlightedPorts.Add(portUI);
    }

    public void UnhighlightDisconnectedPorts(List<Structures.Port> excludePorts = null)
    {
        List<GameObject> portsToKeep = new();
        foreach (GameObject portUI in _highlightedPorts)
        {
            if (excludePorts != null && excludePorts.Contains(portUI.GetComponent<PortUI>().port)) portsToKeep.Add(portUI);
            else Destroy(portUI);
        }
        _highlightedPorts = portsToKeep;
    }

    public void ConnectWire(Structures.Port port1, Structures.Port port2, GameObject wire)
    {
        signalNetworkGraph.ConnectWire(wire, port1, port2);
    }

    public void DisconnectWire(GameObject wire)
    {
        signalNetworkGraph.DisconnectWire(wire);
        Destroy(wire);
    }
}
