using System;
using Inventories;
using Newtonsoft.Json;
using UnityEngine;

public class CargoCart : Cart
{
    [SerializeField] private int capacity = 10;
    private ConveyedResource[] cargo;

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
        // foreach (ConveyedResource[] resources in inputResources) foreach (ConveyedResource resource in resources) if (resource != null) resource.ExitInventory();
        foreach (ConveyedResource resource in cargo) if (resource != null) resource.ExitInventory(transform.position);
    }
}
