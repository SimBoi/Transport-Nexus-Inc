using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

// A simple interface for objects that can be saved and restored
// in case the object shouldn't be instantiated on load, custom logic for finding or creating the object should be implemented in the LoadGame method
public interface ISavable
{
    int ID { get; set; }
    bool ShouldInstantiateOnLoad();
    string GetStateJson();
    void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup);
}

[System.Serializable]
public class CombinedState
{
    public string baseState;
    public string inheritedState;
}

[System.Serializable]
public class SerializableTransform
{
    public float[] position;
    public float[] rotation;
    public float[] scale;

    public Vector3 GetPosition() => new Vector3(position[0], position[1], position[2]);
    public Quaternion GetRotation() => new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]);
    public Vector3 GetScale() => new Vector3(scale[0], scale[1], scale[2]);
}

[System.Serializable]
public class SavebleEntry
{
    public int id;
    public SerializableTransform transform;
    public string type;
    public bool shouldInstantiateOnLoad;
    public string stateJson;
}

[System.Serializable]
public class SaveData
{
    // ISavable objects state
    public List<SavebleEntry> savables = new List<SavebleEntry>();

    // GameManger state
    public int[] materials;
    public List<(Vector2Int tile, Vector2Int orientation, int structureId)> tiles = new();
    public List<(int port1Id, int port2Id)> portConnections = new();
    public List<int> channelIds = new();
    public List<int> trainIds = new();
}

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }
    private int nextId = 0;
    public int GenerateUniqueId() => nextId++;

    public string SaveFileName = "save.json";

    // A registry that maps type names to prefabs
    // Should be populated in the editor
    public List<GameObject> savablePrefabs;
    private Dictionary<string, GameObject> prefabRegistry = new();

    private void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        Instance = this;

        // Build the registry using the prefab's component type name as key
        foreach (GameObject prefab in savablePrefabs)
        {
            ISavable savable = prefab.GetComponent<ISavable>();
            if (savable != null)
            {
                prefabRegistry[savable.GetType().ToString()] = prefab;
            }
        }
    }

    public void SaveGame()
    {
        SaveData saveData = new SaveData();
        // Find all objects that implement ISavable in the scene and save their state
        List<ISavable> saveables = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ISavable>().ToList();

        // Save GameManager state
        GameManager.Instance.SaveState(saveData, saveables);

        // Save ISavable objects state
        foreach (ISavable savable in saveables)
        {
            SavebleEntry entry = new SavebleEntry
            {
                id = savable.ID,
                transform = savable.ShouldInstantiateOnLoad() ? new SerializableTransform
                {
                    position = new float[] { ((MonoBehaviour)savable).transform.position.x, ((MonoBehaviour)savable).transform.position.y, ((MonoBehaviour)savable).transform.position.z },
                    rotation = new float[] { ((MonoBehaviour)savable).transform.rotation.x, ((MonoBehaviour)savable).transform.rotation.y, ((MonoBehaviour)savable).transform.rotation.z, ((MonoBehaviour)savable).transform.rotation.w },
                    scale = new float[] { ((MonoBehaviour)savable).transform.localScale.x, ((MonoBehaviour)savable).transform.localScale.y, ((MonoBehaviour)savable).transform.localScale.z }
                } : new SerializableTransform(),
                type = savable.GetType().ToString(),
                shouldInstantiateOnLoad = savable.ShouldInstantiateOnLoad(),
                stateJson = savable.GetStateJson()
            };
            saveData.savables.Add(entry);
        }

        // Write the save data to a file
        string finalJson = JsonConvert.SerializeObject(saveData, Formatting.Indented);
        string path = Path.Combine(Application.persistentDataPath, SaveFileName);
        File.WriteAllText(path, finalJson);
    }

    public void LoadGame()
    {
        string path = Path.Combine(Application.persistentDataPath, SaveFileName);
        if (!File.Exists(path)) return;
        string fileJson = File.ReadAllText(path);
        SaveData saveData = JsonConvert.DeserializeObject<SaveData>(fileJson);

        // Phase 1: Instantiate all ISavable objects that should be instantiated on load
        Dictionary<int, ISavable> idLookup = new Dictionary<int, ISavable>();
        foreach (SavebleEntry entry in saveData.savables)
        {
            if (!entry.shouldInstantiateOnLoad) continue;

            GameObject obj = Instantiate(prefabRegistry[entry.type], entry.transform.GetPosition(), entry.transform.GetRotation());
            obj.transform.localScale = entry.transform.GetScale();
            idLookup[entry.id] = obj.GetComponent<ISavable>();
            idLookup[entry.id].ID = entry.id;

            // save the prefab for structures
            var structure = obj.GetComponent<Structures.Structure>();
            if (structure != null) structure.prefab = prefabRegistry[entry.type];
        }

        // Phase 2: Non instantiated ISavable objects, custom logic should be implemented here for each type
        foreach (SavebleEntry entry in saveData.savables)
        {
            if (entry.shouldInstantiateOnLoad) continue;

            if (entry.type == typeof(Signals.Port).ToString())
            {
                // find the port using its name and the id of the structure it belongs to (stored in the stateJson)
                (int _, string name, int structureId) = JsonConvert.DeserializeObject<(int, string, int)>(entry.stateJson);
                idLookup[entry.id] = ((MonoBehaviour)idLookup[structureId]).gameObject.GetComponentsInChildren<Signals.Port>().First(p => p.name == name);
                idLookup[entry.id].ID = entry.id;
            }
            else if (entry.type == typeof(Signals.Channel).ToString())
            {
                idLookup[entry.id] = new Signals.Channel();
                idLookup[entry.id].ID = entry.id;
            }
        }

        // Phase 3: Restore GameManager state
        GameManager.Instance.RestoreState(saveData, idLookup);

        // Phase 4: Restore state for all ISavable objects
        foreach (SavebleEntry entry in saveData.savables) idLookup[entry.id].RestoreStateJson(entry.stateJson, idLookup);

        // restore the id generator
        nextId = 0;
        foreach (SavebleEntry entry in saveData.savables) nextId = Mathf.Max(nextId, entry.id + 1);
    }
}
