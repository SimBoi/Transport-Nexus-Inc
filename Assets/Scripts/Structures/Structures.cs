using System.Collections.Generic;
using UnityEngine;
using QuikGraph;
using System.Linq;

namespace Structures
{
    public class SignalNetworkGraph
    {
        private UndirectedGraph<Port, TaggedUndirectedEdge<Port, GameObject>> _graph = new();
        private Dictionary<GameObject, TaggedUndirectedEdge<Port, GameObject>> _wires = new();
        private List<SignalChannel> _signalChannels = new();

        public void AddPort(Port port)
        {
            if (!_graph.AddVertex(port)) return;
            port.signalChannel = null;
        }

        public void RemovePort(Port port)
        {
            foreach (TaggedUndirectedEdge<Port, GameObject> edge in _graph.AdjacentEdges(port)) DisconnectWire(edge.Tag);
            if (port.isConnected) _signalChannels.Remove(port.signalChannel);
            _graph.RemoveVertex(port);
        }

        public void ConnectWire(GameObject wire, Port port1, Port port2)
        {
            if (port1.isConnected) AssignSignalChannelBFS(port2, port1.signalChannel);
            else if (port2.isConnected) AssignSignalChannelBFS(port1, port2.signalChannel);
            else
            {
                AssignSignalChannelBFS(port1, new SignalChannel());
                AssignSignalChannelBFS(port2, port1.signalChannel);
            }

            TaggedUndirectedEdge<Port, GameObject> edge;
            if (port1.CompareTo(port2) > 0) edge = new(port2, port1, wire);
            else edge = new(port1, port2, wire);

            if (!_graph.AddEdge(edge)) throw new System.Exception("Failed to add edge.");
            _wires.Add(wire, edge);
        }

        public void DisconnectWire(GameObject wire)
        {
            if (!_wires.ContainsKey(wire)) return;
            TaggedUndirectedEdge<Port, GameObject> edge = _wires[wire];
            Port port1 = edge.Source;
            Port port2 = edge.Target;

            _wires.Remove(wire);
            _graph.RemoveEdge(edge);

            bool port1Connected = _graph.AdjacentEdges(port1).Any();
            bool port2Connected = _graph.AdjacentEdges(port2).Any();
            if (port1Connected && port2Connected)
            {
                AssignSignalChannelBFS(port1, new SignalChannel(), false);
            }
            else if (port1Connected)
            {
                AssignSignalChannelBFS(port2, null, false);
            }
            else if (port2Connected)
            {
                AssignSignalChannelBFS(port1, null, false);
            }
            else
            {
                AssignSignalChannelBFS(port1, null, true);
                AssignSignalChannelBFS(port2, null, false);
            }
        }

        public void AssignSignalChannelBFS(Port startPort, SignalChannel signalChannel, bool removeOldChannel = true)
        {
            if (removeOldChannel) _signalChannels.Remove(startPort.signalChannel); // remove the old signal channel
            if (signalChannel != null) _signalChannels.Add(signalChannel); // add the new signal channel

            // leaf node
            if (!_graph.AdjacentEdges(startPort).Any())
            {
                startPort.signalChannel = signalChannel;
                return;
            }

            Queue<Port> queue = new();
            HashSet<Port> visited = new();
            queue.Enqueue(startPort);
            visited.Add(startPort);

            while (queue.Count > 0)
            {
                Port current = queue.Dequeue();
                current.signalChannel = signalChannel;

                foreach (Port neighbor in _graph.AdjacentVertices(current))
                {
                    if (visited.Contains(neighbor)) continue;
                    queue.Enqueue(neighbor);
                    visited.Add(neighbor);
                }
            }
        }

        public void ResetSignalChannels()
        {
            foreach (SignalChannel signalChannel in _signalChannels) signalChannel.Reset();
        }
    }

    public class SignalChannel
    {
        private float _signal = 0;

        public void Reset()
        {
            _signal = 0;
        }

        public void Write(float signal)
        {
            _signal = Mathf.Max(_signal, signal);
        }

        public float Read()
        {
            return _signal;
        }
    }

    public class Sensor : MonoBehaviour
    {
        [HideInInspector] public GameObject prefab; // must be set by the instantiator
        public SignalNetworkGraph network { get; private set; }
        [SerializeField] public Port outputPort;

        public void Initialize(SignalNetworkGraph signalNetworkGraph)
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

    public class Processor : MonoBehaviour
    {
        [HideInInspector] public GameObject prefab; // must be set by the instantiator
        public SignalNetworkGraph network { get; private set; }
        [SerializeField] public Port[] inputPorts;
        [SerializeField] public Port outputPort;
        public Queue<float> _outputQueue { get; private set; }
        public float _processedSignal { get; private set; }
        public Processor _chainedInputProcessor { get; private set; }
        public Processor _chainedOutputProcessor { get; private set; }

        public void Initialize(SignalNetworkGraph signalNetworkGraph, int delayTicks = 1)
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
            SignalChannel signalChannel = new();
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

    public class Actuator : MonoBehaviour
    {
        [HideInInspector] public GameObject prefab; // must be set by the instantiator
        public SignalNetworkGraph network { get; private set; }
        [SerializeField] public Port[] inputPorts;

        // the initializer should be called in the derived class's Start() method
        public void Initialize(SignalNetworkGraph signalNetworkGraph)
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
}
