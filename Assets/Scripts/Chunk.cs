using System;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    public const int size = 12;
    public Vector2Int chunkCoords;
    private Biome[,] biomeMap = new Biome[size, size];
    private int[,] heightMap = new int[size, size];
    private bool[,] vegetationMap = new bool[size, size];
    private ResourceNode[,] resourceNodeMap = new ResourceNode[size, size];
    private int[,] tileVariationMap = new int[size, size];
    private int[,] vegetationVariationMap = new int[size, size];
    private int[,] resourceNodeVariationMap = new int[size, size];
    private bool dataReady = false;
    private bool tilesMeshReady = false;
    private bool vegetationMeshReady = false;
    private bool resourceNodesMeshReady = false;
    private Awaitable dataGenerationTask = null;
    private Awaitable tilesMeshGenerationTask = null;
    private Awaitable vegetationMeshGenerationTask = null;
    private Awaitable resourceNodesMeshGenerationTask = null;
    private Mesh tilesMesh;
    private Mesh vegetationMesh;
    private Mesh resourceNodesMesh;
    [SerializeField] GameObject tilesGameObject;
    [SerializeField] GameObject vegetationGameObject;
    [SerializeField] GameObject resourceNodesGameObject;

    private void Awake()
    {
        tilesMesh = new();
        vegetationMesh = new();
        resourceNodesMesh = new();
    }

    public void Clear()
    {
        ClearData();
        ClearTilesMesh();
        ClearVegetationMesh();
        ClearResourceNodesMesh();
    }

    public void ClearData()
    {
        if (!dataReady && dataGenerationTask != null) dataGenerationTask.Cancel();
       dataGenerationTask = null;
    }

    public void ClearTilesMesh()
    {
        tilesMesh.Clear();
        tilesGameObject.GetComponent<MeshFilter>().sharedMesh = null;
        tilesGameObject.GetComponent<MeshRenderer>().materials = new Material[0];
        if (!tilesMeshReady && tilesMeshGenerationTask != null) tilesMeshGenerationTask.Cancel();
        tilesMeshGenerationTask = null;
    }

    public void ClearVegetationMesh()
    {
        vegetationMesh.Clear();
        vegetationGameObject.GetComponent<MeshFilter>().sharedMesh = null;
        vegetationGameObject.GetComponent<MeshRenderer>().materials = new Material[0];
        if (!vegetationMeshReady && vegetationMeshGenerationTask != null) vegetationMeshGenerationTask.Cancel();
        vegetationMeshGenerationTask = null;
    }

    public void ClearResourceNodesMesh()
    {
        resourceNodesMesh.Clear();
        resourceNodesGameObject.GetComponent<MeshFilter>().sharedMesh = null;
        resourceNodesGameObject.GetComponent<MeshRenderer>().materials = new Material[0];
        if (!resourceNodesMeshReady && resourceNodesMeshGenerationTask != null) resourceNodesMeshGenerationTask.Cancel();
        resourceNodesMeshGenerationTask = null;
    }

    public async Awaitable GenerateDataAsync(int seed, Vector2Int chunkCoords, HashSet<Vector2Int> clearedTiles)
    {
        this.chunkCoords = chunkCoords;
        if (dataReady) return;
        async Awaitable Aux(int seed, HashSet<Vector2Int> clearedTiles)
        {
            await Awaitable.BackgroundThreadAsync();

            // generate biome data
            FastNoiseLite biomeNoise = new(seed);
            biomeNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            biomeNoise.SetFrequency(0.1f);
            for (int x = 0; x < size; x++)
            for (int z = 0; z < size; z++)
            {
                Vector2Int tileCoords = this.chunkCoords * size + new Vector2Int(x, z);
                float freq = 1;
                float noise = biomeNoise.GetNoise(tileCoords.x * freq, tileCoords.y * freq);
                if (noise <= 1)
                {
                    biomeMap[x, z] = Biome.LushPlains;
                }
            }

            // generate height data
            FastNoiseLite lushPlainsHeightNoise = new(seed + 1);
            lushPlainsHeightNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            lushPlainsHeightNoise.SetFrequency(0.03f);
            lushPlainsHeightNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
            lushPlainsHeightNoise.SetFractalOctaves(2);
            for (int x = 0; x < size; x++)
            for (int z = 0; z < size; z++)
            {
                if (biomeMap[x, z] == Biome.LushPlains)
                {
                    Vector2Int tileCoords = this.chunkCoords * size + new Vector2Int(x, z);
                    float noise = lushPlainsHeightNoise.GetNoise(tileCoords.x, tileCoords.y);
                    if (noise < -0.7f) heightMap[x, z] = 0;
                    else if (noise < 0.4f) heightMap[x, z] = 1;
                    else if (noise < 0.5f) heightMap[x, z] = 2;
                    else if (noise < 0.6f) heightMap[x, z] = 3;
                    else heightMap[x, z] = 4;
                }
            }

            // generate vegetation data
            FastNoiseLite vegetationNoise = new(seed + 2);
            vegetationNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            vegetationNoise.SetFrequency(0.1f);
            vegetationNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
            vegetationNoise.SetFractalOctaves(2);
            for (int x = 0; x < size; x++)
                for (int z = 0; z < size; z++)
                {
                    vegetationMap[x, z] = false;
                    Vector2Int tileCoords = this.chunkCoords * size + new Vector2Int(x, z);
                    if (clearedTiles.Contains(tileCoords)) continue;
                    if (biomeMap[x, z] == Biome.LushPlains)
                    {
                        float noise = vegetationNoise.GetNoise(tileCoords.x, tileCoords.y);
                        vegetationMap[x, z] = noise > 0.6f;
                    }
                }

            // generate resource node data
            FastNoiseLite ironNodesNoise = new(seed + 3);
            ironNodesNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            ironNodesNoise.SetFrequency(0.1f);
            ironNodesNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
            ironNodesNoise.SetFractalOctaves(2);
            FastNoiseLite coalNodesNoise = new(seed + 4);
            coalNodesNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            coalNodesNoise.SetFrequency(0.1f);
            coalNodesNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
            coalNodesNoise.SetFractalOctaves(2);
            for (int x = 0; x < size; x++)
            for (int z = 0; z < size; z++)
            {
                resourceNodeMap[x, z] = ResourceNode.None;
                Vector2Int tileCoords = this.chunkCoords * size + new Vector2Int(x, z);
                if (clearedTiles.Contains(tileCoords))
                if (biomeMap[x, z] == Biome.LushPlains)
                {
                    float ironNoise = ironNodesNoise.GetNoise(tileCoords.x, tileCoords.y);
                    float coalNoise = coalNodesNoise.GetNoise(tileCoords.x, tileCoords.y);
                    // prioritise certain materials by checking them first
                    if (heightMap[x, z] != 1) resourceNodeMap[x, z] = ResourceNode.None;
                    else if (ironNoise > 0.8f) resourceNodeMap[x, z] = ResourceNode.Iron;
                    else if (coalNoise > 0.6f) resourceNodeMap[x, z] = ResourceNode.Coal;
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
                    Vector2Int tileCoords = this.chunkCoords * size + new Vector2Int(x, z);
                    hashMaps[seed + i][x, z] = GetIntHash(seed + i, tileCoords.x, tileCoords.y);
                }
            }
            for (int x = 0; x < size; x++)
            for (int z = 0; z < size; z++)
            {
                tileVariationMap[x, z] = hashMaps[seed][x, z] % ChunksManager.instance.lushPlainsTiles[heightMap[x, z]].Length;
                vegetationVariationMap[x, z] = hashMaps[seed + 1][x, z] % ChunksManager.instance.lushPlainsVegetation.Length;
                if (resourceNodeMap[x, z] != ResourceNode.None) resourceNodeVariationMap[x, z] = hashMaps[seed + 2][x, z] % ChunksManager.instance.lushPlainsResourceNodes[(int)resourceNodeMap[x, z]].Length;
            }

            dataReady = true;
        }
        dataGenerationTask ??= Aux(seed, clearedTiles);
        try { await dataGenerationTask; }
        catch (OperationCanceledException) {}
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

    public async Awaitable GenerateTilesMeshAsync()
    {
        if (tilesMeshReady) return;
        async Awaitable Aux()
        {
            while (!dataReady) await Awaitable.NextFrameAsync(); // TODO cant await same task multiple times, so created this while instead, potentially use a better solution (events?)
            await Awaitable.BackgroundThreadAsync();
            ThreadSafeMesh threadSafeMesh = GenerateTilesThreadSafeMesh();
            await Awaitable.MainThreadAsync();
            GenerateUnityMesh(threadSafeMesh, tilesMesh, tilesGameObject);

            tilesMeshReady = true;
        }
        tilesMeshGenerationTask ??= Aux();
        try { await tilesMeshGenerationTask; }
        catch (OperationCanceledException) {}
    }

    public async Awaitable GenerateVegetationMeshAsync()
    {
        if (vegetationMeshReady) return;
        async Awaitable Aux()
        {
            while (!dataReady) await Awaitable.NextFrameAsync();
            await Awaitable.BackgroundThreadAsync();
            ThreadSafeMesh threadSafeMesh = GenerateVegetationThreadSafeMesh();
            await Awaitable.MainThreadAsync();
            GenerateUnityMesh(threadSafeMesh, vegetationMesh, vegetationGameObject);

            vegetationMeshReady = true;
        };
        vegetationMeshGenerationTask ??= Aux();
        try { await vegetationMeshGenerationTask; }
        catch (OperationCanceledException) {}
    }

    public async Awaitable GenerateResourceNodesMeshAsync()
    {
        if (resourceNodesMeshReady) return;
        async Awaitable Aux()
        {
            while (!dataReady) await Awaitable.NextFrameAsync();
            await Awaitable.BackgroundThreadAsync();
            ThreadSafeMesh threadSafeMesh = GenerateResourceNodesThreadSafeMesh();
            await Awaitable.MainThreadAsync();
            GenerateUnityMesh(threadSafeMesh, resourceNodesMesh, resourceNodesGameObject);

            resourceNodesMeshReady = true;
        };
        resourceNodesMeshGenerationTask ??= Aux();
        try { await resourceNodesMeshGenerationTask; }
        catch (OperationCanceledException) {}
    }

    public void GenerateVegetationMeshSync()
    {
        if (vegetationMeshGenerationTask != null && !vegetationMeshReady) throw new Exception("Cant run a synchronous generation task while an async generation task is already running");
        if (!dataReady) throw new Exception("tried to generate mesh synchronously but data is not yet ready");
        vegetationMeshReady = false;
        ThreadSafeMesh threadSafeMesh = GenerateVegetationThreadSafeMesh();
        GenerateUnityMesh(threadSafeMesh, vegetationMesh, vegetationGameObject);
        vegetationMeshReady = true;
    }

    private ThreadSafeMesh GenerateTilesThreadSafeMesh()
    {
        ThreadSafeMesh threadSafeMesh = null;
        for (int x = 0; x < size; x++)
        for (int z = 0; z < size; z++)
        {
            Vector3 tileOffset = new(x, 0, z);
            ThreadSafeMesh tileMesh = ChunksManager.instance.lushPlainsTiles[heightMap[x, z]][tileVariationMap[x, z]];
            if (threadSafeMesh == null) threadSafeMesh = new(tileMesh, tileOffset);
            else threadSafeMesh.Combine(tileMesh, tileOffset);
        }
        return threadSafeMesh;
    }

    private ThreadSafeMesh GenerateVegetationThreadSafeMesh()
    {
        ThreadSafeMesh threadSafeMesh = null;
        for (int x = 0; x < size; x++)
        for (int z = 0; z < size; z++)
        {
            if (!vegetationMap[x, z]) continue;
            Vector3 tileOffset = new(x, 0, z);
            ThreadSafeMesh tileMesh = ChunksManager.instance.lushPlainsTiles[heightMap[x, z]][tileVariationMap[x, z]];
            ThreadSafeMesh singleVegetationMesh = ChunksManager.instance.lushPlainsVegetation[vegetationVariationMap[x, z]];
            Vector3 vegetationOffset = tileOffset + new Vector3(0, tileMesh.MaxY, 0);
            if (threadSafeMesh == null) threadSafeMesh = new(singleVegetationMesh, vegetationOffset);
            else threadSafeMesh.Combine(singleVegetationMesh, vegetationOffset);
        }
        return threadSafeMesh;
    }

    private ThreadSafeMesh GenerateResourceNodesThreadSafeMesh()
    {
        ThreadSafeMesh threadSafeMesh = null;
        for (int x = 0; x < size; x++)
        for (int z = 0; z < size; z++)
        {
            if (resourceNodeMap[x, z] == ResourceNode.None) continue;
            Vector3 tileOffset = new(x, 0, z);
            ThreadSafeMesh tileMesh = ChunksManager.instance.lushPlainsTiles[heightMap[x, z]][tileVariationMap[x, z]];
            ThreadSafeMesh resourceNodeMesh = ChunksManager.instance.lushPlainsResourceNodes[(int)resourceNodeMap[x, z]][resourceNodeVariationMap[x, z]];
            Vector3 resourceNodeOffset = tileOffset + new Vector3(0, tileMesh.MaxY, 0);
            if (threadSafeMesh == null) threadSafeMesh = new(resourceNodeMesh, resourceNodeOffset);
            else threadSafeMesh.Combine(resourceNodeMesh, resourceNodeOffset);
        }
        return threadSafeMesh;
    }

    private void GenerateUnityMesh(ThreadSafeMesh threadSafeMesh, Mesh unityMesh, GameObject meshGameObject)
    {
        if (threadSafeMesh == null) return;
        threadSafeMesh.ConvertToUnityMesh(unityMesh, out int[] materialIds);
        meshGameObject.GetComponent<MeshRenderer>().materials = ChunksManager.instance.GetMaterials(materialIds);
        meshGameObject.GetComponent<MeshFilter>().sharedMesh = unityMesh;
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

    public bool CanBuild(Vector2Int localTileCoords)
    {
        int x = localTileCoords.x;
        int z = localTileCoords.y;
        return heightMap[x, z] == 1;
    }

    public ResourceNode GetResourceNode(Vector2Int localTileCoords)
    {
        int x = localTileCoords.x;
        int z = localTileCoords.y;
        return resourceNodeMap[x, z];
    }

    // returns bool indicationg if the mesh needs regenerating
    public bool ClearVegetation(Vector2Int localTileCoords)
    {
        if (!vegetationMap[localTileCoords.x, localTileCoords.y]) return false;
        vegetationMap[localTileCoords.x, localTileCoords.y] = false;
        return true;
    }
}
