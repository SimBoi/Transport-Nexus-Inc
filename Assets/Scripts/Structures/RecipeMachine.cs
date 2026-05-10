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
    private List<ResourceEntity> ingredientResources;
    private List<ResourceEntity>[] producedResources;

    public override string TypeName => GetType().ToString() + recipeBook.HashBook().ToString();

    public override void Awake()
    {
        base.Awake();
        ingredientResources = new List<ResourceEntity>();
        producedResources = new List<ResourceEntity>[numberOfOutputs.Length];
        for (int i = 0; i < producedResources.Length; i++)
        {
            producedResources[i] = new List<ResourceEntity>();
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
        var state = JsonConvert.DeserializeObject<(int, int, List<int>, List<int>[])>(combinedState.inheritedState);
        currentRecipeIndex = state.Item1;
        currentProcessingTicks = state.Item2;
        ingredientResources = state.Item3.ConvertAll(id => idLookup[id] as ResourceEntity);
        for (int i = 0; i < producedResources.Length; i++)
        {
            producedResources[i] = state.Item4[i].ConvertAll(id => idLookup[id] as ResourceEntity);
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
            foreach (List<ResourceEntity> r in producedResources) if (r.Count > 0) return;
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
                    ResourceEntity resource = inputResources[ingredient.channel][i];
                    if (resource == null) continue;
                    if (ingredient.resourceType != resource.resourceType) continue;
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
        foreach (ResourceEntity resource in ingredientResources) resource.DestroyResource();
        ingredientResources.Clear();

        // convert to products
        RecipeBook.Recipe recipe = recipeBook.list[currentRecipeIndex];
        foreach (RecipeBook.Ingredient product in recipe.products)
        {
            GameObject prefab = PrefabRegistries.Instance.resources[product.resourceType];
            for (int i = 0; i < product.amount; i++)
            {
                ResourceEntity resource = Instantiate(prefab, transform.position, Quaternion.identity).GetComponent<ResourceEntity>();
                resource.EnterInventory();
                producedResources[product.channel].Add(resource);
            }
        }

        currentRecipeIndex = -1;
    }

    public override void DropInventory()
    {
        base.DropInventory();

        foreach (ResourceEntity r in ingredientResources) if (r != null) r.ExitInventory(transform.position);
        foreach (List<ResourceEntity> channel in producedResources) foreach (ResourceEntity r in channel) if (r != null) r.ExitInventory(transform.position);

        ingredientResources.Clear();
        foreach (List<ResourceEntity> channel in producedResources) channel.Clear();
    }
}
