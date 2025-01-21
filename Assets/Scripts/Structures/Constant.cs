using Structures;
using UnityEngine;

public class Constant : Sensor
{
    [SerializeField] private float _value;

    protected override float ReadSensor()
    {
        return _value;
    }
}
