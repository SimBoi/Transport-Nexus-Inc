using Structures;
using UnityEngine;

public class Subtracter : Processor
{
    protected override float ProcessSignal(float[] inputSignals)
    {
        return Mathf.Max(inputSignals[0] - inputSignals[1] - inputSignals[2], 0);
    }
}
