using System.Collections.Generic;
using UnityEngine;
using QuikGraph;

namespace Components
{
    public class SignalNetworkGraph
    {
        private UndirectedGraph<Port, TaggedUndirectedEdge<Port, GameObject>> _graph = new();
        private Dictionary<GameObject, TaggedUndirectedEdge<Port, GameObject>> _wires = new();
        private List<SignalChannel> _signalChannels = new();

        public void AddPort(Port port)
        {
            if (!_graph.AddVertex(port)) return;
            port.signalChannel = new SignalChannel();
            _signalChannels.Add(port.signalChannel);
        }

        public void RemovePort(Port port)
        {
            foreach (TaggedUndirectedEdge<Port, GameObject> edge in _graph.AdjacentEdges(port)) DisconnectWire(edge.Tag);
            _signalChannels.Remove(port.signalChannel);
            _graph.RemoveVertex(port);
        }

        public void ConnectWire(GameObject wire, Port port1, Port port2)
        {
            TaggedUndirectedEdge<Port, GameObject> edge = new(port1, port2, wire);

            AssignSinalChannelBFS(port2, port1.signalChannel);
            if (!_graph.AddEdge(edge)) throw new System.Exception("Failed to add edge.");
            _wires.Add(wire, edge);
        }

        public void DisconnectWire(GameObject wire)
        {
            if (!_wires.ContainsKey(wire)) return;
            TaggedUndirectedEdge<Port, GameObject> edge = _wires[wire];

            _wires.Remove(wire);
            _graph.RemoveEdge(edge);
            AssignSinalChannelBFS(edge.Target, new SignalChannel(), false);
        }

        public void AssignSinalChannelBFS(Port startPort, SignalChannel signalChannel, bool removeOldChannel = true)
        {
            if (removeOldChannel) _signalChannels.Remove(startPort.signalChannel); // remove the old signal channel
            _signalChannels.Add(signalChannel); // add the new signal channel

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

    public class Port
    {
        public SignalNetworkGraph network;
        public SignalChannel signalChannel;

        // the Port is enabled by default
        public Port(SignalNetworkGraph signalNetworkGraph)
        {
            network = signalNetworkGraph;
            Connect();
        }

        public void Connect()
        {
            network.AddPort(this);
        }

        public void Disconnect()
        {
            network.RemovePort(this);
        }

        public void Write(float signal)
        {
            signalChannel.Write(signal);
        }

        public float Read()
        {
            return signalChannel.Read();
        }

        public void Reset()
        {
            signalChannel.Reset();
        }
    }

    public class Sensor : MonoBehaviour
    {
        public SignalNetworkGraph network;
        public Port outputPort;

        // the initializer should be called in the derived class's Start() method
        public void Initialize(SignalNetworkGraph signalNetworkGraph)
        {
            network = signalNetworkGraph;
            outputPort = new Port(network);
        }

        public void Read()
        {
            outputPort.Write(ReadSensor());
        }

        protected virtual float ReadSensor() { return 0; }
    }

    public class Processor : MonoBehaviour
    {
        public SignalNetworkGraph network;
        public Port[] inputPorts;
        public Port outputPort;
        private Queue<float> _outputQueue;
        private float _processedSignal;
        private Processor _chainedInputProcessor;
        private Processor _chainedOutputProcessor;

        // the initializer should be called in the derived class's Start() method
        public void Initialize(SignalNetworkGraph signalNetworkGraph, int inputChannelCount = 1, int delayTicks = 1)
        {
            network = signalNetworkGraph;
            SetInputChannelCount(inputChannelCount);
            outputPort = new Port(network);
            SetOutputDelay(delayTicks);
        }

        public void SetInputChannelCount(int inputPortsCount)
        {
            if (inputPortsCount < 1)
            {
                Debug.LogError("Invalid input channel count: " + inputPortsCount);
                return;
            }

            if (inputPorts == null)
            {
                // create input channels array
                inputPorts = new Port[inputPortsCount];
            }
            else
            {
                // resize input channels array, keeping existing channels up to the new size
                Port[] newInputChannels = new Port[inputPortsCount];
                for (int i = 0; i < inputPortsCount; i++) newInputChannels[i] = i < inputPorts.Length ? inputPorts[i] : new Port(network);
                inputPorts = newInputChannels;
            }
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

        // chains the input of the current processor to the output of the inputProcessor and the output of the current processor to the input of the outputProcessor
        public void Chain(Processor inputProcessor)
        {
            if (IsInputChained()) throw new System.Exception("Input already chained.");

            _chainedInputProcessor = inputProcessor;
            inputProcessor._chainedOutputProcessor = this;

            inputPorts[0].Disconnect();
            inputProcessor.outputPort.Disconnect();
            SignalChannel signalChannel = new();
            inputPorts[0].signalChannel = signalChannel;
            inputProcessor.outputPort.signalChannel = signalChannel;
        }

        public void Unchain()
        {
            if (!IsInputChained()) throw new System.Exception("Input not chained.");

            _chainedInputProcessor.outputPort.Connect();
            inputPorts[0].Connect();

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

        protected virtual float ProcessSignal(float[] signals) { Debug.LogError("Signal processor not implemented."); return 0; }

        public void AdvanceOutputQueue()
        {
            if (IsOutputChained()) return; // if chained, the output queue is skipped and the signal is sent directly to the chained output processor

            outputPort.Write(_outputQueue.Dequeue());
            _outputQueue.Enqueue(_processedSignal);
        }
    }

    public class Actuator : MonoBehaviour
    {
        public SignalNetworkGraph network;
        public Port inputPort;

        // the initializer should be called in the derived class's Start() method
        public void Initialize(SignalNetworkGraph signalNetworkGraph)
        {
            network = signalNetworkGraph;
            inputPort = new Port(network);
        }

        public void Write()
        {
            WriteActuator(inputPort.Read());
        }

        protected virtual void WriteActuator(float signal) { }
    }
}
