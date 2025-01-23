using System.Collections.Generic;
using UnityEngine;
using Signals;

namespace Structures
{
    public class Sensor : MonoBehaviour
    {
        [HideInInspector] public GameObject prefab; // must be set by the instantiator
        public PortNetworkGraph network { get; private set; }
        [SerializeField] public Port outputPort;

        public void Initialize(PortNetworkGraph signalNetworkGraph)
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

    public class Actuator : MonoBehaviour
    {
        [HideInInspector] public GameObject prefab; // must be set by the instantiator
        public PortNetworkGraph network { get; private set; }
        [SerializeField] public Port[] inputPorts;

        // the initializer should be called in the derived class's Start() method
        public void Initialize(PortNetworkGraph signalNetworkGraph)
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
