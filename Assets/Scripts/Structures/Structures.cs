using System.Collections.Generic;
using UnityEngine;
using Signals;

namespace Structures
{
    public class Structure : MonoBehaviour
    {
        [HideInInspector] public GameObject prefab; // must be set by the instantiator
    }

    public class Sensor : Structure
    {
        public PortNetworkGraph network { get; private set; }
        [SerializeField] public Port outputPort;

        virtual public void Initialize(PortNetworkGraph signalNetworkGraph)
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

    // base class for rail structures that can be connected to each other along different orientations
    public class DynamicRail : Structure
    {
        public List<Vector2Int> connections { get; private set; } = new List<Vector2Int>(2);
        public List<Vector2Int> trainOrientations { get; private set; } = new List<Vector2Int>(2) { Vector2Int.up, Vector2Int.down };
        public List<Train> trains { get; private set; } = new List<Train>();

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
            if (trains.Contains(train)) throw new System.Exception("Train already in rail.");
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
