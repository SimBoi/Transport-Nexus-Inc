using System.Collections;
using System.Collections.Generic;
using Structures;
using UnityEngine;

public class TrainStop : ActuatorRail
{
    [SerializeField] private float stopTime = 5;
    private List<Train> stoppedTrains = new List<Train>();
    private List<float> timers = new List<float>();

    private void Update()
    {
        for (int i = 0; i < stoppedTrains.Count; i++)
        {
            timers[i] -= Time.deltaTime;
            if (timers[i] <= 0)
            {
                stoppedTrains[i].Accelerate();
                stoppedTrains.RemoveAt(i);
                timers.RemoveAt(i);
                i--;
            }
        }
    }

    protected override void OnTrainEnter(Train train)
    {
        base.OnTrainEnter(train);
        stoppedTrains.Add(train);
        timers.Add(stopTime);
        train.Brake(2);
    }

    protected override void OnTrainExit(Train train)
    {
        base.OnTrainExit(train);
    }
}
