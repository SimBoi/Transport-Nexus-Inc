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

    // Components should be disabled by default, this method will enable them
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

    public void AddWire(Vector2Int start, Vector2Int end)
    {
        return;
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

    public void RemoveWire(Vector2Int start, Vector2Int end)
    {
        return;
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
}
