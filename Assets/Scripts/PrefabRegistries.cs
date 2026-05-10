using System.Collections.Generic;
using UnityEngine;
using Inventories;

public class PrefabRegistries : MonoBehaviour
{
    public static PrefabRegistries Instance { get; private set; }

    // prefabs
    [SerializeField] private List<GameObject> savablePrefabs;
    [SerializeField] private List<GameObject> resourcePrefabs;

    // registries
    [HideInInspector] public Dictionary<string, GameObject> savables = new();
    [HideInInspector] public Dictionary<ResourceType, GameObject> resources = new();

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

        // Build the registry using the resource type as key
        foreach (GameObject prefab in resourcePrefabs)
        {
            ResourceEntity resource = prefab.GetComponent<ResourceEntity>();
            if (resource == null)
            {
                Debug.LogError($"Prefab {prefab.name} does not have a ResourceEntity component.");
                continue;
            }
            ResourceType resourceType = resource.resourceType;
            resources[resourceType] = prefab;
        }

        // TODO Build the registry using the resource nodes as key
    }
}
