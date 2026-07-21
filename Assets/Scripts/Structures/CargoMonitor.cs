using System.Collections.Generic;
using Structures;
using UnityEngine;
using Newtonsoft.Json;

public class CargoMonitor : Sensor
{
    protected override float ReadSensor()
    {
        Vector2Int funnelTile = GameManager.Vector3ToTile(transform.position - transform.forward);
        CargoStorage storage = GameManager.Instance.GetStorageTile(funnelTile);
        if (storage == null)
            return 0;
        return 15 * storage.Count / storage.Capacity;
    }
}
