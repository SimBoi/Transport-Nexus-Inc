using System.Collections.Generic;
using Structures;
using Signals;
using UnityEngine;
using System;
using Inventories;
using UnityEditor;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // Prefab registries
    public List<GameObject> savablePrefabs;
    public Dictionary<string, GameObject> savablesPrefabRegistry = new();
    public Dictionary<Materials, GameObject> materialsPrefabRegistry = new();

    public ulong tick = 0;
    [HideInInspector] public int[] materials = new int[Enum.GetValues(typeof(Materials)).Length];

    public PortNetworkGraph signalNetworkGraph { get; private set; } = new();

    private Dictionary<Vector2Int, (Vector2Int orientation, Structure structure)> _tiles = new();
    private Dictionary<Vector2Int, Sensor> _sensors = new();
    private Dictionary<Vector2Int, Processor> _processors = new();
    private Dictionary<Vector2Int, Actuator> _actuators = new();
    private Dictionary<Vector2Int, SplitterPort> _splitterPorts = new();
    private Dictionary<Vector2Int, Structure> _rails = new();
    private Dictionary<Vector2Int, Structure> _conveyors = new();
    private Dictionary<Vector2Int, Machine> _machines = new();

    private List<Train> _trains = new();

    private bool _isFocused = false;
    private GameObject _focusedStructure;
    private GameObject _focusedTrain;
    [SerializeField] private GameObject portUIPrefab;
    private List<GameObject> _highlightedPorts = new();
    [SerializeField] private GameObject buildingUIPrefab;
    private GameObject _buildingUI;
    [SerializeField] private GameObject trainPrefab;
    [SerializeField] private GameObject ConnectableExtenderPrefab;
    private List<GameObject> _railExtenders = new();

    [SerializeField] private GameObject wirePrefab;
    [SerializeField] private Transform powerLevels;
    [SerializeField] private GameObject powerLevelUIPrefab;

    [SerializeField] private GameObject connectableRailPrefab;
    [SerializeField] private GameObject connectableConveyorPrefab;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Build the registry using the prefab's component type name as key
        foreach (GameObject prefab in savablePrefabs)
        {
            ISavable savable = prefab.GetComponent<ISavable>();
            if (savable != null)
            {
                savablesPrefabRegistry[savable.GetType().ToString()] = prefab;
            }
        }
    }

    void FixedUpdate()
    {
        Tick();
        tick++;
    }

    private void Tick()
    {
        // Step 1: Reset all signal channels
        signalNetworkGraph.ResetSignalChannels();

        // Step 2: Read all sensors
        foreach (Sensor sensor in _sensors.Values) sensor.Read();

        // Step 3: Advance all processor output queues
        foreach (Processor processor in _processors.Values) processor.AdvanceOutputQueue();

        // Step 4: Process all processors and write to output queues
        foreach (Processor processor in _processors.Values) processor.Process();

        // Step 5: Write all actuators
        foreach (Actuator actuator in _actuators.Values) actuator.Write();

        // Step 6: Process all machines
        foreach (Machine machine in _machines.Values) machine.Process();
    }

    public void SaveState(SaveData saveData)
    {
        // save tick
        saveData.tick = tick;

        // save materials
        saveData.materials = materials;

        // save tiles
        foreach (KeyValuePair<Vector2Int, (Vector2Int orientation, Structure structure)> entry in _tiles)
        {
            Vector2Int tile = entry.Key;
            (Vector2Int orientation, Structure structure) = entry.Value;
            saveData.tiles.Add((tile, orientation, structure.ID));
        }

        // save signal network graph
        signalNetworkGraph.SaveState(saveData);

        // save trains
        foreach (Train train in _trains) saveData.trainIds.Add(train.ID);
    }

    public void RestoreState(SaveData saveData, Dictionary<int, ISavable> idLookup)
    {
        // restore tick
        tick = saveData.tick;

        // restore materials
        materials = saveData.materials;

        // restore tiles
        foreach ((Vector2Int tile, Vector2Int orientation, int structureId) in saveData.tiles)
        {
            Structure structure = idLookup[structureId] as Structure;
            _tiles.Add(tile, (orientation, structure));
            if (structure is Sensor sensor) _sensors.Add(tile, sensor);
            else if (structure is Processor processor) _processors.Add(tile, processor);
            else if (structure is Actuator actuator) _actuators.Add(tile, actuator);
            else if (structure is SplitterPort splitterPort) _splitterPorts.Add(tile, splitterPort);
            if (structure is DynamicRail || structure is SensorRail || structure is ActuatorRail) _rails.Add(tile, structure);
            else if (structure is DynamicConveyorBelt || structure is SensorConveyorBelt || structure is ActuatorConveyorBelt) _conveyors.Add(tile, structure);
            else if (structure is Machine machine) _machines.Add(tile, machine);
        }

        // restore signal network graph and wires
        signalNetworkGraph.RestoreVertices(saveData, idLookup);
        signalNetworkGraph.RestoreChannels(saveData, idLookup);
        foreach ((int port1Id, int port2Id) in saveData.portConnections)
        {
            Port port1 = idLookup[port1Id] as Port;
            Port port2 = idLookup[port2Id] as Port;
            GameObject wire = Instantiate(wirePrefab, port1.transform.position, Quaternion.identity);
            wire.GetComponent<AutoWireResizer>().SetEnd(port2.transform.position);
            signalNetworkGraph.RestoreEdge(wire, port1, port2);
            ShowPowerLevelUI(port1, port2);
        }

        // restore trains
        foreach (int trainId in saveData.trainIds)
        {
            Train train = idLookup[trainId] as Train;
            _trains.Add(train);
        }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////// Materials //////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////

    public bool HasMaterials(int[] costs)
    {
        for (int i = 0; i < costs.Length; i++)
        {
            if (materials[i] < costs[i]) return false;
        }
        return true;
    }

    public void SpendMaterials(int[] costs)
    {
        for (int i = 0; i < costs.Length; i++)
        {
            materials[i] -= costs[i];
        }
    }

    public void AddMaterials(int[] gains)
    {
        for (int i = 0; i < gains.Length; i++)
        {
            materials[i] += gains[i];
        }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////
    //////////////////////////////////// Tile Management ///////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////

    public static Vector2Int Vector3ToTile(Vector3 position)
    {
        return Vector2Int.RoundToInt(new Vector2(position.x, position.z));
    }

    public static Vector3 TileToVector3(Vector2Int tile)
    {
        return new Vector3(tile.x, 0, tile.y);
    }

    public Structure GetTileStructure(Vector2Int tile)
    {
        if (!_tiles.ContainsKey(tile)) return null;
        return _tiles[tile].structure;
    }

    public Vector2Int GetTileOrientation(Vector2Int tile)
    {
        return _tiles[tile].orientation;
    }

    public bool AddStructure(Vector2Int tile, Vector2Int orientation, GameObject structurePrefab)
    {
        if (_tiles.ContainsKey(tile)) return false;
        int size = structurePrefab.GetComponent<Structure>().size;
        Vector2Int relativeUp = orientation;
        Vector2Int relativeRight = new Vector2Int(relativeUp.y, -relativeUp.x);
        for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) if (_tiles.ContainsKey(tile + x * relativeRight + y * relativeUp)) return false;

        Vector3 position = new Vector3(tile.x, 0, tile.y);
        Quaternion rotation = Quaternion.LookRotation(new Vector3(orientation.x, 0, orientation.y), Vector3.up);
        Structure instantiatedStructure = Instantiate(structurePrefab, position, rotation).GetComponent<Structure>();
        instantiatedStructure.tile = tile;
        instantiatedStructure.orientation = orientation;
        instantiatedStructure.prefab = structurePrefab;
        for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) _tiles.Add(tile + x * relativeRight + y * relativeUp, (orientation, instantiatedStructure));

        if (instantiatedStructure is Machine machine)
        {
            for (int x = 0; x < size; x++) for (int y = 0; y < size; y++)
                {
                    _machines.Add(tile + x * relativeRight + y * relativeUp, machine);
                    _actuators.Add(tile + x * relativeRight + y * relativeUp, machine);
                }
            machine.Initialize(signalNetworkGraph);
        }
        else if (instantiatedStructure is DynamicRail dynamicRail)
        {
            for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) _rails.Add(tile + x * relativeRight + y * relativeUp, dynamicRail);
            ConnectRail(tile);
        }
        else if (instantiatedStructure is SensorRail sensorRail)
        {
            for (int x = 0; x < size; x++) for (int y = 0; y < size; y++)
                {
                    _rails.Add(tile + x * relativeRight + y * relativeUp, sensorRail);
                    _sensors.Add(tile + x * relativeRight + y * relativeUp, sensorRail);
                }
            sensorRail.Initialize(signalNetworkGraph);
            ConnectRail(tile);
        }
        else if (instantiatedStructure is ActuatorRail actuatorRail)
        {
            for (int x = 0; x < size; x++) for (int y = 0; y < size; y++)
                {
                    _rails.Add(tile + x * relativeRight + y * relativeUp, actuatorRail);
                    _actuators.Add(tile + x * relativeRight + y * relativeUp, actuatorRail);
                }
            actuatorRail.Initialize(signalNetworkGraph);
            ConnectRail(tile);
        }
        else if (instantiatedStructure is DynamicConveyorBelt dynamicConveyor)
        {
            for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) _conveyors.Add(tile + x * relativeRight + y * relativeUp, dynamicConveyor);
            ConnectConveyor(tile);
        }
        else if (instantiatedStructure is SensorConveyorBelt sensorConveyor)
        {
            for (int x = 0; x < size; x++) for (int y = 0; y < size; y++)
                {
                    _conveyors.Add(tile + x * relativeRight + y * relativeUp, sensorConveyor);
                    _sensors.Add(tile + x * relativeRight + y * relativeUp, sensorConveyor);
                }
            sensorConveyor.Initialize(signalNetworkGraph);
            ConnectConveyor(tile);
        }
        else if (instantiatedStructure is ActuatorConveyorBelt actuatorConveyor)
        {
            for (int x = 0; x < size; x++) for (int y = 0; y < size; y++)
                {
                    _conveyors.Add(tile + x * relativeRight + y * relativeUp, actuatorConveyor);
                    _actuators.Add(tile + x * relativeRight + y * relativeUp, actuatorConveyor);
                }
            actuatorConveyor.Initialize(signalNetworkGraph);
            ConnectConveyor(tile);
        }
        else if (instantiatedStructure is Sensor sensor)
        {
            for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) _sensors.Add(tile + x * relativeRight + y * relativeUp, sensor);
            sensor.Initialize(signalNetworkGraph);
        }
        else if (instantiatedStructure is Processor processor)
        {
            for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) _processors.Add(tile + x * relativeRight + y * relativeUp, processor);
            processor.Initialize(signalNetworkGraph);

            // chain input and output processors
            Vector2Int behindTile = tile - orientation;
            Vector2Int frontTile = tile + orientation; // TODO: size > 1 support for chaining
            if (_tiles.ContainsKey(behindTile))
            {
                (Vector2Int orientation, Structure structure) behindStructure = _tiles[behindTile];
                if (behindStructure.structure is Processor inputProcessor && behindStructure.orientation == orientation)
                {
                    processor.Chain(inputProcessor);
                }
            }
            if (_tiles.ContainsKey(frontTile))
            {
                (Vector2Int orientation, Structure structure) frontStructure = _tiles[frontTile];
                if (frontStructure.structure is Processor outputProcessor && frontStructure.orientation == orientation)
                {
                    outputProcessor.Chain(processor);
                }
            }
        }
        else if (instantiatedStructure is Actuator actuator)
        {
            for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) _actuators.Add(tile + x * relativeRight + y * relativeUp, actuator);
            actuator.Initialize(signalNetworkGraph);
        }
        else if (instantiatedStructure is SplitterPort splitterPort)
        {
            for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) _splitterPorts.Add(tile + x * relativeRight + y * relativeUp, splitterPort);
            splitterPort.Initialize(signalNetworkGraph);
        }
        else
        {
            Destroy(instantiatedStructure.gameObject);
            throw new Exception("Invalid structure type");
        }

        FocusStructure(instantiatedStructure.gameObject);
        return true;
    }

    public void RotateStructureClockwise(GameObject structure)
    {
        RotateStructureClockwise(structure.GetComponent<Structure>().tile);
    }

    public void RotateStructureClockwise(Vector2Int tile)
    {
        if (!_tiles.ContainsKey(tile)) throw new Exception("No structure found at position: " + tile);

        (Vector2Int oldOrientation, Structure structure) = _tiles[tile];
        (Vector2Int newTile, Vector2Int newOrientation) = Structure.RotateClockwise(tile, oldOrientation, structure.size);

        if (!RemoveStructure(tile)) return;
        AddStructure(newTile, newOrientation, structure.prefab);
    }

    public bool RemoveStructure(GameObject structure)
    {
        return RemoveStructure(structure.GetComponent<Structure>().tile);
    }

    public bool RemoveStructure(Vector2Int tile)
    {
        if (!_tiles.ContainsKey(tile)) throw new Exception("No structure found at position: " + tile);
        Structure structure = _tiles[tile].structure;
        int size = structure.size;
        Vector2Int relativeUp = _tiles[tile].orientation;
        Vector2Int relativeRight = new Vector2Int(relativeUp.y, -relativeUp.x);

        if (_machines.ContainsKey(tile))
        {
            Machine machine = _machines[tile];
            machine.DropInventory();
            foreach (Port inputPort in _actuators[tile].inputPorts) inputPort.RemoveFromNetwork();
            for (int x = 0; x < size; x++) for (int y = 0; y < size; y++)
                {
                    _machines.Remove(tile + x * relativeRight + y * relativeUp);
                    _actuators.Remove(tile + x * relativeRight + y * relativeUp);
                }
        }
        else if (_rails.ContainsKey(tile))
        {
            if (_rails[tile] is DynamicRail dynamicRail && dynamicRail.trains.Count > 0) return false;
            if (_rails[tile] is SensorRail sensorRail && sensorRail.trains.Count > 0) return false;
            if (_rails[tile] is ActuatorRail actuatorRail && actuatorRail.trains.Count > 0) return false;

            if (_sensors.ContainsKey(tile))
            {
                _sensors[tile].outputPort.RemoveFromNetwork();
                for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) _sensors.Remove(tile + x * relativeRight + y * relativeUp);
            }
            else if (_actuators.ContainsKey(tile))
            {
                foreach (Port inputPort in _actuators[tile].inputPorts) inputPort.RemoveFromNetwork();
                for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) _actuators.Remove(tile + x * relativeRight + y * relativeUp);
            }

            DisconnectRail(tile);
            for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) _rails.Remove(tile + x * relativeRight + y * relativeUp);
        }
        else if (_conveyors.ContainsKey(tile))
        {
            List<ConveyedResource> resources = GetTileResources(tile);
            while (resources.Count > 0) resources[0].ExitConveyPath();

            if (_sensors.ContainsKey(tile))
            {
                _sensors[tile].outputPort.RemoveFromNetwork();
                for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) _sensors.Remove(tile + x * relativeRight + y * relativeUp);
            }
            else if (_actuators.ContainsKey(tile))
            {
                foreach (Port inputPort in _actuators[tile].inputPorts) inputPort.RemoveFromNetwork();
                for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) _actuators.Remove(tile + x * relativeRight + y * relativeUp);
            }

            DisconnectConveyor(tile);
            for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) _conveyors.Remove(tile + x * relativeRight + y * relativeUp);
        }
        else if (_sensors.ContainsKey(tile))
        {
            _sensors[tile].outputPort.RemoveFromNetwork();
            for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) _sensors.Remove(tile + x * relativeRight + y * relativeUp);
        }
        else if (_processors.ContainsKey(tile))
        {
            Processor processor = _processors[tile];

            if (processor.IsInputChained()) processor.Unchain();
            if (processor.IsOutputChained()) processor.UnchainOutput();

            foreach (Port inputPort in processor.inputPorts) inputPort.RemoveFromNetwork();
            processor.outputPort.RemoveFromNetwork();

            for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) _processors.Remove(tile + x * relativeRight + y * relativeUp);
        }
        else if (_actuators.ContainsKey(tile))
        {
            foreach (Port inputPort in _actuators[tile].inputPorts) inputPort.RemoveFromNetwork();
            for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) _actuators.Remove(tile + x * relativeRight + y * relativeUp);
        }
        else if (_splitterPorts.ContainsKey(tile))
        {
            _splitterPorts[tile].port.RemoveFromNetwork();
            for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) _splitterPorts.Remove(tile + x * relativeRight + y * relativeUp);
        }

        for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) _tiles.Remove(tile + x * relativeRight + y * relativeUp);

        if (_isFocused && _focusedStructure == structure.gameObject) Unfocus();

        structure.GetComponent<Item>().Destroy();
        return true;
    }

    public void ConnectWire(Port port1, Port port2, GameObject wire)
    {
        signalNetworkGraph.ConnectWire(wire, port1, port2);
        ShowPowerLevelUI(port1, port2);
    }

    public void ShowPowerLevelUI(Port port1, Port port2)
    {
        GameObject powerLevelUI = Instantiate(powerLevelUIPrefab, powerLevels);
        powerLevelUI.GetComponent<PowerLevelUI>().Initialize(port1, port2);
    }

    public void DisconnectWire(GameObject wire)
    {
        signalNetworkGraph.DisconnectWire(wire);
        Destroy(wire);
    }

    // called after placing a new rail, connects the rail to its neighbors
    public void ConnectRail(Vector2Int tile)
    {
        if (!_rails.ContainsKey(tile)) throw new Exception("No rail found at position: " + tile);

        // find the possible neighbors of the rail
        Vector2Int[] neighbors;
        if (_rails[tile] is DynamicRail)
        {
            neighbors = new Vector2Int[4] {
                Vector2Int.down,
                Vector2Int.right,
                Vector2Int.up,
                Vector2Int.left
            };
        }
        else
        {
            (Vector2Int orientation, _) = _tiles[tile];
            neighbors = new Vector2Int[2] {
                orientation,
                -orientation
            };
        }

        // find compatible connections with neighbors
        List<Vector2Int> compatibleConnections = new();
        foreach (Vector2Int neighborDir in neighbors)
        {
            Vector2Int neighborTile = tile + neighborDir;
            if (!_rails.ContainsKey(neighborTile)) continue;

            if (_rails[neighborTile] is DynamicRail neighborRail)
            {
                if (!neighborRail.CanConnect(-neighborDir)) continue;
                compatibleConnections.Add(neighborDir);
            }
            else
            {
                List<Vector2Int> orientations = GetTrainOrientations(neighborTile);
                foreach (Vector2Int orientation in orientations) if (orientation == neighborDir) compatibleConnections.Add(neighborDir);
            }

            if (compatibleConnections.Count == 2) break;
        }

        // orient the rail if its a dynamic rail
        if (_tiles[tile].structure is DynamicRail) foreach (Vector2Int dir in compatibleConnections) ((DynamicRail)_rails[tile]).Connect(dir);

        // reorient neighbors
        foreach (Vector2Int dir in compatibleConnections) if (_rails[tile + dir] is DynamicRail neighborRail) neighborRail.Connect(-dir);
    }

    // disconnects the rail from all neighbors
    public void DisconnectRail(Vector2Int tile)
    {
        // get the neighbors of the rail
        List<Vector2Int> neighbors;
        if (_rails[tile] is DynamicRail)
        {
            DynamicRail dynamicRail = (DynamicRail)_rails[tile];

            neighbors = new();
            foreach (Vector2Int dir in dynamicRail.connections) neighbors.Add(dir);

            // disconnect the rail from its neighbors
            foreach (Vector2Int dir in neighbors) dynamicRail.Disconnect(dir);
        }
        else
        {
            (Vector2Int orientation, _) = _tiles[tile];
            neighbors = new List<Vector2Int> { orientation, -orientation };
        }

        // disconnect the neighbors from the rail
        foreach (Vector2Int dir in neighbors) if (_rails.ContainsKey(tile + dir) && _rails[tile + dir] is DynamicRail neighborRail && neighborRail.connections.Contains(-dir)) neighborRail.Disconnect(-dir);
    }

    public Vector2Int GetNextTrainOrientation(Vector2Int tile, Vector2Int orientation)
    {
        if (!_rails.ContainsKey(tile)) return Vector2Int.zero;

        if (_rails[tile] is DynamicRail dynamicRail)
        {
            foreach (Vector2Int o in dynamicRail.trainOrientations) if (o != orientation) return -o;
            return Vector2Int.zero;
        }
        else if (_rails[tile] is SensorRail sensorRail) return sensorRail.GetNextTrainOrientation(orientation);
        else if (_rails[tile] is ActuatorRail actuatorRail) return actuatorRail.GetNextTrainOrientation(orientation);
        return Vector2Int.zero;
    }

    public List<Vector2Int> GetTrainOrientations(Vector2Int tile)
    {
        if (!_rails.ContainsKey(tile)) return null;

        if (_rails[tile] is DynamicRail dynamicRail) return dynamicRail.trainOrientations;
        else if (_rails[tile] is SensorRail sensorRail) return sensorRail.GetTrainOrientations();
        else if (_rails[tile] is ActuatorRail actuatorRail) return actuatorRail.GetTrainOrientations();
        else return null;
    }

    // called after placing a new conveyor, connects the conveyor to its neighbors
    public void ConnectConveyor(Vector2Int tile)
    {
        if (!_conveyors.ContainsKey(tile)) throw new Exception("No conveyor found at position: " + tile);

        Vector2Int orientation = GetTileOrientation(tile);
        Structure structure = GetTileStructure(tile);

        // find the possible neighbors of the conveyor
        Vector2Int[] neighbors = new Vector2Int[4] {
            Vector2Int.down,
            Vector2Int.right,
            Vector2Int.up,
            Vector2Int.left
        };

        // find compatible connections with neighbors
        List<Vector2Int> compatibleConnections = new();
        foreach (Vector2Int neighborDir in neighbors)
        {
            Vector2Int neighborTile = tile + neighborDir;
            if (!_conveyors.ContainsKey(neighborTile)) continue;

            if (_conveyors[neighborTile] is DynamicConveyorBelt neighborConveyor)
            {
                if (!neighborConveyor.CanConnect(-neighborDir)) continue;

                compatibleConnections.Add(neighborDir);
            }
            else
            {
                Vector2Int neighborOrientation = GetTileOrientation(neighborTile);
                if (neighborOrientation == neighborDir) compatibleConnections.Add(neighborDir);
            }
        }

        // remove conveyors with opposite directions
        for (int i = compatibleConnections.Count - 1; i >= 0; i--)
        {
            Vector2Int neighborOrientation = GetTileOrientation(tile + compatibleConnections[i]);
            if (compatibleConnections[i] == -orientation && neighborOrientation != compatibleConnections[i]) continue; // input conveyor
            if (neighborOrientation == compatibleConnections[i] && neighborOrientation != -orientation) continue; // output conveyor
            compatibleConnections.RemoveAt(i);
        }

        // orient the conveyor if its a dynamic conveyor
        if (_tiles[tile].structure is DynamicConveyorBelt)
        {
            if (compatibleConnections.Count == 0) ((DynamicConveyorBelt)_conveyors[tile]).exitOrientation = orientation;
            foreach (Vector2Int dir in compatibleConnections) ((DynamicConveyorBelt)_conveyors[tile]).Connect(dir);
        }

        // reorient neighbors
            foreach (Vector2Int dir in compatibleConnections) if (_conveyors[tile + dir] is DynamicConveyorBelt neighborConveyor) neighborConveyor.Connect(-dir);
    }

    // disconnects the conveyor from all neighbors
    public void DisconnectConveyor(Vector2Int tile)
    {
        // get the neighbors of the conveyor
        List<Vector2Int> neighbors;
        if (_conveyors[tile] is DynamicConveyorBelt)
        {
            DynamicConveyorBelt dynamicConveyor = (DynamicConveyorBelt)_conveyors[tile];

            neighbors = new();
            foreach (Vector2Int dir in dynamicConveyor.connections) neighbors.Add(dir);

            // disconnect the conveyor from its neighbors
            foreach (Vector2Int dir in neighbors) dynamicConveyor.Disconnect(dir);
        }
        else
        {
            (Vector2Int orientation, _) = _tiles[tile];
            neighbors = new List<Vector2Int> { orientation, -orientation };
        }

        // disconnect the neighbors from the conveyor
        foreach (Vector2Int dir in neighbors) if (_conveyors.ContainsKey(tile + dir) && _conveyors[tile + dir] is DynamicConveyorBelt neighborConveyor && neighborConveyor.connections.Contains(-dir)) neighborConveyor.Disconnect(-dir);
    }

    public List<ConveyedResource> GetTileResources(Vector2Int tile)
    {
        if (!_conveyors.ContainsKey(tile)) return null;

        if (_conveyors[tile] is DynamicConveyorBelt dynamicConveyor) return dynamicConveyor.resources;
        else if (_conveyors[tile] is SensorConveyorBelt sensorConveyor) return sensorConveyor.resources;
        else if (_conveyors[tile] is ActuatorConveyorBelt actuatorConveyor) return actuatorConveyor.resources;
        else return null;
    }

    public Vector2Int GetNextConveyorExitOrientation(Vector2Int tile, ConveyedResource resource)
    {
        if (!_conveyors.ContainsKey(tile)) return Vector2Int.zero;

        if (_conveyors[tile] is DynamicConveyorBelt dynamicConveyor) return dynamicConveyor.exitOrientation;
        else if (_conveyors[tile] is SensorConveyorBelt sensorConveyor) return sensorConveyor.GetNextExitOrientation(resource);
        else if (_conveyors[tile] is ActuatorConveyorBelt actuatorConveyor) return actuatorConveyor.GetNextExitOrientation(resource);
        else return Vector2Int.zero;
    }

    public List<Vector2Int> GetConveyorExitOrientations(Vector2Int tile)
    {
        if (!_conveyors.ContainsKey(tile)) return null;

        if (_conveyors[tile] is DynamicConveyorBelt dynamicConveyor) return new List<Vector2Int>(1) { dynamicConveyor.exitOrientation };
        else if (_conveyors[tile] is SensorConveyorBelt sensorConveyor) return sensorConveyor.GetExitOrientations();
        else if (_conveyors[tile] is ActuatorConveyorBelt actuatorConveyor) return actuatorConveyor.GetExitOrientations();
        else return null;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////// UI /////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////

    public bool IsFocused()
    {
        return _isFocused;
    }

    public void FocusStructure(GameObject structure, bool structureUI = true, bool portUI = true, List<Port> excludePorts = null, bool buildingUI = true)
    {
        if (_isFocused) Unfocus();
        _isFocused = true;
        _focusedStructure = structure;
        Vector3 focusPosition;
        if (_focusedStructure.GetComponentInChildren<StructureUI>())
        {
            focusPosition = _focusedStructure.GetComponentInChildren<StructureUI>().focusPoint.position;
        }
        else
        {
            Vector2Int orientation = GetTileOrientation(structure.GetComponent<Structure>().tile);
            Vector3 relativeUp = new Vector3(orientation.x, 0, orientation.y);
            Vector3 relativeRight = new Vector3(orientation.y, 0, -orientation.x);
            int size = structure.GetComponent<Structure>().size;
            focusPosition = structure.transform.position + size / 2f * (relativeRight + relativeUp);
        }

        if (structureUI)
        {
            _focusedStructure.GetComponent<StructureUI>().Focus();
        }
        if (portUI)
        {
            HighlightDisconnectedPorts(focusPosition, excludePorts: excludePorts);
            // TODO: "delete" button for ports that are already connected
        }
        if (buildingUI)
        {
            if (_buildingUI != null) Destroy(_buildingUI);
            Vector3 cameraDirection = Camera.main.transform.forward;
            Vector3 position = focusPosition - cameraDirection * 0.5f + new Vector3(-0.75f, 0, -0.75f);
            Quaternion rotation = Quaternion.LookRotation(-cameraDirection, Vector3.up);
            _buildingUI = Instantiate(buildingUIPrefab, position, rotation);
            _buildingUI.GetComponent<BuildingUI>().structure = structure;

            // show rail extenders if the structure is connectable
            Vector2Int tile = Vector3ToTile(focusPosition);
            if (_rails.ContainsKey(tile))
            {
                if (_rails[tile] is DynamicRail dynamicRail)
                {
                    List<Vector2Int> orientations = new List<Vector2Int> { Vector2Int.down, Vector2Int.right, Vector2Int.up, Vector2Int.left };
                    foreach (Vector2Int orientation in orientations)
                    {
                        if (!_tiles.ContainsKey(tile + orientation) && dynamicRail.CanConnect(orientation))
                        {
                            ShowConnectableExtender(tile, orientation, connectableRailPrefab);
                        }
                    }
                }
                else
                {
                    List<Vector2Int> orientations = GetTrainOrientations(tile);
                    foreach (Vector2Int orientation in orientations)
                    {
                        if (!_tiles.ContainsKey(tile - orientation))
                        {
                            ShowConnectableExtender(tile, -orientation, connectableRailPrefab);
                        }
                    }
                }
            }
            else if (_conveyors.ContainsKey(tile))
            {
                if (_conveyors[tile] is DynamicConveyorBelt dynamicConveyor)
                {
                    List<Vector2Int> orientations = new List<Vector2Int> { Vector2Int.down, Vector2Int.right, Vector2Int.up, Vector2Int.left };
                    foreach (Vector2Int orientation in orientations)
                    {
                        if (!_tiles.ContainsKey(tile + orientation) && dynamicConveyor.CanConnect(orientation))
                        {
                            ShowConnectableExtender(tile, orientation, connectableConveyorPrefab, orientation == -GetTileOrientation(tile));
                        }
                    }
                }
                else
                {
                    List<Vector2Int> orientations = GetConveyorExitOrientations(tile);
                    orientations.Add(-GetTileOrientation(tile));
                    foreach (Vector2Int orientation in orientations)
                    {
                        if (!_tiles.ContainsKey(tile + orientation))
                        {
                            ShowConnectableExtender(tile, orientation, connectableConveyorPrefab, orientation == -GetTileOrientation(tile));
                        }
                    }
                }
            }
        }
    }

    public void HighlightDisconnectedPorts(Vector3 center, float radius = 0, List<Port> excludePorts = null)
    {
        // go through all the tiles in a 2*radius square around the center and highlight the ports
        for (int x = -Mathf.FloorToInt(radius); x <= Mathf.FloorToInt(radius); x++)
        {
            for (int y = -Mathf.FloorToInt(radius); y <= Mathf.FloorToInt(radius); y++)
            {
                Vector2Int tile = new Vector2Int(x, y) + Vector3ToTile(center);
                if (!_tiles.ContainsKey(tile)) continue;

                (Vector2Int _, Structure structure) = _tiles[tile];
                if (structure is Sensor sensor)
                {
                    if (excludePorts != null && excludePorts.Contains(sensor.outputPort)) continue;
                    HighlightDisconnectedPort(sensor.outputPort);
                }
                else if (structure is Processor processor)
                {
                    foreach (Port port in processor.inputPorts)
                    {
                        if (excludePorts != null && excludePorts.Contains(port)) continue;
                        HighlightDisconnectedPort(port);
                    }
                    if (excludePorts != null && excludePorts.Contains(processor.outputPort)) continue;
                    HighlightDisconnectedPort(processor.outputPort);
                }
                else if (structure is Actuator actuator)
                {
                    foreach (Port port in actuator.inputPorts)
                    {
                        if (excludePorts != null && excludePorts.Contains(port)) continue;
                        HighlightDisconnectedPort(port);
                    }
                }
                else if (structure is SplitterPort splitterPort)
                {
                    if (excludePorts != null && excludePorts.Contains(splitterPort.port)) continue;
                    HighlightPort(splitterPort.port);
                }
            }
        }
    }

    public void HighlightDisconnectedPort(Port port)
    {
        if (port.isConnected) return;
        HighlightPort(port);
    }

    public void HighlightPort(Port port)
    {
        Vector3 cameraDirection = Camera.main.transform.forward;
        Vector3 position = port.transform.position - cameraDirection * 0.5f;
        Quaternion rotation = Quaternion.LookRotation(-cameraDirection, Vector3.up);
        GameObject portUI = Instantiate(portUIPrefab, position, rotation);
        portUI.GetComponent<PortUI>().port = port;
        _highlightedPorts.Add(portUI);
    }

    public void UnhighlightDisconnectedPorts(List<Port> excludePorts = null)
    {
        List<GameObject> portsToKeep = new();
        foreach (GameObject portUI in _highlightedPorts)
        {
            if (excludePorts != null && excludePorts.Contains(portUI.GetComponent<PortUI>().port)) portsToKeep.Add(portUI);
            else Destroy(portUI);
        }
        _highlightedPorts = portsToKeep;
    }

    public void ShowConnectableExtender(Vector2Int tile, Vector2Int orientation, GameObject connectablePrefab, bool isReversed = false)
    {
        tile += orientation;
        Vector3 position = new Vector3(tile.x, 0, tile.y);
        Quaternion rotation = Quaternion.LookRotation(new Vector3(orientation.x, 0, orientation.y), Vector3.up);
        ConnectableExtender extender = Instantiate(ConnectableExtenderPrefab, position, rotation).GetComponent<ConnectableExtender>();
        extender.connectablePrefab = connectablePrefab;
        extender.tile = tile;
        extender.orientation = isReversed ? -orientation : orientation;
        _railExtenders.Add(extender.gameObject);
    }

    public void FocusTrain(GameObject train)
    {
        if (_isFocused) Unfocus();
        _isFocused = true;
        _focusedTrain = train;
        _focusedTrain.GetComponent<TrainUI>().Focus();
    }

    public void Unfocus(bool structureUI = true, bool portUI = true, List<Port> excludePorts = null, bool buildingUI = true, bool trainUI = true)
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

            foreach (GameObject railExtender in _railExtenders) Destroy(railExtender);
            _railExtenders.Clear();
        }
        if (trainUI && _focusedTrain != null)
        {
            _focusedTrain.GetComponent<TrainUI>().Unfocus();
            _focusedTrain = null;
        }
    }

    public void UnfocusAll()
    {
        Unfocus();
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////// Trains /////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////

    public bool CanBuildTrain(Vector2Int tile)
    {
        if (!_rails.ContainsKey(tile)) return false;

        if (_rails[tile] is DynamicRail dynamicRail) return dynamicRail.trains.Count == 0;
        if (_rails[tile] is SensorRail sensorRail) return sensorRail.trains.Count == 0;
        if (_rails[tile] is ActuatorRail actuatorRail) return actuatorRail.trains.Count == 0;

        return false;
    }

    public bool BuildTrain(Vector2Int tile)
    {
        if (!_rails.ContainsKey(tile)) return false;
        if (!(_rails[tile] is TrainStop trainStop) || trainStop.trains.Count > 0) return false;

        Train train = Instantiate(trainPrefab, new Vector3(tile.x, 0, tile.y), Quaternion.identity).GetComponent<Train>();
        train.Initialize(tile, _tiles[tile].orientation);
        _trains.Add(train);
        return true;
    }

    public void DestroyTrain(Train train)
    {
        _trains.Remove(train);
        train.DestroyTrain();
    }

    public void TrainEnterTile(Train train, Vector2Int tile)
    {
        if (_rails[tile] is DynamicRail dynamicRail) dynamicRail.TrainEnter(train);
        else if (_rails[tile] is SensorRail sensorRail) sensorRail.TrainEnter(train);
        else if (_rails[tile] is ActuatorRail actuatorRail) actuatorRail.TrainEnter(train);
    }

    public void TrainExitTile(Train train, Vector2Int tile)
    {
        if (_rails[tile] is DynamicRail dynamicRail) dynamicRail.TrainExit(train);
        else if (_rails[tile] is SensorRail sensorRail) sensorRail.TrainExit(train);
        else if (_rails[tile] is ActuatorRail actuatorRail) actuatorRail.TrainExit(train);
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////
    //////////////////////////////////////////// Conveyors //////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////

    public void ResourceEnterTile(ConveyedResource resource, Vector2Int tile)
    {
        if (_conveyors[tile] is DynamicConveyorBelt dynamicConveyor) dynamicConveyor.ResourceEnter(resource);
        else if (_conveyors[tile] is SensorConveyorBelt sensorConveyor) sensorConveyor.ResourceEnter(resource);
        else if (_conveyors[tile] is ActuatorConveyorBelt actuatorConveyor) actuatorConveyor.ResourceEnter(resource);
        else throw new Exception("No conveyor found at position: " + tile);
    }

    public void ResourceExitTile(ConveyedResource resource, Vector2Int tile)
    {
        if (_conveyors[tile] is DynamicConveyorBelt dynamicConveyor) dynamicConveyor.ResourceExit(resource);
        else if (_conveyors[tile] is SensorConveyorBelt sensorConveyor) sensorConveyor.ResourceExit(resource);
        else if (_conveyors[tile] is ActuatorConveyorBelt actuatorConveyor) actuatorConveyor.ResourceExit(resource);
        else throw new Exception("No conveyor found at position: " + tile);
    }
}

[CustomEditor(typeof(GameManager))]
public class GameManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        GameManager manager = (GameManager)target;

        // Draw the default inspector
        DrawDefaultInspector();

        // Custom display for materials array
        EditorGUILayout.Space();
        if (manager.materials != null && manager.materials.Length > 0)
        {
            EditorGUILayout.LabelField("Materials", EditorStyles.boldLabel);
            for (int i = 0; i < manager.materials.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                manager.materials[i] = EditorGUILayout.IntField(((Materials)i).ToString(), manager.materials[i]);
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
