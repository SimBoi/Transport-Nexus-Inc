using Structures;
using UnityEngine;

public class Constant : Sensor
{
    [SerializeField] private int _value;

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
