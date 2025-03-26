using Structures;
using UnityEngine;

public class Adder : Processor
{
    protected override float ProcessSignal(float[] inputSignals)
    {
        // float outputSignal = 0;
        // foreach (float inputSignal in inputSignals)
        // {
        //     outputSignal = Mathf.Max(outputSignal, inputSignal);
        // }
        // return outputSignal;
        float outputSignal = 0;
        foreach (float inputSignal in inputSignals)
        {
            outputSignal += inputSignal;
        }
        return Mathf.Min(outputSignal, 15);
    }
}
