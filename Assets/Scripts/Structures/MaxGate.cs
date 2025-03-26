using Structures;
using UnityEngine;

public class MaxGate : Processor
{
    protected override float ProcessSignal(float[] inputSignals)
    {
        float outputSignal = 0;
        foreach (float inputSignal in inputSignals)
        {
            outputSignal = Mathf.Max(outputSignal, inputSignal);
        }
        return outputSignal;
    }
}
