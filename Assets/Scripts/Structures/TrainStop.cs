using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Structures;
using UnityEngine;

public class TrainStop : ActuatorRail
{
    public bool stopTrain = true;

    public override string GetStateJson()
    {
        CombinedState state = new CombinedState
        {
            baseState = base.GetStateJson(),
            inheritedState = JsonConvert.SerializeObject(stopTrain)
        };
        return JsonConvert.SerializeObject(state);
    }

    public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
    {
        CombinedState state = JsonConvert.DeserializeObject<CombinedState>(stateJson);
        base.RestoreStateJson(state.baseState, idLookup);
        stopTrain = JsonConvert.DeserializeObject<bool>(state.inheritedState);
    }

    protected override void OnTrainEnter(Train train)
    {
        if (stopTrain) train.Brake(2);
        else train.Accelerate();
    }

    protected override void WriteActuator(float[] inputSignals)
    {
        if (inputSignals[0] > 0)
        {
            stopTrain = true;
        }
        else
        {
            if (stopTrain) foreach (Train train in trains) train.Accelerate();
            stopTrain = false;
        }
    }
}
