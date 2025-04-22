using System.Collections.Generic;
using UnityEngine;
using Signals;
using Newtonsoft.Json;
using Inventories;
using System.Data;
using System;
using NUnit.Framework;

namespace Structures
{
    public class Structure : MonoBehaviour, ISavable
    {
        public int size = 1; // structure extends in +x and +y direction
        [HideInInspector] public Vector2Int tile = Vector2Int.zero; // the tile the structure is on, if size > 1, this is the bottom left tile relative to the orientation
        [HideInInspector] public Vector2Int orientation = Vector2Int.up; // the direction of the structure
        [HideInInspector] public GameObject prefab; // must be set by the instantiator

        private int _id = -1;
        public int ID
        {
            get
            {
                if (_id == -1) _id = SaveManager.Instance.GenerateUniqueId();
                return _id;
            }
            set => _id = value;
        }

        public virtual string TypeName => GetType().ToString();

        public bool ShouldInstantiateOnLoad() => true;

        public virtual string GetStateJson()
        {
            return JsonConvert.SerializeObject((
                size,
                (tile.x, tile.y),
                (orientation.x, orientation.y)
            ));
        }

        public virtual void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
        {
            var state = JsonConvert.DeserializeObject<(int, (int, int), (int, int))>(stateJson);
            size = state.Item1;
            tile = new Vector2Int(state.Item2.Item1, state.Item2.Item2);
            orientation = new Vector2Int(state.Item3.Item1, state.Item3.Item2);
        }

        public static (Vector2Int newTile, Vector2Int newOrientation) RotateClockwise(Vector2Int tile, Vector2Int orientation, int size)
        {
            // newTile should be the bottom left tile of the structure relative to the new orientation
            Vector2Int newTile = tile + (size - 1) * orientation;
            Vector2Int newOrientation = new Vector2Int(orientation.y, -orientation.x);
            return (newTile, newOrientation);
        }
    }

    public class Sensor : Structure
    {
        public PortNetworkGraph network { get; private set; }
        [SerializeField] public Port outputPort;

        public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
        {
            base.RestoreStateJson(stateJson, idLookup);
            network = GameManager.Instance.signalNetworkGraph;
        }

        public virtual void Initialize(PortNetworkGraph signalNetworkGraph)
        {
            network = signalNetworkGraph;
            outputPort.AddToNetwork(network);
        }

        public void Read()
        {
            outputPort.Write(ReadSensor());
        }

        protected virtual float ReadSensor() { return 0; }
    }

    public class Processor : Structure
    {
        public PortNetworkGraph network { get; private set; }
        [SerializeField] public Port[] inputPorts;
        [SerializeField] public Port outputPort;
        public Queue<float> _outputQueue { get; private set; }
        public float _processedSignal { get; private set; }
        public Processor _chainedInputProcessor { get; private set; }
        public Processor _chainedOutputProcessor { get; private set; }

        public override string GetStateJson()
        {
            CombinedState combinedState = new()
            {
                baseState = base.GetStateJson(),
                inheritedState = JsonConvert.SerializeObject((
                    _outputQueue.ToArray(),
                    _processedSignal,
                    _chainedInputProcessor?.ID ?? -1,
                    _chainedOutputProcessor?.ID ?? -1
                ))
            };
            return JsonConvert.SerializeObject(combinedState);
        }

        public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
        {
            var combinedState = JsonConvert.DeserializeObject<CombinedState>(stateJson);
            base.RestoreStateJson(combinedState.baseState, idLookup);
            var state = JsonConvert.DeserializeObject<(
                float[],
                float,
                int,
                int
            )>(combinedState.inheritedState);

            _outputQueue = new Queue<float>(state.Item1);
            _processedSignal = state.Item2;
            _chainedInputProcessor = state.Item3 == -1 ? null : (Processor)idLookup[state.Item3];
            _chainedOutputProcessor = state.Item4 == -1 ? null : (Processor)idLookup[state.Item4];
        }

        public void Initialize(PortNetworkGraph signalNetworkGraph, int delayTicks = 1)
        {
            network = signalNetworkGraph;
            foreach (Port inputPort in inputPorts) inputPort.AddToNetwork(network);
            outputPort.AddToNetwork(network);
            SetOutputDelay(delayTicks);
        }

        public void SetOutputDelay(int delayTicks)
        {
            if (delayTicks < 1)
            {
                Debug.LogError("Invalid delay ticks: " + delayTicks);
                return;
            }

            _outputQueue = new Queue<float>(delayTicks);
            for (int i = 0; i < delayTicks; i++) _outputQueue.Enqueue(0);
        }

        public void Chain(Processor inputProcessor)
        {
            if (IsInputChained()) throw new System.Exception("Input already chained.");

            _chainedInputProcessor = inputProcessor;
            inputProcessor._chainedOutputProcessor = this;

            inputPorts[0].RemoveFromNetwork();
            inputProcessor.outputPort.RemoveFromNetwork();
            Channel signalChannel = new();
            inputPorts[0].signalChannel = signalChannel;
            inputProcessor.outputPort.signalChannel = signalChannel;
        }

        public void Unchain()
        {
            if (!IsInputChained()) throw new System.Exception("Input not chained.");

            _chainedInputProcessor.outputPort.AddToNetwork(network);
            inputPorts[0].AddToNetwork(network);

            _chainedInputProcessor._chainedOutputProcessor = null;
            _chainedInputProcessor = null;
        }

        public void UnchainOutput()
        {
            if (!IsOutputChained()) throw new System.Exception("Output not chained.");
            _chainedOutputProcessor.Unchain();
        }

        public bool IsInputChained()
        {
            return _chainedInputProcessor != null;
        }

        public bool IsOutputChained()
        {
            return _chainedOutputProcessor != null;
        }

        public void Process()
        {
            float[] signals = new float[inputPorts.Length];
            for (int i = 0; i < inputPorts.Length; i++) signals[i] = inputPorts[i].Read();
            _processedSignal = ProcessSignal(signals);

            // send processed signal to chained output processor in the same tick and process the next processor in the chain
            if (IsOutputChained())
            {
                outputPort.Reset();
                outputPort.Write(_processedSignal);
                _chainedOutputProcessor.Process();
            }
        }

        protected virtual float ProcessSignal(float[] inputSignals) { Debug.LogError("Signal processor not implemented."); return 0; }

        public void AdvanceOutputQueue()
        {
            if (IsOutputChained()) return; // if chained, the output queue is skipped and the signal is sent directly to the chained output processor

            outputPort.Write(_outputQueue.Dequeue());
            _outputQueue.Enqueue(_processedSignal);
        }
    }

    public class Actuator : Structure
    {
        public PortNetworkGraph network { get; private set; }
        [SerializeField] public Port[] inputPorts;

        public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
        {
            base.RestoreStateJson(stateJson, idLookup);
            network = GameManager.Instance.signalNetworkGraph;
        }

        virtual public void Initialize(PortNetworkGraph signalNetworkGraph)
        {
            network = signalNetworkGraph;
            foreach (Port inputPort in inputPorts) inputPort.AddToNetwork(network);
        }

        public void Write()
        {
            float[] signals = new float[inputPorts.Length];
            for (int i = 0; i < inputPorts.Length; i++) signals[i] = inputPorts[i].Read();
            WriteActuator(signals);
        }

        protected virtual void WriteActuator(float[] inputSignals) { }
    }

    public class Splitter : Structure
    {
        public PortNetworkGraph network { get; private set; }
        [SerializeField] public Port port;

        public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
        {
            base.RestoreStateJson(stateJson, idLookup);
            network = GameManager.Instance.signalNetworkGraph;
        }

        public void Initialize(PortNetworkGraph signalNetworkGraph)
        {
            network = signalNetworkGraph;
            port.AddToNetwork(network);
        }
    }

    // base structure class for all structures that can connect with nearby structures
    public class ConnectableStructure : Structure
    {
        public List<Vector2Int> connections { get; private set; } = new List<Vector2Int>(2); // connections to other structures by direction

        public override string GetStateJson()
        {
            CombinedState combinedState = new()
            {
                baseState = base.GetStateJson(),
                inheritedState = JsonConvert.SerializeObject(connections.ToArray())
            };
            return JsonConvert.SerializeObject(combinedState);
        }

        public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
        {
            var combinedState = JsonConvert.DeserializeObject<CombinedState>(stateJson);
            base.RestoreStateJson(combinedState.baseState, idLookup);
            var state = JsonConvert.DeserializeObject<Vector2Int[]>(combinedState.inheritedState);

            connections = new List<Vector2Int>(state);
        }

        public virtual bool CanConnect(Vector2Int dir)
        {
            return connections.Count < 2 || connections.Contains(dir);
        }

        public void Connect(Vector2Int dir)
        {
            connections.Add(dir);
            OnConnect(dir);
        }

        public void Disconnect(Vector2Int dir)
        {
            connections.Remove(dir);
            OnDisconnect(dir);
        }

        public virtual void OnConnect(Vector2Int dir) { }
        public virtual void OnDisconnect(Vector2Int dir) { }
    }

    public class DynamicRail : ConnectableStructure
    {
        public List<Vector2Int> trainOrientations { get; private set; } = new List<Vector2Int>(2) { Vector2Int.up, Vector2Int.down };
        public List<Train> trains { get; private set; } = new List<Train>();

        public override string GetStateJson()
        {
            CombinedState combinedState = new()
            {
                baseState = base.GetStateJson(),
                inheritedState = JsonConvert.SerializeObject((
                    trainOrientations.ToArray(),
                    trains.ConvertAll(t => t.ID).ToArray()
                ))
            };
            return JsonConvert.SerializeObject(combinedState);
        }

        public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
        {
            var combinedState = JsonConvert.DeserializeObject<CombinedState>(stateJson);
            base.RestoreStateJson(combinedState.baseState, idLookup);
            var state = JsonConvert.DeserializeObject<(
                Vector2Int[],
                int[]
            )>(combinedState.inheritedState);

            trainOrientations = new List<Vector2Int>(state.Item1);
            trains = new List<Train>(state.Item2.Length);
            foreach (int trainId in state.Item2) trains.Add((Train)idLookup[trainId]);

            OnOrientRail();
        }

        public override void OnConnect(Vector2Int dir)
        {
            if (connections.Count == 1)
            {
                trainOrientations[0] = -dir;
                trainOrientations[1] = dir;
            }
            else if (connections.Count == 2)
            {
                trainOrientations[1] = -dir;
            }

            OnOrientRail();
        }

        public override void OnDisconnect(Vector2Int dir)
        {
            if (connections.Count == 1)
            {
                trainOrientations[0] = -connections[0];
                trainOrientations[1] = connections[0];
            }
            else if (connections.Count == 0)
            {
                trainOrientations[0] = Vector2Int.up;
                trainOrientations[1] = Vector2Int.down;
            }

            OnOrientRail();
        }

        public bool TrainEnter(Train train)
        {
            trains.Add(train);
            OnTrainEnter(train);
            return true;
        }

        public void TrainExit(Train train)
        {
            OnTrainExit(train);
            trains.Remove(train);
        }

        public virtual void OnOrientRail() { }
        protected virtual void OnTrainEnter(Train train) { }
        protected virtual void OnTrainExit(Train train) { }
    }

    public class SensorRail : Sensor
    {
        public List<Train> trains { get; private set; } = new List<Train>();

        public override string GetStateJson()
        {
            CombinedState combinedState = new()
            {
                baseState = base.GetStateJson(),
                inheritedState = JsonConvert.SerializeObject(trains.ConvertAll(t => t.ID).ToArray())
            };
            return JsonConvert.SerializeObject(combinedState);
        }

        public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
        {
            var combinedState = JsonConvert.DeserializeObject<CombinedState>(stateJson);
            base.RestoreStateJson(combinedState.baseState, idLookup);
            var state = JsonConvert.DeserializeObject<int[]>(combinedState.inheritedState);

            trains = new List<Train>(state.Length);
            foreach (int trainId in state) trains.Add((Train)idLookup[trainId]);
        }

        public virtual Vector2Int GetNextTrainOrientation(Vector2Int orientation)
        {
            List<Vector2Int> orientations = GetTrainOrientations();
            foreach (Vector2Int o in orientations) if (o != orientation) return -o;
            return Vector2Int.zero;
        }

        public virtual List<Vector2Int> GetTrainOrientations()
        {
            return new List<Vector2Int>(2) { orientation, -orientation };
        }

        public bool TrainEnter(Train train)
        {
            if (trains.Contains(train)) return false;
            trains.Add(train);
            OnTrainEnter(train);
            return true;
        }

        public void TrainExit(Train train)
        {
            OnTrainExit(train);
            trains.Remove(train);
        }

        protected virtual void OnTrainEnter(Train train) { }
        protected virtual void OnTrainExit(Train train) { }
    }

    public class ActuatorRail : Actuator
    {
        public List<Train> trains { get; private set; } = new List<Train>();

        public override string GetStateJson()
        {
            CombinedState combinedState = new()
            {
                baseState = base.GetStateJson(),
                inheritedState = JsonConvert.SerializeObject(trains.ConvertAll(t => t.ID).ToArray())
            };
            return JsonConvert.SerializeObject(combinedState);
        }

        public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
        {
            var combinedState = JsonConvert.DeserializeObject<CombinedState>(stateJson);
            base.RestoreStateJson(combinedState.baseState, idLookup);
            var state = JsonConvert.DeserializeObject<int[]>(combinedState.inheritedState);

            trains = new List<Train>(state.Length);
            foreach (int trainId in state) trains.Add((Train)idLookup[trainId]);
        }

        public virtual Vector2Int GetNextTrainOrientation(Vector2Int orientation)
        {
            List<Vector2Int> orientations = GetTrainOrientations();
            foreach (Vector2Int o in orientations) if (o != orientation) return -o;
            return Vector2Int.zero;
        }

        public virtual List<Vector2Int> GetTrainOrientations()
        {
            return new List<Vector2Int>(2) { orientation, -orientation };
        }

        public bool TrainEnter(Train train)
        {
            if (trains.Contains(train)) return false;
            trains.Add(train);
            OnTrainEnter(train);
            return true;
        }

        public void TrainExit(Train train)
        {
            OnTrainExit(train);
            trains.Remove(train);
        }

        protected virtual void OnTrainEnter(Train train) { }
        protected virtual void OnTrainExit(Train train) { }
    }

    public class DynamicConveyorBelt : ConnectableStructure
    {
        public float speed = 1f;
        public Vector2Int exitOrientation;
        public List<ConveyedResource> resources { get; private set; } = new List<ConveyedResource>(2);

        public override string GetStateJson()
        {
            CombinedState combinedState = new()
            {
                baseState = base.GetStateJson(),
                inheritedState = JsonConvert.SerializeObject((
                    speed,
                    (exitOrientation.x, exitOrientation.y),
                    resources.ConvertAll(r => r.ID).ToArray()
                ))
            };
            return JsonConvert.SerializeObject(combinedState);
        }

        public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
        {
            var combinedState = JsonConvert.DeserializeObject<CombinedState>(stateJson);
            base.RestoreStateJson(combinedState.baseState, idLookup);
            var state = JsonConvert.DeserializeObject<(
                float,
                (int, int),
                int[]
            )>(combinedState.inheritedState);

            speed = state.Item1;
            exitOrientation = new Vector2Int(state.Item2.Item1, state.Item2.Item2);
            resources = new List<ConveyedResource>(state.Item3.Length);
            foreach (int resourceId in state.Item3) resources.Add((ConveyedResource)idLookup[resourceId]);

            OnOrientConveyorBelt();
        }

        public void Update()
        {
            Vector2Int nextTile = tile + exitOrientation;
            List<ConveyedResource> nextTileResources = GameManager.Instance.GetTileResources(nextTile);
            for (int i = resources.Count - 1; i >= 0; i--) resources[i].Convey(speed, resources, nextTileResources);
        }

        public override bool CanConnect(Vector2Int dir)
        {
            if (connections.Contains(dir)) return true;
            if (connections.Count == 0) return true;

            if (connections.Count == 1)
            {
                if (connections[0] == exitOrientation && dir == -orientation) return true;
                else if (connections[0] != exitOrientation && dir != -orientation) return true;
                else return false;
            }

            return false;
        }

        public override void OnConnect(Vector2Int dir)
        {
            // update exit orientation
            if (orientation != -dir) exitOrientation = dir;
            else if (connections.Count == 1) exitOrientation = orientation;

            // update resources interpolation
            foreach (ConveyedResource resource in resources) resource.NewPath(orientation, exitOrientation);

            OnOrientConveyorBelt();
        }

        public void ResourceEnter(ConveyedResource resource)
        {
            if (resources.Contains(resource)) throw new Exception("Resource already on conveyor belt.");
            resources.Add(resource);
        }

        public void ResourceExit(ConveyedResource resource)
        {
            if (!resources.Contains(resource)) throw new Exception("Resource not on conveyor belt.");
            resources.Remove(resource);
        }

        public virtual void OnOrientConveyorBelt() { }
    }

    public class SensorConveyorBelt : Sensor
    {
        public float speed = 1f;
        public List<ConveyedResource> resources { get; private set; } = new List<ConveyedResource>(2);

        public override string GetStateJson()
        {
            CombinedState combinedState = new()
            {
                baseState = base.GetStateJson(),
                inheritedState = JsonConvert.SerializeObject((
                    speed,
                    resources.ConvertAll(r => r.ID).ToArray()
                ))
            };
            return JsonConvert.SerializeObject(combinedState);
        }

        public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
        {
            var combinedState = JsonConvert.DeserializeObject<CombinedState>(stateJson);
            base.RestoreStateJson(combinedState.baseState, idLookup);
            var state = JsonConvert.DeserializeObject<(
                float,
                int[]
            )>(combinedState.inheritedState);

            speed = state.Item1;
            resources = new List<ConveyedResource>(state.Item2.Length);
            foreach (int resourceId in state.Item2) resources.Add((ConveyedResource)idLookup[resourceId]);
        }

        public void Update()
        {
            for (int i = resources.Count - 1; i >= 0; i--)
            {
                Vector2Int nextTile = tile + GetNextExitOrientation(resources[i]);
                List<ConveyedResource> nextTileResources = GameManager.Instance.GetTileResources(nextTile);
                resources[i].Convey(speed, resources, nextTileResources);
            }
        }

        public virtual Vector2Int GetNextExitOrientation(ConveyedResource resource)
        {
            return orientation;
        }

        public virtual List<Vector2Int> GetExitOrientations()
        {
            return new List<Vector2Int>(1) { orientation };
        }

        public void ResourceEnter(ConveyedResource resource)
        {
            if (resources.Contains(resource)) throw new System.Exception("Resource already on conveyor belt.");
            resources.Add(resource);
            OnResourceEnter(resource);
        }

        public void ResourceExit(ConveyedResource resource)
        {
            if (!resources.Contains(resource)) throw new System.Exception("Resource not on conveyor belt.");
            resources.Remove(resource);
            OnResourceExit(resource);
        }

        public virtual void OnResourceEnter(ConveyedResource resource) { }
        public virtual void OnResourceExit(ConveyedResource resource) { }
    }

    public class ActuatorConveyorBelt : Actuator
    {
        public float speed = 1f;
        public List<ConveyedResource> resources { get; private set; } = new List<ConveyedResource>(2);

        public override string GetStateJson()
        {
            CombinedState combinedState = new()
            {
                baseState = base.GetStateJson(),
                inheritedState = JsonConvert.SerializeObject((
                    speed,
                    resources.ConvertAll(r => r.ID).ToArray()
                ))
            };
            return JsonConvert.SerializeObject(combinedState);
        }

        public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
        {
            var combinedState = JsonConvert.DeserializeObject<CombinedState>(stateJson);
            base.RestoreStateJson(combinedState.baseState, idLookup);
            var state = JsonConvert.DeserializeObject<(
                float,
                int[]
            )>(combinedState.inheritedState);

            speed = state.Item1;
            resources = new List<ConveyedResource>(state.Item2.Length);
            foreach (int resourceId in state.Item2) resources.Add((ConveyedResource)idLookup[resourceId]);
        }

        public void Update()
        {
            for (int i = resources.Count - 1; i >= 0; i--)
            {
                Vector2Int nextTile = tile + GetNextExitOrientation(resources[i]);
                List<ConveyedResource> nextTileResources = GameManager.Instance.GetTileResources(nextTile);
                resources[i].Convey(speed, resources, nextTileResources);
            }
        }

        public virtual Vector2Int GetNextExitOrientation(ConveyedResource resource)
        {
            return orientation;
        }

        public virtual List<Vector2Int> GetExitOrientations()
        {
            return new List<Vector2Int>(1) { orientation };
        }

        public void ResourceEnter(ConveyedResource resource)
        {
            if (resources.Contains(resource)) throw new System.Exception("Resource already on conveyor belt.");
            resources.Add(resource);
            OnResourceEnter(resource);
        }

        public void ResourceExit(ConveyedResource resource)
        {
            if (!resources.Contains(resource)) throw new System.Exception("Resource not on conveyor belt.");
            resources.Remove(resource);
            OnResourceExit(resource);
        }

        public virtual void OnResourceEnter(ConveyedResource resource) { }
        public virtual void OnResourceExit(ConveyedResource resource) { }
    }

    public class Machine : Actuator
    {
        [SerializeField] private GameObject[] inputFunnels; // transform.position should round to the conveyor belt it is connected to
        [SerializeField] private GameObject[] outputFunnels; // transform.position should round to the conveyor belt it is connected to
        [SerializeField] private ulong funnelSpeedInTicks = 2;
        [SerializeField] protected int[] numberOfInputs = { 1 };
        [SerializeField] protected int[] numberOfOutputs = { 1 };

        public bool isProcessing = false;
        public ConveyedResource[][] inputResources;
        public ConveyedResource[][] outputResources;

        public virtual void Awake()
        {
            inputResources = new ConveyedResource[numberOfInputs.Length][];
            outputResources = new ConveyedResource[numberOfOutputs.Length][];
            for (int i = 0; i < numberOfInputs.Length; i++) inputResources[i] = new ConveyedResource[numberOfInputs[i]];
            for (int i = 0; i < numberOfOutputs.Length; i++) outputResources[i] = new ConveyedResource[numberOfOutputs[i]];
            if (inputFunnels.Length != numberOfInputs.Length) throw new Exception("Number of input funnels does not match number of input channels.");
            if (outputFunnels.Length != numberOfOutputs.Length) throw new Exception("Number of output funnels does not match number of output channels.");
        }

        public override string GetStateJson()
        {
            CombinedState combinedState = new()
            {
                baseState = base.GetStateJson(),
                inheritedState = JsonConvert.SerializeObject((
                    isProcessing,
                    Array.ConvertAll(inputResources, arr => Array.ConvertAll(arr, r => r == null ? -1 : r.ID)),
                    Array.ConvertAll(outputResources, arr => Array.ConvertAll(arr, r => r == null ? -1 : r.ID))
                ))
            };
            return JsonConvert.SerializeObject(combinedState);
        }

        public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
        {
            var combinedState = JsonConvert.DeserializeObject<CombinedState>(stateJson);
            base.RestoreStateJson(combinedState.baseState, idLookup);
            var state = JsonConvert.DeserializeObject<(
                bool,
                int[][],
                int[][]
            )>(combinedState.inheritedState);

            isProcessing = state.Item1;
            for (int i = 0; i < inputResources.Length; i++)
            {
                inputResources[i] = new ConveyedResource[state.Item2[i].Length];
                for (int j = 0; j < state.Item2[i].Length; j++)
                    if (state.Item2[i][j] != -1) inputResources[i][j] = (ConveyedResource)idLookup[state.Item2[i][j]];
            }
            for (int i = 0; i < outputResources.Length; i++)
            {
                outputResources[i] = new ConveyedResource[state.Item3[i].Length];
                for (int j = 0; j < state.Item3[i].Length; j++)
                    if (state.Item3[i][j] != -1) outputResources[i][j] = (ConveyedResource)idLookup[state.Item3[i][j]];
            }
        }

        protected override void WriteActuator(float[] inputSignals)
        {
            isProcessing = inputSignals[0] > 0;
        }

        public void Process()
        {
            if (GameManager.Instance.tick % funnelSpeedInTicks == 0) OutputToConveyorBelts();
            if (isProcessing) ProcessMachine();
            if (GameManager.Instance.tick % funnelSpeedInTicks == 0) InputFromConveyorBelts();
        }

        public int TryOutputResources(int channel, List<ConveyedResource> outputResources) // modifies the outputResources list
        {
            int successCount = 0;
            for (int i = 0; i < numberOfOutputs[channel]; i++)
            {
                if (outputResources.Count == 0) break;
                if (this.outputResources[channel][i] != null) continue;
                this.outputResources[channel][i] = outputResources[0];
                outputResources.RemoveAt(0);
                successCount++;
            }
            return successCount;
        }

        public void InputFromConveyorBelts()
        {
            for (int channel = 0; channel < inputFunnels.Length; channel++)
            {
                for (int i = 0; i < numberOfInputs[channel]; i++)
                {
                    if (inputResources[channel][i] != null) continue;

                    Vector2Int funnelTile = GameManager.Vector3ToTile(inputFunnels[channel].transform.position);
                    List<ConveyedResource> funnelResources = GameManager.Instance.GetTileResources(funnelTile);
                    if (funnelResources == null)
                    {
                        // no conveyor belt under the funnel, disable it
                        inputFunnels[channel].SetActive(false);
                        break;
                    }
                    else
                    {
                        // conveyor belt under the funnel, enable it
                        inputFunnels[channel].SetActive(true);
                    }
                    if (funnelResources.Count == 0) break;
                    ConveyedResource resourceToPickup = funnelResources[0]; // FIFO
                    resourceToPickup.ExitConveyPath();
                    inputResources[channel][i] = resourceToPickup;
                    resourceToPickup.EnterInventory();
                    resourceToPickup.transform.position = transform.position;

                    break; // only pick one resource at a time
                }
            }
        }

        public void OutputToConveyorBelts()
        {
            for (int channel = 0; channel < outputFunnels.Length; channel++)
            {
                for (int i = 0; i < numberOfOutputs[channel]; i++)
                {
                    if (outputResources[channel][i] == null) continue;

                    Vector2Int funnelTile = GameManager.Vector3ToTile(outputFunnels[channel].transform.position);
                    if (GameManager.Instance.GetTileResources(funnelTile) == null)
                    {
                        // no conveyor belt under the funnel, disable it
                        outputFunnels[channel].SetActive(false);
                        break;
                    }
                    else
                    {
                        // conveyor belt under the funnel, enable it
                        outputFunnels[channel].SetActive(true);
                    }
                    if (outputResources[channel][i].TryEnterConveyPath(funnelTile))
                    {
                        outputResources[channel][i].ExitInventory();
                        outputResources[channel][i] = null;
                    }

                    break; // only output one resource at a time
                }
            }
        }

        public void MoveInputResourcesToOutput()
        {
            for (int inputChannel = 0; inputChannel < inputFunnels.Length; inputChannel++)
            {
                for (int i = 0; i < numberOfInputs[inputChannel]; i++)
                {
                    if (inputResources[inputChannel][i] == null) continue;

                    for (int outputChannel = 0; outputChannel < outputFunnels.Length; outputChannel++)
                    {
                        for (int j = 0; j < numberOfOutputs[outputChannel]; j++)
                        {
                            if (outputResources[outputChannel][j] != null) continue;
                            outputResources[outputChannel][j] = inputResources[inputChannel][i];
                            inputResources[inputChannel][i] = null;
                            break;
                        }
                    }
                }
            }
        }

        public virtual void DropInventory()
        {
            foreach (ConveyedResource[] resources in inputResources) foreach (ConveyedResource resource in resources) if (resource != null) resource.ExitInventory();
            foreach (ConveyedResource[] resources in outputResources) foreach (ConveyedResource resource in resources) if (resource != null) resource.ExitInventory();
        }

        public virtual void ProcessMachine() { }
    }
}
