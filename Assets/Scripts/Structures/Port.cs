using System;
using UnityEngine;

namespace Structures
{
    public class Port : MonoBehaviour, IComparable<Port>
    {
        public SignalNetworkGraph network { get; private set; }
        public SignalChannel signalChannel { get; set; }
        public bool isConnected => signalChannel != null;

        public void AddToNetwork(SignalNetworkGraph signalNetworkGraph)
        {
            if (network != null) throw new Exception("Port already in a network.");
            network = signalNetworkGraph;
            network.AddPort(this);
        }

        public void RemoveFromNetwork()
        {
            network.RemovePort(this);
            network = null;
        }

        public void Write(float signal)
        {
            if (signalChannel == null) return;
            signalChannel.Write(signal);
        }

        public float Read()
        {
            return isConnected ? signalChannel.Read() : 0;
        }

        public void Reset()
        {
            if (signalChannel == null) return;
            signalChannel.Reset();
        }

        public int CompareTo(Port other)
        {
            return GetInstanceID().CompareTo(other.GetInstanceID());
        }
    }
}