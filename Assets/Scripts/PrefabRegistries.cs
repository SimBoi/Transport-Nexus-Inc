using System.Collections.Generic;
using UnityEngine;
using Inventories;

public class PrefabRegistries : MonoBehaviour
{
    public static PrefabRegistries Instance { get; private set; }

    // prefabs
    [SerializeField] private List<GameObject> savablePrefabs; // TODO clean up duplicate registries in the code base
    [SerializeField] private List<GameObject> materialPrefabs;
    [SerializeField] private List<GameObject> resourcePrefabs; // TODO create better naming to avoid confusion between item, resource, conveyedResource, material, etc

    // registries
    [HideInInspector] public Dictionary<string, GameObject> savables = new();
    [HideInInspector] public Dictionary<Materials, GameObject> materials = new();
    [HideInInspector] public Dictionary<ResourceNode, GameObject> resources = new();

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

        // TODO Build the registry using the resource nodes as key
    }
}
