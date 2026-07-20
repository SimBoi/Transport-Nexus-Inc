using System;
using System.Collections.Generic;
using System.Linq;
using Inventories;
using Mono.Cecil;
using Newtonsoft.Json;
using UnityEngine;

public class CargoCart : Cart
{
    public CargoStorage storage;

    public override string GetStateJson()
    {
        CombinedState combinedState = new()
        {
            baseState = base.GetStateJson(),
            inheritedState = storage.GetStateJson()
        };
        return JsonConvert.SerializeObject(combinedState);
    }

    public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
    {
        CombinedState combinedState = JsonConvert.DeserializeObject<CombinedState>(stateJson);
        base.RestoreStateJson(combinedState.baseState, idLookup);
        storage.RestoreStateJson(combinedState.inheritedState, idLookup);
    }
}
