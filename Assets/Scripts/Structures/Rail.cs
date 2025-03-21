using Structures;
using UnityEngine;

public class Rail : DynamicRail
{
    [SerializeField] private GameObject straightRail;
    [SerializeField] private GameObject rightTurnRail;
    [SerializeField] private GameObject leftTurnRail;

    protected override void OnOrientRail()
    {
        // straight rail
        if (trainOrientations[0] == -trainOrientations[1])
        {
            straightRail.SetActive(true);
            rightTurnRail.SetActive(false);
            leftTurnRail.SetActive(false);
        }
        // right turn rail
        else if (trainOrientations[1] == new Vector2Int(-trainOrientations[0].y, trainOrientations[0].x))
        {
            straightRail.SetActive(false);
            rightTurnRail.SetActive(true);
            leftTurnRail.SetActive(false);
        }
        // left turn rail
        else
        {
            straightRail.SetActive(false);
            rightTurnRail.SetActive(false);
            leftTurnRail.SetActive(true);
        }

        transform.rotation = Quaternion.LookRotation(new Vector3(trainOrientations[0].x, 0, trainOrientations[0].y), Vector3.up);
    }
}
