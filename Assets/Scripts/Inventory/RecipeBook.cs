using System;
using System.Collections.Generic;
using Inventories;
using UnityEngine;

[CreateAssetMenu(fileName = "RecipeBook", menuName = "ScriptableObjects/RecipeBook", order = 1)]
public class RecipeBook : ScriptableObject
{
    [Serializable]
    public class Ingredient
    {
        public Materials materialType;
        public int amount;
        public int channel;
    }

    [Serializable]
    public class Recipe
    {
        public List<Ingredient> ingredients;
        public List<Ingredient> products;
        public int processingTicks;
    }

    public List<Recipe> list = new List<Recipe>();

    public int HashBook()
    {
        var hash = new HashCode();
        foreach (Recipe recipe in list)
        {
            foreach (Ingredient ingredient in recipe.ingredients)
            {
                hash.Add(ingredient.materialType);
                hash.Add(ingredient.amount);
                hash.Add(ingredient.channel);
            }
            foreach (Ingredient product in recipe.products)
            {
                hash.Add(product.materialType);
                hash.Add(product.amount);
                hash.Add(product.channel);
            }
            hash.Add(recipe.processingTicks);
        }
        return hash.ToHashCode();
    }

    public int GetFirstValidRecipe(ConveyedResource[][] inputResources)
    {
        // for each recipe in the list, for each ingredient in the recipe, check if the inputResources has at least the same amount of resources
        // if it does, return the index of the recipe
        for (int i = 0; i < list.Count; i++)
        {
            Recipe recipe = list[i];
            bool validRecipe = true;
            foreach (Ingredient ingredient in recipe.ingredients)
            {
                int ingredientAmount = 0;
                foreach (ConveyedResource resource in inputResources[ingredient.channel])
                {
                    if (resource != null && resource.materialType == ingredient.materialType) ingredientAmount++;
                }
                if (ingredientAmount < ingredient.amount)
                {
                    validRecipe = false;
                    break; // no need to check further, this recipe is not valid
                }
            }
            if (validRecipe) return i;
        }
        return -1; // no valid recipe found
    }
}
