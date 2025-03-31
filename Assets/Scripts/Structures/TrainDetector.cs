using System.Collections.Generic;
using Newtonsoft.Json;
using Structures;
using UnityEngine;

public class TrainDetector : SensorRail
{
    public bool weightMode = false;

    public override string GetStateJson()
    {
        CombinedState state = new CombinedState
        {
            baseState = base.GetStateJson(),
            inheritedState = JsonConvert.SerializeObject(weightMode)
        };
        return JsonConvert.SerializeObject(state);
    }

    public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
    {
        CombinedState state = JsonConvert.DeserializeObject<CombinedState>(stateJson);
        base.RestoreStateJson(state.baseState, idLookup);
        weightMode = JsonConvert.DeserializeObject<bool>(state.inheritedState);
    }

    protected override float ReadSensor()
    {
        if (weightMode)
        {
            // TODO: Implement weight detection logic here
            // For example, set the sensor value based on the weight of the train
            // sensorValue = CalculateWeight();
            return 0; // Placeholder for weight calculation
        }
        else
        {
            return trains.Count > 0 ? 1 : 0;
        }
    }

    public void ModifyValue()
    {
        weightMode = !weightMode;
    }
}
