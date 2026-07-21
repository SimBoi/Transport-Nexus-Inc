using System;
using System.Collections;
using System.Collections.Generic;
using Inventories;
using Newtonsoft.Json;
using UnityEngine;

// TODO: future improvement potentially avoid inheriting MonoBehaviour
public class CargoStorage : MonoBehaviour, ISavableProperty
{
    public int Capacity { get; private set; } = 10;
    public int Count { get; private set; }
    private ResourceEntity[] cargo;

    public void Awake()
    {
        cargo = new ResourceEntity[Capacity];
        Count = 0;
    }

    public string GetStateJson()
    {
        return JsonConvert.SerializeObject((
            Capacity,
            Array.ConvertAll(cargo, c => c == null ? -1 : c.ID)
        ));
    }

    public void RestoreStateJson(
        string stateJson,
        Dictionary<int, ISavable> idLookup)
    {
        var state = JsonConvert.DeserializeObject<(int, int[])>(stateJson);
        Capacity = state.Item1;
        cargo = new ResourceEntity[Capacity];
        Count = 0;
        for (int i = 0; i < state.Item2.Length; i++)
        {
            cargo[i] = state.Item2[i] == -1 ? null : idLookup[state.Item2[i]] as ResourceEntity;
            if (cargo[i] != null) Count++;
        }
    }

    public void OnDestroy()
    {
        DropInventory();
    }

    public void DropInventory()
    {
        foreach (ResourceEntity resource in cargo)
            if (resource != null)
                resource.ExitInventory(transform.position);
        for (int i = 0; i < cargo.Length; i++)
            cargo[i] = null;
        Count = 0;
    }

    public bool TryInputResource(ResourceEntity resource, Action PrepareResource= null)
    {
        for (int i = 0; i < cargo.Length; i++)
        {
            if (cargo[i] != null)
                continue;
            PrepareResource?.Invoke();
            resource.EnterInventory();
            cargo[i] = resource;
            Count++;
            return true;
        }
        return false;
    }

    public ResourceEntity TryOutputResource()
    {
        for (int i = 0; i < cargo.Length; i++)
        {
            if (cargo[i] == null) 
                continue;
            ResourceEntity resource = cargo[i];
            resource.ExitInventory(transform.position);
            cargo[i] = null;
            Count--;
            return resource;
        }
        return null;
    }
}
