using Components;
using UnityEngine;

public class OrGate : Processor
{
    protected override float ProcessSignal(float[] inputSignals)
    {
        float outputSignal = 0;
        foreach (float inputSignal in inputSignals)
        {
            outputSignal = Mathf.Max(outputSignal, inputSignal);
        }
        return outputSignal > 0 ? 1 : 0;
    }
}
