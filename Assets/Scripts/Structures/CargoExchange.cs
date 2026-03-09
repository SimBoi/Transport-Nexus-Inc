using System;
using System.Collections;
using System.Collections.Generic;
using Inventories;
using Newtonsoft.Json;
using Structures;
using UnityEngine;

public class CargoExchange : Machine
{
    public const int speedTicks = 1;
    private int currentProcessingTicks = 0;

    public override string TypeName => GetType().ToString() + speedTicks;

    public override void Awake()
    {
        base.Awake();
        OnInput += InputFromStructures;
        OnOutput += OutputToStructures;
    }

    public override string GetStateJson()
    {
        CombinedState combinedState = new()
        {
            baseState = base.GetStateJson(),
            inheritedState = JsonConvert.SerializeObject(
                currentProcessingTicks
            )
        };
        return JsonConvert.SerializeObject(combinedState);
    }

    public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
    {
        CombinedState combinedState = JsonConvert.DeserializeObject<CombinedState>(stateJson);
        base.RestoreStateJson(combinedState.baseState, idLookup);
        var state = JsonConvert.DeserializeObject<int>(combinedState.inheritedState);
        currentProcessingTicks = state;
    }

    public override void ProcessMachine()
    {
        if (currentProcessingTicks < speedTicks) currentProcessingTicks++;
        if (currentProcessingTicks == speedTicks && inputResources[0][0] != null && outputResources[0][0] == null)
        {
            outputResources[0][0] = inputResources[0][0];
            inputResources[0][0] = null;
            currentProcessingTicks = 0;
        }
    }

    public void InputFromStructures()
    {
        Debug.Log("try input");
    }

    public void OutputToStructures()
    {
        Debug.Log("try output");
    }
}
