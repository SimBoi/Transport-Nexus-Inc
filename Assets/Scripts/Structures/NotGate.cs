using Structures;
using UnityEngine;

public class NotGate : Processor
{
    protected override float ProcessSignal(float[] inputSignals)
    {
        return Mathf.Max(1 - inputSignals[0], 0);
    }
}
