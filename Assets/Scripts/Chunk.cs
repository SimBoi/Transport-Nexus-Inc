using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class Chunk : MonoBehaviour
{
    public const int size = 8;
    private Biome[,] biomeMap = new Biome[size, size];
    private int[,] heightMap = new int[size, size];
    private int[,] tileVariationMap = new int[size, size];
    private int[,] vegetationVariationMap = new int[size, size];  // -1 means no vegetation on the tile
    private Awaitable dataGenerationTask = null;
    private Awaitable meshGenerationTask = null;
    private Mesh mesh;

    public void Awake()
    {
        mesh = new Mesh();
    }

    public void Clear()
    {
        mesh.Clear();
        GetComponent<MeshFilter>().sharedMesh = null;
        GetComponent<MeshRenderer>().materials = new Material[0];
        if (dataGenerationTask != null) dataGenerationTask.Cancel();
        if (meshGenerationTask != null) meshGenerationTask.Cancel();
        dataGenerationTask = null;
        meshGenerationTask = null;
    }

    public Awaitable GenerateDataAsync(int seed, Vector2Int chunkCoords)
    {
        dataGenerationTask ??= GenerateDataAsyncAux(seed, chunkCoords);
        return dataGenerationTask;
    }

    public async Awaitable GenerateDataAsyncAux(int seed, Vector2Int chunkCoords)
    {
        await Awaitable.BackgroundThreadAsync();

        // generate biome data
        Vector2Int biomeSeed = new(seed * 41, seed * 14);
        for (int x = 0; x < size; x++)
        for (int z = 0; z < size; z++)
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
        for (int x = 0; x < size; x++)
        for (int z = 0; z < size; z++)
        {
            if (biomeMap[x, z] == Biome.LushPlains)
            {
                Vector2Int tileCoords = chunkCoords * size + new Vector2Int(x, z);
                float freq1 = .1f;
                float freq2 = .2f;
                float freq3 = .3f;
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

        // generate tile variations and vegetation
        Dictionary<int, int[,]> hashMaps = new();
        for (int i = 0; i < 2; i++)
        {
            hashMaps.Add(seed + i, new int[size, size]);
            for (int x = 0; x < size; x++)
            for (int z = 0; z < size; z++)
            {
                Vector2Int tileCoords = chunkCoords * size + new Vector2Int(x, z);
                hashMaps[seed + i][x, z] = GetTileHash(seed + i, tileCoords.x, tileCoords.y);
            }
        }
        for (int x = 0; x < size; x++)
        for (int z = 0; z < size; z++)
        {
            int randTile = hashMaps[seed][x, z] % ChunksManager.instance.lushPlainsTiles[heightMap[x, z]].Length;
            int randVegetation = hashMaps[seed + 1][x, z] % 5;

            tileVariationMap[x, z] = randTile;
            vegetationVariationMap[x, z] = randVegetation < ChunksManager.instance.lushPlainsVegetation.Length ? randVegetation : -1;
        }
    }

    private static void Print2DArray<T>(T[,] array)
    {
        string s = "";
        for (int i = 0; i < array.GetLength(0); i++)
        {
            for (int j = 0; j < array.GetLength(1); j++)
            {
                s += array[i, j].ToString() + "    ";
            }
            s += "\n";
        }
        print(s);
    }

    public Awaitable GenerateMeshAsync(Vector2Int chunkCoords)
    {
        meshGenerationTask ??= GenerateMeshAsyncAux(chunkCoords);
        return meshGenerationTask;
    }

    public async Awaitable GenerateMeshAsyncAux(Vector2Int chunkCoords)
    {
        // combine the tile meshes on a background thread
        await Awaitable.BackgroundThreadAsync();
        await dataGenerationTask;
        ThreadSafeMesh threadSafeMesh = null;
        for (int x = 0; x < size; x++)
        for (int z = 0; z < size; z++)
        {
            Vector3 localTileCoords = new Vector3(x, 0, z);
            ThreadSafeMesh tileMesh = ChunksManager.instance.lushPlainsTiles[heightMap[x, z]][tileVariationMap[x, z]];
            print(heightMap[x, z]);
            if (threadSafeMesh == null) threadSafeMesh = new(tileMesh);
            else threadSafeMesh.Combine(tileMesh, localTileCoords);
        }

        // convert to unity mesh on the main thread
        await Awaitable.MainThreadAsync();
        threadSafeMesh.ConvertToUnityMesh(mesh, out int[] materialIds);
        GetComponent<MeshFilter>().sharedMesh = mesh;
        GetComponent<MeshRenderer>().materials = ChunksManager.instance.GetMaterials(materialIds);
    }

    // get a positive hash from three ints
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

            return hash < 0 ? -(hash + 1) : hash;
        }
    }
}