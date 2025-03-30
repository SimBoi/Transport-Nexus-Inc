using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Structures;
using UnityEngine;

public class RailTIntersection : ActuatorRail
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
        OrientRail();
    }

    public override List<Vector2Int> GetCurrentTrainOrientations(Vector2Int tile)
    {
        Vector2Int tileOrientation = GameManager.Instance.GetTileOrientation(tile);
        if (isRightTurn) return new List<Vector2Int> { tileOrientation, new Vector2Int(-tileOrientation.y, tileOrientation.x) };
        else return new List<Vector2Int> { tileOrientation, new Vector2Int(tileOrientation.y, -tileOrientation.x) };
    }

    public override List<Vector2Int> GetAllTrainOrientations(Vector2Int tile)
    {
        Vector2Int tileOrientation = GameManager.Instance.GetTileOrientation(tile);
        return new List<Vector2Int> { tileOrientation, new Vector2Int(-tileOrientation.y, tileOrientation.x), new Vector2Int(tileOrientation.y, -tileOrientation.x) };
    }

    protected override void WriteActuator(float[] inputSignals)
    {
        isRightTurn = inputSignals[0] == 0;

        OrientRail();
    }

    public void OrientRail()
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
