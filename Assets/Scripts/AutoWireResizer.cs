using GogoGaga.OptimizedRopesAndCables;
using UnityEngine;

public class AutoWireResizer : MonoBehaviour
{
    [SerializeField] private Rope rope;
    [SerializeField] private float slack = 0.5f;

    public void SetStart(Vector3 start)
    {
        rope.StartPoint.position = start;
    }

    public void SetEnd(Vector3 end)
    {
        rope.EndPoint.position = end;

        // resize the rope, adding slack
        float length = Vector3.Distance(rope.StartPoint.position, rope.EndPoint.position) + slack;
        rope.ropeLength = length;
    }
}
