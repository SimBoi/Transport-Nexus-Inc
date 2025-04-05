using Structures;
using UnityEngine;

public class ConveyorBelt : DynamicConveyorBelt
{
    [SerializeField] private GameObject straightModel;
    [SerializeField] private GameObject rightTurnModel;
    [SerializeField] private GameObject leftTurnModel;

    public override void OnOrientConveyorBelt()
    {
        Vector2Int orientation = GameManager.Instance.GetTileOrientation(tile);

        if (exitOrientation == orientation) // straight conveyor belt
        {
            straightModel.SetActive(true);
            rightTurnModel.SetActive(false);
            leftTurnModel.SetActive(false);
        }
        else if (exitOrientation == new Vector2Int(orientation.y, -orientation.x)) // right turn conveyor belt
        {
            straightModel.SetActive(false);
            rightTurnModel.SetActive(true);
            leftTurnModel.SetActive(false);
        }
        else
        {
            straightModel.SetActive(false);
            rightTurnModel.SetActive(false);
            leftTurnModel.SetActive(true);
        }
    }
}
