using System;
using System.Collections;
using System.Collections.Generic;
using Inventories;
using Newtonsoft.Json;
using Structures;
using UnityEngine;

public class RecipeMachine : Machine
{
    [SerializeField] private RecipeBook recipeBook;

    private int currentRecipeIndex = -1;
    private int currentProcessingTicks = 0;
    private List<ConveyedResource> ingredientResources;
    private List<ConveyedResource>[] producedResources;

    public override string TypeName => GetType().ToString() + recipeBook.HashBook().ToString();

    public override void Awake()
    {
        base.Awake();
        ingredientResources = new List<ConveyedResource>();
        producedResources = new List<ConveyedResource>[numberOfOutputs.Length];
        for (int i = 0; i < producedResources.Length; i++)
        {
            producedResources[i] = new List<ConveyedResource>();
        }
    }

    public override string GetStateJson()
    {
        CombinedState combinedState = new()
        {
            baseState = base.GetStateJson(),
            inheritedState = JsonConvert.SerializeObject((
                currentRecipeIndex,
                currentProcessingTicks,
                ingredientResources.ConvertAll(resource => resource.ID),
                Array.ConvertAll(producedResources, channel => channel.ConvertAll(resource => resource.ID))
            ))
        };
        return JsonConvert.SerializeObject(combinedState);
    }

    public override void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup)
    {
        CombinedState combinedState = JsonConvert.DeserializeObject<CombinedState>(stateJson);
        base.RestoreStateJson(combinedState.baseState, idLookup);
        var state = JsonConvert.DeserializeObject<((int, int), List<int>, List<List<int>>)>(combinedState.inheritedState);
        currentRecipeIndex = state.Item1.Item1;
        currentProcessingTicks = state.Item1.Item2;
        ingredientResources = state.Item2.ConvertAll(id => idLookup[id] as ConveyedResource);
        for (int i = 0; i < producedResources.Length; i++)
        {
            producedResources[i] = state.Item3[i].ConvertAll(id => idLookup[id] as ConveyedResource);
        }
    }

    public override void ProcessMachine()
    {
        // advance the recipe processing ticks and check if the recipe is finished
        if (currentRecipeIndex != -1)
        {
            currentProcessingTicks++;
            if (currentProcessingTicks >= recipeBook.list[currentRecipeIndex].processingTicks) FinishRecipe();
        }

        // if there are products in the producedResources list, try to move them to the output conveyor
        for (int channel = 0; channel < producedResources.Length; channel++)
        {
            if (producedResources[channel].Count == 0) continue;
            TryOutputResources(channel, producedResources[channel]);
        }

        // if there are no products in the producedResources list, try to start a new recipe
        if (currentRecipeIndex == -1)
        {
            foreach (List<ConveyedResource> r in producedResources) if (r.Count > 0) return;
            StartRecipe();
        }
    }

    public void StartRecipe()
    {
        // try to start a new recipe, set currentRecipeIndex to the index of the first valid recipe and move the resources to the processedResources list
        int recipeIndex = recipeBook.GetFirstValidRecipe(inputResources);
        if (recipeIndex != -1)
        {
            currentRecipeIndex = recipeIndex;
            currentProcessingTicks = 0;

            RecipeBook.Recipe recipe = recipeBook.list[currentRecipeIndex];
            foreach (RecipeBook.Ingredient ingredient in recipe.ingredients)
            {
                // find the and move the required amount from the inputResources to the processedResources list
                int ingredientAmount = 0;
                for (int i = 0; i < inputResources[ingredient.channel].Length && ingredientAmount < ingredient.amount; i++)
                {
                    ConveyedResource resource = inputResources[ingredient.channel][i];
                    if (resource == null) continue;
                    if (ingredient.materialType != resource.materialType) continue;
                    ingredientResources.Add(resource);
                    inputResources[ingredient.channel][i] = null;
                    ingredientAmount++;
                }
            }
        }
    }

    public void FinishRecipe()
    {
        // destroy the processed resources
        foreach (ConveyedResource resource in ingredientResources) resource.DestroyResource();
        ingredientResources.Clear();

        // convert to products
        RecipeBook.Recipe recipe = recipeBook.list[currentRecipeIndex];
        foreach (RecipeBook.Ingredient product in recipe.products)
        {
            GameObject prefab = PrefabRegistries.Instance.materials[product.materialType];
            for (int i = 0; i < product.amount; i++)
            {
                ConveyedResource resource = Instantiate(prefab, transform.position, Quaternion.identity).GetComponent<ConveyedResource>();
                resource.EnterInventory();
                producedResources[product.channel].Add(resource);
            }
        }

        currentRecipeIndex = -1;
    }

    public override void DropInventory()
    {
        base.DropInventory();

        foreach (ConveyedResource r in ingredientResources) if (r != null) r.ExitInventory();
        foreach (List<ConveyedResource> channel in producedResources) foreach (ConveyedResource r in channel) if (r != null) r.ExitInventory();

        ingredientResources.Clear();
        foreach (List<ConveyedResource> channel in producedResources) channel.Clear();
    }
}
