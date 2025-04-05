using System.Collections;
using System.Collections.Generic;
using Inventories;
using Newtonsoft.Json;
using Structures;
using UnityEngine;

public class ConveyorBeltTIntersection : ActuatorConveyorBelt
{
    [SerializeField] private GameObject rightTurnModel;
    [SerializeField] private GameObject leftTurnModel;
    public bool isRightTurn = true;

    public override string GetStateJson()
    {
        CombinedState state = new CombinedState
        {
            baseState = base.GetStateJson(),
            inheritedState = JsonConvert.SerializeObject(isRightTurn)
        };
        return JsonConvert.SerializeObject(state);
    }

    public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
    {
        CombinedState state = JsonConvert.DeserializeObject<CombinedState>(stateJson);
        base.RestoreStateJson(state.baseState, idLookup);
        isRightTurn = JsonConvert.DeserializeObject<bool>(state.inheritedState);
        OrientBelt();
    }

    public override Vector2Int GetNextExitOrientation(ConveyedResource resource)
    {
        Vector2Int orientation = GameManager.Instance.GetTileOrientation(tile);
        return isRightTurn ? new Vector2Int(orientation.y, -orientation.x) : new Vector2Int(-orientation.y, orientation.x);
    }

    public override List<Vector2Int> GetExitOrientations()
    {
        Vector2Int orientation = GameManager.Instance.GetTileOrientation(tile);
        return new List<Vector2Int>
        {
            new Vector2Int(orientation.y, -orientation.x), // right turn
            new Vector2Int(-orientation.y, orientation.x) // left turn
        };
    }

    protected override void WriteActuator(float[] inputSignals)
    {
        isRightTurn = inputSignals[0] == 0;

        OrientBelt();
    }

    public void OrientBelt()
    {
        if (isRightTurn)
        {
            rightTurnModel.SetActive(true);
            leftTurnModel.SetActive(false);
        }
        else
        {
            rightTurnModel.SetActive(false);
            leftTurnModel.SetActive(true);
        }
    }
}
