using System.Collections;
using System.Collections.Generic;
using Structures;
using UnityEngine;

public class TrainStop : ActuatorRail
{
    public bool stopTrain = true;

    protected override void OnTrainEnter(Train train)
    {
        if (stopTrain) train.Brake(2);
    }

    protected override void WriteActuator(float[] inputSignals)
    {
        if (inputSignals[0] > 0)
        {
            stopTrain = true;
        }
        else
        {
            stopTrain = false;
            foreach (Train train in trains)
            {
                train.Accelerate();
            }
        }
    }
}
