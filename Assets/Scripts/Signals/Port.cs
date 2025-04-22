using System;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Signals
{
    // Ports belonging to the same structure should have different names, this is necessary for restoring states from JSON correctly
    public class Port : MonoBehaviour, IComparable<Port>, ISavable
    {
        public PortNetworkGraph network { get; private set; }
        public Channel signalChannel { get; set; }
        public bool isConnected => signalChannel != null;

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
            return JsonConvert.SerializeObject((
                signalChannel?.ID ?? -1,
                name,
                transform.parent.GetComponentInParent<ISavable>().ID
            ));
        }

        public void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
        {
            (int signalChannelId, string _, int _) = JsonConvert.DeserializeObject<(int, string, int)>(stateJson);
            network = GameManager.Instance.signalNetworkGraph;
            signalChannel = signalChannelId == -1 ? null : (Channel)idLookup[signalChannelId];
        }

        public void AddToNetwork(PortNetworkGraph signalNetworkGraph)
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
