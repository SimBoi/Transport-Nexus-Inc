using UnityEngine;
using System.Collections.Generic;

public class ChunksManager : MonoBehaviour
{
    [SerializeField] private GameObject chunkPrefab;
    private Dictionary<Vector2Int, Chunk> chunks = new();
    [SerializeField] private Transform center;
    public int generateDistance;
    public int renderDistance;
    public int seed;

    private void Update()
    {
        // load chunks
        for (int x = -generateDistance; x <= generateDistance; x++) for (int z = -generateDistance; z <= generateDistance; z++)
        {
            Vector2Int chunkCoords = new Vector2Int(Mathf.FloorToInt((center.position.x + x) / Chunk.size), Mathf.FloorToInt((center.position.z + z) / Chunk.size));
            if (!chunks.ContainsKey(chunkCoords))
            {
                Chunk chunk = Instantiate(chunkPrefab, new Vector3(chunkCoords.x * Chunk.size, 0, chunkCoords.y * Chunk.size), Quaternion.identity).GetComponent<Chunk>();
                chunk.GenerateDataAsync(seed, chunkCoords);
                chunks.Add(chunkCoords, chunk);
            }
            if (-renderDistance <= x && x <= renderDistance)
            {
                chunks[chunkCoords].GenerateMeshAsync();
            }
        }

        // TODO unload chunks
    }
}