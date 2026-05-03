using System;
using System.Collections;
using System.Collections.Generic;
using Inventories;
using Newtonsoft.Json;
using Structures;
using UnityEngine;

public class ResourceExtractor : Machine
{
    public const int speedTicks = 1;
    private int currentProcessingTicks = 0; // TODO move this to the Machine base class
    public ResourceNode resourceNode = ResourceNode.None;

    public override string TypeName => GetType().ToString();

    public override string GetStateJson()
    {
        CombinedState combinedState = new()
        {
            baseState = base.GetStateJson(),
            inheritedState = JsonConvert.SerializeObject((
                currentProcessingTicks,
                resourceNode
            ))
        };
        return JsonConvert.SerializeObject(combinedState);
    }

    public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
    {
        CombinedState combinedState = JsonConvert.DeserializeObject<CombinedState>(stateJson);
        base.RestoreStateJson(combinedState.baseState, idLookup);
        var state = JsonConvert.DeserializeObject<(int, ResourceNode)>(combinedState.inheritedState);
        currentProcessingTicks = state.Item1;
        resourceNode = state.Item2;
    }

    public override void ProcessMachine()
    {
        if (resourceNode == ResourceNode.None) return;
        if (currentProcessingTicks < speedTicks) currentProcessingTicks++;
        if (currentProcessingTicks == speedTicks && outputResources[0][0] == null)
        {
            outputResources[0][0] = null; // TODO access the registry to create an item
            currentProcessingTicks = 0;
        }
    }
}
