using System;
using System.Collections;
using System.Collections.Generic;
using Inventories;
using Newtonsoft.Json;
using Structures;
using UnityEngine;

public class CargoExchange : Machine
{
    public const int speedTicks = 1;
    private int currentProcessingTicks = 0;

    public override string TypeName => GetType().ToString();

    public override void Awake()
    {
        base.Awake();
        OnInput += InputFromCart;
        OnOutput += OutputToCart;
    }

    public override string GetStateJson()
    {
        CombinedState combinedState = new()
        {
            baseState = base.GetStateJson(),
            inheritedState = JsonConvert.SerializeObject(
                currentProcessingTicks
            )
        };
        return JsonConvert.SerializeObject(combinedState);
    }

    public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
    {
        CombinedState combinedState = JsonConvert.DeserializeObject<CombinedState>(stateJson);
        base.RestoreStateJson(combinedState.baseState, idLookup);
        var state = JsonConvert.DeserializeObject<int>(combinedState.inheritedState);
        currentProcessingTicks = state;
    }

    public override void ProcessMachine()
    {
        if (currentProcessingTicks < speedTicks) currentProcessingTicks++;
        if (currentProcessingTicks == speedTicks && inputResources[0][0] != null && outputResources[0][0] == null)
        {
            outputResources[0][0] = inputResources[0][0];
            inputResources[0][0] = null;
            currentProcessingTicks = 0;
        }
    }

    public void InputFromCargoStorage()
    {
        for (int channel = 0; channel < inputFunnels.Length; channel++)
        {
            for (int i = 0; i < numberOfInputs[channel]; i++)
            {
                if (inputResources[channel][i] != null) continue;

                Vector2Int funnelTile = GameManager.Vector3ToTile(inputFunnels[channel].transform.position);

                // find out which cargo storage type we are connected to
                Cart cart = GameManager.Instance.GetCart(funnelTile);
                StructureEntity structure = GameManager.Instance.GetTileStructure(funnelTile);
                CargoStorage storage;
                if (cart is CargoCart cargoCart && cargoCart.train.speed <= 0)
                    storage = cargoCart.storage;
                else if (structure is CargoContainer cargoContainer)
                    storage = cargoContainer.storage;
                else
                    continue;
                
                ResourceEntity resourceToPickup = storage.TryOutputResource();
                if (resourceToPickup == null) continue;
                inputResources[channel][i] = resourceToPickup;
                resourceToPickup.EnterInventory();
                resourceToPickup.transform.position = transform.position;

                break; // only pick one resource at a time
            }
        }
    }

    public void OutputToCargoStorage()
    {
        for (int channel = 0; channel < outputFunnels.Length; channel++)
        {
            for (int i = 0; i < numberOfOutputs[channel]; i++)
            {
                ResourceEntity resource = outputResources[channel][i];
                if (resource == null) continue;

                Vector2Int funnelTile = GameManager.Vector3ToTile(outputFunnels[channel].transform.position);
                Cart cart = GameManager.Instance.GetCart(funnelTile);
                if (cart is CargoCart cargoCart)
                {
                    if (cargoCart.train.speed > 0) continue;
                    if (cargoCart.TryInputResource(resource, () =>
                    {
                        resource.ExitInventory();
                        outputResources[channel][i] = null;
                    }))
                    {
                        break; // only output one resource at a time
                    }
                }
            }
        }
    }
}

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
