using System.Collections.Generic;
using UnityEngine;
using Signals;
using Newtonsoft.Json;

namespace Structures
{
    public class Structure : MonoBehaviour, ISavable
    {
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
        public bool ShouldInstantiateOnLoad() => true;
        public virtual string GetStateJson() { return ""; }
        public virtual void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup) { }
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
            Debug.Log("Processor Initialize");
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

    // base class for rail structures that can be connected to each other along different orientations
    public class DynamicRail : Structure
    {
        public List<Vector2Int> connections { get; private set; } = new List<Vector2Int>(2);
        public List<Vector2Int> trainOrientations { get; private set; } = new List<Vector2Int>(2) { Vector2Int.up, Vector2Int.down };
        public List<Train> trains { get; private set; } = new List<Train>();

        public override string GetStateJson()
        {
            CombinedState combinedState = new()
            {
                baseState = base.GetStateJson(),
                inheritedState = JsonConvert.SerializeObject((
                    connections.ToArray(),
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
                Vector2Int[],
                int[]
            )>(combinedState.inheritedState);

            connections = new List<Vector2Int>(state.Item1);
            trainOrientations = new List<Vector2Int>(state.Item2);
            trains = new List<Train>(state.Item3.Length);
            foreach (int trainId in state.Item3) trains.Add((Train)idLookup[trainId]);

            OnOrientRail();
        }

        public bool CanConnect(Vector2Int dir)
        {
            return connections.Count < 2 || connections.Contains(dir);
        }

        public void Connect(Vector2Int dir)
        {
            connections.Add(dir);
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

        public void Disconnect(Vector2Int dir)
        {
            connections.Remove(dir);
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

        protected virtual void OnOrientRail() { }
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

        public virtual List<Vector2Int> GetCurrentTrainOrientations(Vector2Int tile)
        {
            Vector2Int orientation = GameManager.Instance.GetTileOrientation(tile);
            return new List<Vector2Int>(2) { orientation, -orientation };
        }

        public virtual List<Vector2Int> GetAllTrainOrientations(Vector2Int tile)
        {
            return GetCurrentTrainOrientations(tile);
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

        public virtual List<Vector2Int> GetCurrentTrainOrientations(Vector2Int tile)
        {
            Vector2Int orientation = GameManager.Instance.GetTileOrientation(tile);
            return new List<Vector2Int>(2) { orientation, -orientation };
        }

        public virtual List<Vector2Int> GetAllTrainOrientations(Vector2Int tile)
        {
            return GetCurrentTrainOrientations(tile);
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
}
