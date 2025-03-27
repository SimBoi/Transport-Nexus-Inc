using System.Collections.Generic;
using Structures;
using UnityEngine;
using Newtonsoft.Json;

public class Constant : Sensor
{
    [SerializeField] private int _value;


    public override string GetStateJson()
    {
        CombinedState state = new CombinedState
        {
            baseState = base.GetStateJson(),
            inheritedState = JsonConvert.SerializeObject(_value)
        };
        return JsonConvert.SerializeObject(state);
    }

    public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
    {
        CombinedState state = JsonConvert.DeserializeObject<CombinedState>(stateJson);
        base.RestoreStateJson(state.baseState, idLookup);
        _value = JsonConvert.DeserializeObject<int>(state.inheritedState);
    }

    protected override float ReadSensor()
    {
        return _value;
    }

    public void ModifyValue(int offset)
    {
        _value += offset;
        _value = Mathf.Clamp(_value, 0, 15);
    }
}
