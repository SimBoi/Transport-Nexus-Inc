using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Udar.SceneManager;
using UnityEngine.SceneManagement;
using System;
using Structures;
using Signals;

// TODO: future improvement avoid having to implement custom logic whenever I have an ISavable as a property of another ISavable / a child of a prefab that wont be directly instantiated. potentially make 3 classes: ISavable (root monobehaviours in prefabs), ISavableChild (child monobehaviours in prefabs) and ISavableProperty (non monobehaviours), avoid repeating the CombinedState code by finding a way to reuse it
// A simple interface for objects that can be saved and restored
// in case the object shouldn't be instantiated on load, custom logic for finding or creating the object should be implemented in the LoadGame method
public interface ISavable
{
    int ID { get; set; }
    string TypeName { get; }
    bool ShouldInstantiateOnLoad();
    string GetStateJson();
    void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup);
}

public interface ISavableProperty
{
    string GetStateJson();
    void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup);
}

[Serializable]
public class SaveMetadata
{
    public float playtime;
}

[Serializable]
public class CombinedState
{
    public string baseState;
    public string inheritedState;
}

[Serializable]
public class SerializableTransform
{
    public float[] position;
    public float[] rotation;
    public float[] scale;

    public Vector3 GetPosition() => new(position[0], position[1], position[2]);
    public Quaternion GetRotation() => new(rotation[0], rotation[1], rotation[2], rotation[3]);
    public Vector3 GetScale() => new(scale[0], scale[1], scale[2]);
}

[Serializable]
public class SavebleEntry
{
    public int id;
    public SerializableTransform transform;
    public string type;
    public bool shouldInstantiateOnLoad;
    public string stateJson;
}

[Serializable]
public class SerializableTile
{
    public Vector2Int tile;
    public Vector2Int orientation;
    public int structureId;
}

[Serializable]
public class SerializablePortConnection
{
    public int port1Id;
    public int port2Id;
}

[Serializable]
public class SaveData
{
    public SaveMetadata metadata;
    // ISavable objects state
    public List<SavebleEntry> savables = new();

    // GameManger state
    public ulong tick;
    public int[] resources;
    public List<SerializableTile> tiles = new();
    public List<SerializablePortConnection> portConnections = new();
    public List<int> channelIds = new();
    public List<int> trainIds = new();
}

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }
    private int nextId = 0;
    public int GenerateUniqueId() => nextId++;
    [SerializeField] private SceneField menuScene;
    [SerializeField] private SceneField gameScene;
    public int LoadedSaveSlot { get; private set; } = -1;
    private SaveMetadata loadedSaveMetadata = null;
    private float sessionPlaytime = 0;

    public string SaveFileName = "save.json";

    private void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void FixedUpdate()
    {
        sessionPlaytime += Time.fixedDeltaTime;
    }

    public SaveMetadata GetSaveMetadata(int saveSlot)
    {
        string path = Path.Combine(Application.persistentDataPath, $"{SaveFileName}_{saveSlot}.json");
        if (!File.Exists(path)) return null;
        string fileJson = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<SaveData>(fileJson).metadata;
    }

    public void SaveGame()
    {
        SaveData saveData = new();

        // Find all MonoBehaviour ISavable objects in the scene and save their state
        List<ISavable> saveables = FindObjectsByType<MonoBehaviour>().OfType<ISavable>().ToList();

        // Find all non MonoBehaviour ISavable objects in the scene and save their state
        // Sigal Channels
        HashSet<Channel> signalChannels = new();
        foreach (ISavable savable in saveables) if (savable is Port port && port.signalChannel != null) signalChannels.Add(port.signalChannel);
        foreach (Channel signalChannel in signalChannels) saveables.Add(signalChannel);

        // Save GameManager state
        GameManager.Instance.SaveState(saveData);

        // Save ISavable objects state
        foreach (ISavable savable in saveables)
        {
            SavebleEntry entry = new()
            {
                id = savable.ID,
                transform = savable.ShouldInstantiateOnLoad() ? new SerializableTransform
                {
                    position = new float[] { ((MonoBehaviour)savable).transform.position.x, ((MonoBehaviour)savable).transform.position.y, ((MonoBehaviour)savable).transform.position.z },
                    rotation = new float[] { ((MonoBehaviour)savable).transform.rotation.x, ((MonoBehaviour)savable).transform.rotation.y, ((MonoBehaviour)savable).transform.rotation.z, ((MonoBehaviour)savable).transform.rotation.w },
                    scale = new float[] { ((MonoBehaviour)savable).transform.localScale.x, ((MonoBehaviour)savable).transform.localScale.y, ((MonoBehaviour)savable).transform.localScale.z }
                } : new SerializableTransform(),
                type = savable.TypeName,
                shouldInstantiateOnLoad = savable.ShouldInstantiateOnLoad(),
                stateJson = savable.GetStateJson()
            };
            saveData.savables.Add(entry);
        }

        // update save metadata
        loadedSaveMetadata.playtime += sessionPlaytime;
        sessionPlaytime = 0;
        saveData.metadata = loadedSaveMetadata;

        // Write the save data to a file
        string finalJson = JsonConvert.SerializeObject(saveData, Formatting.Indented);
        string path = Path.Combine(Application.persistentDataPath, $"{SaveFileName}_{LoadedSaveSlot}.json");
        File.WriteAllText(path, finalJson);
    }

    public void StartNewGame(int saveSlot)
    {
        sessionPlaytime = 0;
        LoadedSaveSlot = saveSlot;
        loadedSaveMetadata = new()
        {
            playtime = 0
        };
        SceneManager.LoadScene(gameScene.Name);
    }

    public async Awaitable LoadGameAsync(int saveSlot)
    {
        sessionPlaytime = 0;
        LoadedSaveSlot = saveSlot;

        await SceneManager.LoadSceneAsync(gameScene.Name);
        await Awaitable.EndOfFrameAsync();
        print(PrefabRegistries.Instance.ToString());

        string path = Path.Combine(Application.persistentDataPath, $"{SaveFileName}_{saveSlot}.json");
        if (!File.Exists(path)) throw new Exception("save file doesnt exist");
        string fileJson = File.ReadAllText(path);
        SaveData saveData = JsonConvert.DeserializeObject<SaveData>(fileJson);
        loadedSaveMetadata = saveData.metadata;

        // Phase 1: Instantiate all ISavable objects that should be instantiated on load
        Dictionary<int, ISavable> idLookup = new();
        foreach (SavebleEntry entry in saveData.savables)
        {
            if (!entry.shouldInstantiateOnLoad) continue;

            GameObject obj = Instantiate(PrefabRegistries.Instance.savables[entry.type], entry.transform.GetPosition(), entry.transform.GetRotation());
            obj.transform.localScale = entry.transform.GetScale();
            idLookup[entry.id] = obj.GetComponent<ISavable>();
            idLookup[entry.id].ID = entry.id;

            // save the prefab for structures
            if (obj.TryGetComponent<StructureEntity>(out var structure))
                structure.prefab = PrefabRegistries.Instance.savables[entry.type];
        }

        // Phase 2: Non instantiated ISavable objects, custom logic should be implemented here for each type
        foreach (SavebleEntry entry in saveData.savables)
        {
            if (entry.shouldInstantiateOnLoad) continue;

            // custom logic for finding/instantiating the object
            if (entry.type == typeof(Port).ToString())
            {
                // find the port using its name and the id of the structure it belongs to (stored in the stateJson)
                (int _, string name, int structureId) = JsonConvert.DeserializeObject<(int, string, int)>(entry.stateJson);
                idLookup[entry.id] = ((MonoBehaviour)idLookup[structureId]).gameObject.GetComponentsInChildren<Port>().First(p => p.name == name);
            }
            else if (entry.type == typeof(Channel).ToString())
            {
                idLookup[entry.id] = new Channel();
            }
            else if (entry.type == typeof(ChunksManager).ToString())
            {
                idLookup[entry.id] = ChunksManager.instance;
            }

            idLookup[entry.id].ID = entry.id;
        }

        // Phase 3: Restore GameManager state
        GameManager.Instance.RestoreState(saveData, idLookup);

        // Phase 4: Restore state for all ISavable objects
        foreach (SavebleEntry entry in saveData.savables) idLookup[entry.id].RestoreStateJson(entry.stateJson, idLookup);

        // restore the id generator
        nextId = 0;
        foreach (SavebleEntry entry in saveData.savables) nextId = Mathf.Max(nextId, entry.id + 1);
    }

    public void ExitToMainMenu()
    {
        sessionPlaytime = 0;
        LoadedSaveSlot = -1;
        loadedSaveMetadata = null;
        SceneManager.LoadScene(menuScene.Name);
    }

    public void SaveAndExitToMainMenu()
    {
        SaveGame();
        ExitToMainMenu();
    }
}
