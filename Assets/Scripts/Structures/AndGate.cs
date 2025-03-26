using Structures;
using UnityEngine;

public class AndGate : Processor
{
    protected override float ProcessSignal(float[] inputSignals)
    {
        float outputSignal = 1;
        for (int i = 0; i < inputSignals.Length; i++)
        {
            if (!inputPorts[i].isConnected) continue;
            outputSignal = Mathf.Min(outputSignal, inputSignals[i]);
        }
        return outputSignal;
    }
}
