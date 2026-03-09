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

    public override string TypeName => GetType().ToString() + speedTicks;

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

    public void InputFromCart()
    {
        for (int channel = 0; channel < inputFunnels.Length; channel++)
        {
            for (int i = 0; i < numberOfInputs[channel]; i++)
            {
                if (inputResources[channel][i] != null) continue;

                Vector2Int funnelTile = GameManager.Vector3ToTile(inputFunnels[channel].transform.position);
                Cart cart = GameManager.Instance.GetCart(funnelTile);
                if (cart is CargoCart cargoCart)
                {
                    ConveyedResource resourceToPickup = cargoCart.TryOutputResource();
                    if (resourceToPickup == null) continue;
                    inputResources[channel][i] = resourceToPickup;
                    resourceToPickup.EnterInventory();
                    resourceToPickup.transform.position = transform.position;

                    break; // only pick one resource at a time
                }
            }
        }
    }

    public void OutputToCart()
    {
        for (int channel = 0; channel < outputFunnels.Length; channel++)
        {
            for (int i = 0; i < numberOfOutputs[channel]; i++)
            {
                if (outputResources[channel][i] == null) continue;

                Vector2Int funnelTile = GameManager.Vector3ToTile(outputFunnels[channel].transform.position);
                Cart cart = GameManager.Instance.GetCart(funnelTile);
                if (cart is CargoCart cargoCart)
                {
                    if (cargoCart.TryInputResource(outputResources[channel][i], ))
                    {
                        outputResources[channel][i].ExitInventory();
                        outputResources[channel][i] = null;
                    }

                    break; // only output one resource at a time
                }
            }
        }
    }
}
