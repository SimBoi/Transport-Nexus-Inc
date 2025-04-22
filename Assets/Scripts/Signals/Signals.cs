using System.Collections.Generic;
using UnityEngine;
using QuikGraph;
using System.Linq;
using Newtonsoft.Json;

namespace Signals
{
    public class PortNetworkGraph
    {
        private UndirectedGraph<Port, TaggedUndirectedEdge<Port, GameObject>> _graph = new();
        private Dictionary<GameObject, TaggedUndirectedEdge<Port, GameObject>> _wires = new();
        private HashSet<Channel> _signalChannels = new();

        public void SaveState(SaveData saveData)
        {
            foreach (TaggedUndirectedEdge<Port, GameObject> edge in _graph.Edges)
            {
                Port port1 = edge.Source;
                Port port2 = edge.Target;
                saveData.portConnections.Add((port1.ID, port2.ID));
            }

            foreach (Channel signalChannel in _signalChannels) saveData.channelIds.Add(signalChannel.ID);
        }

        public void RestoreVertices(SaveData saveData, Dictionary<int, ISavable> idLookup)
        {
            foreach (SavebleEntry entry in saveData.savables)
            {
                if (entry.type != typeof(Port).ToString()) continue;
                Port port = (Port)idLookup[entry.id];
                _graph.AddVertex(port);
            }
        }

        public void RestoreChannels(SaveData saveData, Dictionary<int, ISavable> idLookup)
        {
            foreach (int channelId in saveData.channelIds)
            {
                Channel channel = (Channel)idLookup[channelId];
                _signalChannels.Add(channel);
            }
        }

        public void RestoreEdge(GameObject wire, Port port1, Port port2)
        {
            TaggedUndirectedEdge<Port, GameObject> edge;
            if (port1.CompareTo(port2) > 0) edge = new(port2, port1, wire);
            else edge = new(port1, port2, wire);

            _graph.AddEdge(edge);
            _wires.Add(wire, edge);
        }

        public void AddPort(Port port)
        {
            if (!_graph.AddVertex(port)) return;
            port.signalChannel = null;
        }

        public void RemovePort(Port port)
        {
            foreach (TaggedUndirectedEdge<Port, GameObject> edge in _graph.AdjacentEdges(port).ToList()) DisconnectWire(edge.Tag);
            if (port.isConnected) _signalChannels.Remove(port.signalChannel);
            _graph.RemoveVertex(port);
        }

        public void ConnectWire(GameObject wire, Port port1, Port port2)
        {
            if (port1.isConnected) AssignSignalChannelBFS(port2, port1.signalChannel);
            else if (port2.isConnected) AssignSignalChannelBFS(port1, port2.signalChannel);
            else
            {
                AssignSignalChannelBFS(port1, new Channel());
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
                AssignSignalChannelBFS(port1, new Channel(), false);
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

            Object.Destroy(wire);
        }

        public void AssignSignalChannelBFS(Port startPort, Channel signalChannel, bool removeOldChannel = true)
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
            foreach (Channel signalChannel in _signalChannels) signalChannel.Reset();
        }
    }

    public class Channel : ISavable
    {
        private float _signal = 0;

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

        public string TypeName => GetType().ToString();

        public bool ShouldInstantiateOnLoad() => false;

        public string GetStateJson()
        {
            return JsonConvert.SerializeObject(_signal);
        }

        public void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
        {
            float signal = JsonConvert.DeserializeObject<float>(stateJson);
            _signal = signal;
        }

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
}