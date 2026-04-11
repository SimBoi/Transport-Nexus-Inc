using System.Collections.Generic;
using UnityEngine;

public enum Biome
{
    LushPlains
}

public class TileMesh
{
    public Vector3[] vertices;
    public int[][] submeshTriangles;
    public Material[] materials;
}

public class Chunk : MonoBehaviour
{
    public static int size = 8;
    private Biome[,] biomeMap = new Biome[size, size];
    private int[,] heightMap = new int[size, size];
    private int[,] vegetationMap = new int[size, size];
    private Awaitable dataGenerationTask = null;
    private Awaitable meshGenerationTask = null;

    public Awaitable GenerateDataAsync(int seed, Vector2Int chunkCoords)
    {
        if (dataGenerationTask == null) dataGenerationTask = GenerateDataAsyncAux(seed, chunkCoords);
        return dataGenerationTask;
    }

    public async Awaitable GenerateDataAsyncAux(int seed, Vector2Int chunkCoords)
    {
        await Awaitable.BackgroundThreadAsync();

        // generate biome data
        Vector2Int biomeSeed = new(seed * 41, seed * 14);
        for (int x = 0; x < size; x++) for (int z = 0; z < size; z++)
        {
            Vector2Int tileCoords = chunkCoords * size + new Vector2Int(x, z);
            float freq = 1;
            float noise = Mathf.PerlinNoise(tileCoords.x * freq + biomeSeed.x, tileCoords.y * freq + biomeSeed.y);
            if (noise <= 1)
            {
                biomeMap[x, z] = Biome.LushPlains;
            }
        }

        // generate height data
        Vector2Int heightSeed1 = new(seed * 17, seed * 34);
        Vector2Int heightSeed2 = new(seed * 41, seed * 6);
        Vector2Int heightSeed3 = new(seed * 19, seed * 5);
        for (int x = 0; x < size; x++) for (int z = 0; z < size; z++)
        {
            if (biomeMap[x, z] == Biome.LushPlains)
            {
                Vector2Int tileCoords = chunkCoords * size + new Vector2Int(x, z);
                float freq1 = 1;
                float freq2 = 2;
                float freq3 = 3;
                float scale1 = 3;
                float scale2 = 2;
                float scale3 = 1;
                float scaleSum = scale1 + scale2 + scale3;
                float noise1 = Mathf.PerlinNoise(tileCoords.x * freq1 + heightSeed1.x, tileCoords.y * freq1 + heightSeed1.y) * scale1;
                float noise2 = Mathf.PerlinNoise(tileCoords.x * freq2 + heightSeed2.x, tileCoords.y * freq2 + heightSeed2.y) * scale2;
                float noise3 = Mathf.PerlinNoise(tileCoords.x * freq3 + heightSeed3.x, tileCoords.y * freq3 + heightSeed3.y) * scale3;
                heightMap[x, z] = Mathf.FloorToInt((noise1 + noise2 + noise3) / scaleSum * 5);
            }
        }

        // generate vegetation
        Dictionary<int, int[,]> hashMaps = new();
        for (int i = 0; i < 1; i++)
        {
            hashMaps.Add(seed + i, new int[size, size]);
            for (int x = 0; x < size; x++) for (int z = 0; z < size; z++)
            {
                Vector2Int tileCoords = chunkCoords * size + new Vector2Int(x, z);
                hashMaps[seed + i][x, z] = GetTileHash(seed + i, tileCoords.x, tileCoords.y);
            }
        }
        for (int x = 0; x < size; x++) for (int z = 0; z < size; z++)
        {
            if (hashMaps[seed][x, z] % 5 == 0)
            {
                vegetationMap[x, z] = 1;
            }
        }
    }

    public Awaitable GenerateMeshAsync()
    {
        if (meshGenerationTask == null) meshGenerationTask = GenerateMeshAsyncAux();
        return meshGenerationTask;
    }

    public async Awaitable GenerateMeshAsyncAux()
    {
        await dataGenerationTask;
        await Awaitable.BackgroundThreadAsync();
        // TODO generate the mesh
    }

    int GetTileHash(int seed, int x, int z)
    {
        unchecked
        {
            const int A = (int)0x9e3779b1;
            const int B = (int)0x85ebca77;
            const int C = (int)0xc2b2ae3d;
            const int D = (int)0x7feb352d;
            const int E = (int)0x846ca68b;

            int hash = x * A ^ z * B ^ seed * C;
            hash ^= hash >> 16;
            hash *= D;
            hash ^= hash >> 15;
            hash *= E;
            hash ^= hash >> 16;
            return hash;
        }
    }
}