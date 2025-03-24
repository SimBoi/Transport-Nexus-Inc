using UnityEngine;

public class Cart : MonoBehaviour
{
    [HideInInspector] public Train train;

    private void OnTriggerEnter()
    {
        if (train.isCrashed) return;

        train.Crash();
    }
}
