using Structures;
using UnityEngine;

public class XorGate : Processor
{
    protected override float ProcessSignal(float[] inputSignals)
    {
        float outputSignal = 0;
        foreach (float inputSignal in inputSignals)
        {
            outputSignal += inputSignal;
        }
        return outputSignal == 1 ? 1 : 0;
    }
}
