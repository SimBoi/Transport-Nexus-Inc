using System.Collections.Generic;
using UnityEngine;
using Inventories;

public class PrefabRegistries : MonoBehaviour
{
    public static PrefabRegistries Instance { get; private set; }

    // prefabs
    [SerializeField] private List<GameObject> savablePrefabs;
    [SerializeField] private List<GameObject> materialPrefabs;

    // registries
    [HideInInspector] public Dictionary<string, GameObject> savables = new();
    [HideInInspector] public Dictionary<Materials, GameObject> materials = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Build the registry using the prefab's component type name as key
        foreach (GameObject prefab in savablePrefabs)
        {
            ISavable savable = prefab.GetComponent<ISavable>();
            if (savable == null)
            {
                Debug.LogError($"Prefab {prefab.name} does not have an ISavable component.");
                continue;
            }
            savables[savable.TypeName] = prefab;
        }

        // Build the registry using the material type as key
        foreach (GameObject prefab in materialPrefabs)
        {
            ConveyedResource conveyedResource = prefab.GetComponent<ConveyedResource>();
            if (conveyedResource == null)
            {
                Debug.LogError($"Prefab {prefab.name} does not have a ConveyedResource component.");
                continue;
            }
            Materials material = conveyedResource.materialType;
            materials[material] = prefab;
        }
    }
}
