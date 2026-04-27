using System;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    public const int size = 8;
    private Biome[,] biomeMap = new Biome[size, size];
    private int[,] heightMap = new int[size, size];
    private bool[,] vegetationMap = new bool[size, size];
    private int[,] resourceNodeMap = new int[size, size];
    private int[,] tileVariationMap = new int[size, size];
    private int[,] vegetationVariationMap = new int[size, size];
    private int[,] resourceNodeVariationMap = new int[size, size];
    private bool dataReady = false;
    private bool meshReady = false;
    private Awaitable dataGenerationTask = null;
    private Awaitable meshGenerationTask = null;
    private Mesh tilesMesh;
    private Mesh vegetationMesh;
    private Mesh resourceNodesMesh;
    [SerializeField] GameObject tilesGameObject;
    [SerializeField] GameObject vegetationGameObject;
    [SerializeField] GameObject resourceNodesGameObjects;

    public void Awake()
    {
        tilesMesh = new Mesh();
        vegetationMesh = new Mesh();
        resourceNodesMesh = new Mesh();
    }

    public void Clear()
    {
        tilesMesh.Clear();
        tilesGameObject.GetComponent<MeshFilter>().sharedMesh = null;
        vegetationGameObject.GetComponent<MeshFilter>().sharedMesh = null;
        resourceNodesGameObjects.GetComponent<MeshFilter>().sharedMesh = null;
        tilesGameObject.GetComponent<MeshRenderer>().materials = null;
        vegetationGameObject.GetComponent<MeshRenderer>().materials = null;
        resourceNodesGameObjects.GetComponent<MeshRenderer>().materials = null;
        if (!dataReady && dataGenerationTask != null) dataGenerationTask.Cancel();
        if (!meshReady && meshGenerationTask != null) meshGenerationTask.Cancel();
        dataGenerationTask = null;
        meshGenerationTask = null;
    }

    public async Awaitable GenerateDataAsync(int seed, Vector2Int chunkCoords)
    {
        if (dataReady) return;
        dataGenerationTask ??= GenerateDataAsyncAux(seed, chunkCoords);
        try { await dataGenerationTask; }
        catch (OperationCanceledException) {}
    }

    public async Awaitable GenerateDataAsyncAux(int seed, Vector2Int chunkCoords)
    {
        await Awaitable.BackgroundThreadAsync();

        // generate biome data
        Vector2Int biomeSeed = GetVec2Hash(seed + 1);
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
        Vector2Int heightSeed1 = GetVec2Hash(seed + 2);
        Vector2Int heightSeed2 = GetVec2Hash(seed + 3);
        Vector2Int heightSeed3 = GetVec2Hash(seed + 4);
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
                heightMap[x, z] = Mathf.FloorToInt((noise1 + noise2 + noise3) / scaleSum * ChunksManager.instance.lushPlainsTiles.Length);
            }
        }

        // generate vegetation data
        Vector2Int vegetationSeed1 = GetVec2Hash(seed + 5);
        Vector2Int vegetationSeed2 = GetVec2Hash(seed + 6);
        Vector2Int vegetationSeed3 = GetVec2Hash(seed + 7);
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
                float noise1 = Mathf.PerlinNoise(tileCoords.x * freq1 + vegetationSeed1.x, tileCoords.y * freq1 + vegetationSeed1.y) * scale1;
                float noise2 = Mathf.PerlinNoise(tileCoords.x * freq2 + vegetationSeed2.x, tileCoords.y * freq2 + vegetationSeed2.y) * scale2;
                float noise3 = Mathf.PerlinNoise(tileCoords.x * freq3 + vegetationSeed3.x, tileCoords.y * freq3 + vegetationSeed3.y) * scale3;
                float finalNoise = (noise1 + noise2 + noise3) / scaleSum;
                vegetationMap[x, z] = finalNoise > .7f;
            }
        } 

        // generate resource node data
        Vector2Int resourceNodeSeed1 = GetVec2Hash(seed + 8);
        Vector2Int resourceNodeSeed2 = GetVec2Hash(seed + 9);
        Vector2Int resourceNodeSeed3 = GetVec2Hash(seed + 10);
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
                float noise1 = Mathf.PerlinNoise(tileCoords.x * freq1 + resourceNodeSeed1.x, tileCoords.y * freq1 + resourceNodeSeed1.y) * scale1;
                float noise2 = Mathf.PerlinNoise(tileCoords.x * freq2 + resourceNodeSeed2.x, tileCoords.y * freq2 + resourceNodeSeed2.y) * scale2;
                float noise3 = Mathf.PerlinNoise(tileCoords.x * freq3 + resourceNodeSeed3.x, tileCoords.y * freq3 + resourceNodeSeed3.y) * scale3;
                resourceNodeMap[x, z] = Mathf.FloorToInt((noise1 + noise2 + noise3) / scaleSum * 5);
            }
        }

        // generate variations for tiles, vegetation and resources
        Dictionary<int, int[,]> hashMaps = new();
        for (int i = 0; i < 3; i++)
        {
            hashMaps.Add(seed + i, new int[size, size]);
            for (int x = 0; x < size; x++)
            for (int z = 0; z < size; z++)
            {
                Vector2Int tileCoords = chunkCoords * size + new Vector2Int(x, z);
                hashMaps[seed + i][x, z] = GetIntHash(seed + i, tileCoords.x, tileCoords.y);
            }
        }
        for (int x = 0; x < size; x++)
        for (int z = 0; z < size; z++)
        {
            int randTile = hashMaps[seed][x, z] % ChunksManager.instance.lushPlainsTiles[heightMap[x, z]].Length;
            int randVegetation = hashMaps[seed + 1][x, z] % 5;
            int randResourceNode = hashMaps[seed + 2][x, z] % ChunksManager.instance.lushPlainsResourceNodes[]

            tileVariationMap[x, z] = randTile;
            vegetationVariationMap[x, z] = randVegetation < ChunksManager.instance.lushPlainsVegetation.Length ? randVegetation : -1;
        }

        dataReady = true;
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

    public async Awaitable GenerateMeshAsync(Vector2Int chunkCoords)
    {
        if (meshReady) return;
        meshGenerationTask ??= GenerateMeshAsyncAux(chunkCoords);
        try { await meshGenerationTask; } 
        catch (OperationCanceledException) {}
    }

    public async Awaitable GenerateMeshAsyncAux(Vector2Int chunkCoords)
    {
        // combine the tile meshes on a background thread
        await Awaitable.BackgroundThreadAsync();
        if (!dataReady) await dataGenerationTask;
        ThreadSafeMesh threadSafeTilesMesh = null;
        ThreadSafeMesh threadSafeVegetationMesh = null;
        for (int x = 0; x < size; x++)
        for (int z = 0; z < size; z++)
        {
            Vector3 tileOffset = new(x, 0, z);

            ThreadSafeMesh tileMesh = ChunksManager.instance.lushPlainsTiles[heightMap[x, z]][tileVariationMap[x, z]];
            if (threadSafeTilesMesh == null) threadSafeTilesMesh = new(tileMesh, tileOffset);
            else threadSafeTilesMesh.Combine(tileMesh, tileOffset);
        
            if (vegetationVariationMap[x, z] > -1)
            {
                ThreadSafeMesh singleVegetationMesh = ChunksManager.instance.lushPlainsVegetation[vegetationVariationMap[x, z]];
                Vector3 vegetationOffset = tileOffset + new Vector3(0, tileMesh.MaxY, 0);
                if (threadSafeVegetationMesh == null) threadSafeVegetationMesh = new(singleVegetationMesh, vegetationOffset);
                else threadSafeVegetationMesh.Combine(singleVegetationMesh, vegetationOffset);
            }
        }

        // convert to unity mesh on the main thread
        await Awaitable.MainThreadAsync();
        threadSafeTilesMesh.ConvertToUnityMesh(tilesMesh, out int[] tilesMaterialIds);
        threadSafeVegetationMesh.ConvertToUnityMesh(vegetationMesh, out int[] vegetationMaterialIds);
        tilesGameObject.GetComponent<MeshFilter>().sharedMesh = tilesMesh;
        tilesGameObject.GetComponent<MeshRenderer>().materials = ChunksManager.instance.GetMaterials(tilesMaterialIds);
        if (vegetationMesh != null)
        {
            vegetationGameObject.GetComponent<MeshRenderer>().materials = ChunksManager.instance.GetMaterials(vegetationMaterialIds);
            vegetationGameObject.GetComponent<MeshFilter>().sharedMesh = vegetationMesh;
        }

        meshReady = true;
    }

    private static int GetIntHash(int x, int y, int z)
    {
        unchecked
        {
            const int A = (int)0x9e3779b1;
            const int B = (int)0x85ebca77;
            const int C = (int)0xc2b2ae3d;
            const int D = (int)0x7feb352d;
            const int E = (int)0x846ca68b;

            int hash = x * A ^ y * B ^ z * C;
            hash ^= hash >> 16;
            hash *= D;
            hash ^= hash >> 15;
            hash *= E;
            hash ^= hash >> 16;

            return hash < 0 ? -(hash + 1) : hash;
        }
    }

    Vector2Int GetVec2Hash(int x)
    {
        unchecked
        {
            const int A = unchecked((int)0x9E3779B9);
            const int B = unchecked((int)0x7F4A7C15);
            const int C = unchecked((int)0x94D049BB);
            const int D = unchecked((int)0xED5AD4BB);
            const int E = unchecked((int)0xAC4C1B51);
            const int F = unchecked((int)0x31848BAB);

            int h1 = x * A;
            h1 ^= h1 >> 16;
            h1 *= B;
            h1 ^= h1 >> 13;
            h1 *= C;
            h1 ^= h1 >> 16;

            int h2 = x * D;
            h2 ^= h2 >> 15;
            h2 *= E;
            h2 ^= h2 >> 14;
            h2 *= F;
            h2 ^= h2 >> 15;

            if (h1 < 0) h1 = -(h1 + 1);
            if (h2 < 0) h2 = -(h2 + 1);

            return new Vector2Int(h1, h2);
        }
    }
}