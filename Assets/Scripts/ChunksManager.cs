using UnityEngine;
using System.Collections.Generic;

public enum Biome
{
    LushPlains
}

public class ThreadSafeMesh
{
    public List<Vector3> vertices;
    public List<List<int>> submeshTriangles;
    public List<Material> materials;
    // TODO add/modify the data in this class accordingly

    public void Combine(ThreadSafeMesh other, Vector3 offset)
    {
        // TODO combine two ThreadSafeMeshes
    }
}

public class ChunksManager : MonoBehaviour
{
    public static ChunksManager instance { get; private set; }
    [SerializeField] private GameObject chunkPrefab;
    private Dictionary<Vector2Int, Chunk> chunks = new();
    [SerializeField] private Transform center;
    public int generateDistance;
    public int renderDistance;
    public int seed;

    [SerializeField] private GameObject[][] lushPlainsTilePrefabs;
    [SerializeField] private GameObject[] lushPlainsVegetationPrefabs;
    [HideInInspector] public ThreadSafeMesh[][] lushPlainsTiles { get; private set; }
    [HideInInspector] public ThreadSafeMesh[] lushPlainsVegetation { get; private set; }

    private void Start()
    {
        // TODO extract the meshes from the prefabs on startup
    }

    private void Update()
    {
        // load chunks
        for (int x = -generateDistance; x <= generateDistance; x++)
        for (int z = -generateDistance; z <= generateDistance; z++)
        {
            Vector2Int chunkCoords = new Vector2Int
            (
                Mathf.FloorToInt((center.position.x + x) / Chunk.size),
                Mathf.FloorToInt((center.position.z + z) / Chunk.size) 
            );
            if (!chunks.ContainsKey(chunkCoords))
            {
                Chunk chunk = Instantiate
                (
                    chunkPrefab,
                    new Vector3(chunkCoords.x * Chunk.size, 0, chunkCoords.y * Chunk.size),
                    Quaternion.identity
                ).GetComponent<Chunk>();
                chunk.GenerateDataAsync(seed, chunkCoords);
                chunks.Add(chunkCoords, chunk);
            }
            if (-renderDistance <= x && x <= renderDistance)
            {
                chunks[chunkCoords].GenerateMeshAsync(chunkCoords);
            }
        }

        // TODO unload chunks
    }
}