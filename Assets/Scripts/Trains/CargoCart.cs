using System;
using System.Linq;
using Inventories;
using Mono.Cecil;
using Newtonsoft.Json;
using UnityEngine;

public class CargoCart : Cart
{
    [SerializeField] private int capacity = 10;
    private ConveyedResource[] cargo;


    public void Awake()
    {
        cargo = new ConveyedResource[capacity];
    }

    public override string GetStateJson()
    {
        CombinedState combinedState = new()
        {
            baseState = base.GetStateJson(),
            inheritedState = JsonConvert.SerializeObject((
                capacity,
                Array.ConvertAll(cargo, c => c == null ? -1 : c.ID)
            ))
        };
        return JsonConvert.SerializeObject(combinedState);
    }

    public override void RestoreStateJson(string stateJson, System.Collections.Generic.Dictionary<int, ISavable> idLookup)
    {
        CombinedState combinedState = JsonConvert.DeserializeObject<CombinedState>(stateJson);
        base.RestoreStateJson(combinedState.baseState, idLookup);
        var state = JsonConvert.DeserializeObject<(int, int[])>(combinedState.inheritedState);
        capacity = state.Item1;
        cargo = new ConveyedResource[capacity];
        for (int i = 0; i < state.Item2.Length; i++)
            cargo[i] = state.Item2[i] == -1 ? null : idLookup[state.Item2[i]] as ConveyedResource;
    }

    public void DropInventory()
    {
        foreach (ConveyedResource resource in cargo) if (resource != null) resource.ExitInventory(transform.position);
        for (int i = 0; i < cargo.Count(); i++) cargo[i] = null;
    }

    public bool TryInputResource(ConveyedResource resource, Action PrepareResource= null)
    {
        for (int i = 0; i < cargo.Count(); i++) if (cargo[i] == null)
        {
            if (PrepareResource != null) PrepareResource.Invoke();
            resource.EnterInventory();
            cargo[i] = resource;
            return true;
        }
        return false;
    }

    public ConveyedResource TryOutputResource()
    {
        for (int i = 0; i < cargo.Count(); i++) if (cargo[i] != null)
        {
            ConveyedResource resource = cargo[i];
            resource.ExitInventory(transform.position);
            cargo[i] = null;
            return resource;
        }
        return null;
    }
}
