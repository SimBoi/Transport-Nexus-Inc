using Structures;
using UnityEngine;

namespace Signals
{
    public class SplitterPort : StructureEntity
    {
        public PortNetworkGraph network { get; private set; }
        [SerializeField] public Port port;

        virtual public void InitializeSplitterPort(PortNetworkGraph signalNetworkGraph)
        {
            network = signalNetworkGraph;
            port.AddToNetwork(network);
        }
    }
}
