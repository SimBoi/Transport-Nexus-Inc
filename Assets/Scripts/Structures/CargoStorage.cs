using System;
using System.Collections;
using System.Collections.Generic;
using Inventories;
using Newtonsoft.Json;
using UnityEngine;

// TODO: future improvement potentially avoid inheriting MonoBehaviour
public class CargoStorage : MonoBehaviour, ISavableProperty
{
    [SerializeField] private int capacity = 10;
    private ResourceEntity[] cargo;

    public void Awake()
    {
        cargo = new ResourceEntity[capacity];
    }

    public string GetStateJson()
    {
        return JsonConvert.SerializeObject((
            capacity,
            Array.ConvertAll(cargo, c => c == null ? -1 : c.ID)
        ));
    }

    public void RestoreStateJson(
        string stateJson,
        Dictionary<int, ISavable> idLookup)
    {
        var state = JsonConvert.DeserializeObject<(int, int[])>(stateJson);
        capacity = state.Item1;
        cargo = new ResourceEntity[capacity];
        for (int i = 0; i < state.Item2.Length; i++)
            cargo[i] = state.Item2[i] == -1 ? null : idLookup[state.Item2[i]] as ResourceEntity;
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
            return resource;
        }
        return null;
    }
}
