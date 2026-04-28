using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.Rendering;
using System.Linq;

public enum ResourceNode
{
    iron,
    coal,
    none
}

public enum Biome
{
    LushPlains
}

[Serializable]
public struct ThreadSafeSubmesh
{
    public int materialId;
    public MeshTopology topology;
    public List<int> indices;
}

public class ThreadSafeMesh
{
    private List<Vector3> vertices;
    private List<Vector3> normals;
    private List<Vector4> tangents;
    private List<Color> colors;
    private List<Vector2> uv0;
    private List<Vector2> uv1;
    private List<Vector2> uv2;
    private List<Vector2> uv3;
    private List<Vector2> uv4;
    private List<Vector2> uv5;
    private List<Vector2> uv6;
    private List<Vector2> uv7;
    private List<ThreadSafeSubmesh> submeshes;
    public float MaxY { get; private set; }

    public ThreadSafeMesh(ThreadSafeMesh src, Vector3 offset)
    {
        vertices = new(src.vertices.Count);
        for (int i = 0; i < src.vertices.Count; i++) vertices.Add(src.vertices[i] + offset);
        normals = new(src.normals);
        tangents = new(src.tangents);
        colors = new(src.colors);
        uv0 = new(src.uv0);
        uv1 = new(src.uv1);
        uv2 = new(src.uv2);
        uv3 = new(src.uv3);
        uv4 = new(src.uv4);
        uv5 = new(src.uv5);
        uv6 = new(src.uv6);
        uv7 = new(src.uv7);
        submeshes = new(src.submeshes.Count);
        foreach (var submesh in src.submeshes)
        {
            submeshes.Add(new ThreadSafeSubmesh
            {
                materialId = submesh.materialId,
                topology = submesh.topology,
                indices = new List<int>(submesh.indices)
            });
        }
        MaxY = src.MaxY + offset.y;
    }

    public ThreadSafeMesh(Mesh unityMesh, int[] materialIds, Transform transform)
    {
        if (unityMesh.vertexCount == 0) throw new Exception("ThreadSafeMesh cant have 0 vertices");
        if (unityMesh.subMeshCount != materialIds.Length) throw new Exception("subMeshCount doesnt match the number of materials");

        vertices = new(unityMesh.vertexCount);
        normals = new(unityMesh.vertexCount);
        tangents = new(unityMesh.vertexCount);
        colors = new(unityMesh.vertexCount);
        uv0 = new(unityMesh.vertexCount);
        uv1 = new(unityMesh.vertexCount);
        uv2 = new(unityMesh.vertexCount);
        uv3 = new(unityMesh.vertexCount);
        uv4 = new(unityMesh.vertexCount);
        uv5 = new(unityMesh.vertexCount);
        uv6 = new(unityMesh.vertexCount);
        uv7 = new(unityMesh.vertexCount);
        submeshes = new(unityMesh.subMeshCount);

        unityMesh.GetVertices(vertices);
        Vector3[] transformedPoints = new Vector3[unityMesh.vertexCount];
        transform.TransformPoints(vertices.ToArray(), transformedPoints);
        vertices = new(transformedPoints.ToArray());
        unityMesh.GetNormals(normals);
        unityMesh.GetTangents(tangents);
        unityMesh.GetColors(colors);
        unityMesh.GetUVs(0, uv0);
        unityMesh.GetUVs(1, uv1);
        unityMesh.GetUVs(2, uv2);
        unityMesh.GetUVs(3, uv3);
        unityMesh.GetUVs(4, uv4);
        unityMesh.GetUVs(5, uv5);
        unityMesh.GetUVs(6, uv6);
        unityMesh.GetUVs(7, uv7);
        for (int i = 0; i < unityMesh.subMeshCount; i++)
        {
            int[] indices = unityMesh.GetIndices(i);
            if (indices == null || indices.Length == 0) continue;
            submeshes.Add(new ThreadSafeSubmesh
            {
                materialId = materialIds[i],
                topology = unityMesh.GetTopology(i),
                indices = new List<int>(indices)
            });
        }
        foreach (Vector3 vertex in vertices) MaxY = Mathf.Max(MaxY, vertex.y);

        // fill missing values

        static void EnsureSize<T>(List<T> list, int count, T defaultValue)
        {
            for (int i = list.Count; i < count; i++) list.Add(defaultValue);
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
        }

        if (normals.Count != vertices.Count)
        {
            // copy the mesh
            Mesh tmpMesh = UnityEngine.Object.Instantiate(unityMesh);
            tmpMesh.RecalculateNormals();
            tmpMesh.GetNormals(normals);
            EnsureSize(normals, vertices.Count, Vector3.up);
        }
        EnsureSize(tangents, vertices.Count, new Vector4(1, 0, 0, 1));
        EnsureSize(colors, vertices.Count, new Color(255, 255, 255, 255));
        EnsureSize(uv0, vertices.Count, Vector2.zero);
        EnsureSize(uv1, vertices.Count, Vector2.zero);
        EnsureSize(uv2, vertices.Count, Vector2.zero);
        EnsureSize(uv3, vertices.Count, Vector2.zero);
        EnsureSize(uv4, vertices.Count, Vector2.zero);
        EnsureSize(uv5, vertices.Count, Vector2.zero);
        EnsureSize(uv6, vertices.Count, Vector2.zero);
        EnsureSize(uv7, vertices.Count, Vector2.zero);
        if (submeshes.Count == 0)
        {
            submeshes.Add(new ThreadSafeSubmesh
            {
                materialId = materialIds.Length > 0 ? materialIds[0] : 0,
                topology = MeshTopology.Triangles,
                indices = new List<int>()
            });
        }
    }

    public void Combine(ThreadSafeMesh other, Vector3 offset)
    {
        int baseVertex = vertices.Count;

        foreach (Vector3 vertex in other.vertices) vertices.Add(vertex + offset);
        normals.AddRange(other.normals);
        tangents.AddRange(other.tangents);
        colors.AddRange(other.colors);
        uv0.AddRange(other.uv0);
        uv1.AddRange(other.uv1);
        uv2.AddRange(other.uv2);
        uv3.AddRange(other.uv3);
        uv4.AddRange(other.uv4);
        uv5.AddRange(other.uv5);
        uv6.AddRange(other.uv6);
        uv7.AddRange(other.uv7);

        for (int i = 0; i < other.submeshes.Count; i++)
        {
            int targetSubmeshIndex = -1;

            // find existing submesh
            for (int j = 0; j < submeshes.Count; j++)
            {
                if (submeshes[j].materialId == other.submeshes[i].materialId &&
                    submeshes[j].topology == other.submeshes[i].topology)
                {
                    targetSubmeshIndex = j;
                    break;
                }
            }

            // create new submesh
            if (targetSubmeshIndex == -1)
            {
                submeshes.Add(new ThreadSafeSubmesh
                {
                    materialId = other.submeshes[i].materialId,
                    topology = other.submeshes[i].topology,
                    indices = new List<int>()
                });
                targetSubmeshIndex = submeshes.Count - 1;
            }

            List<int> dstIndices = submeshes[targetSubmeshIndex].indices;
            for (int t = 0; t < other.submeshes[i].indices.Count; t++)
            {
                dstIndices.Add(other.submeshes[i].indices[t] + baseVertex);
            }

            MaxY = Mathf.Max(MaxY, other.MaxY + offset.y);
        }
    }

    public void ConvertToUnityMesh(Mesh mesh, out int[] materialIds)
    {
        if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTangents(tangents);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(1, uv1);
        mesh.SetUVs(2, uv2);
        mesh.SetUVs(3, uv3);
        mesh.SetUVs(4, uv4);
        mesh.SetUVs(5, uv5);
        mesh.SetUVs(6, uv6);
        mesh.SetUVs(7, uv7);

        mesh.subMeshCount = submeshes.Count;
        materialIds = new int[submeshes.Count];
        for (int i = 0; i < submeshes.Count; i++)
        {
            var s = submeshes[i];
            materialIds[i] = s.materialId;
            mesh.SetIndices(s.indices, s.topology, i, false);
        }

        mesh.RecalculateBounds();
    }
}

[Serializable]
public struct GameObjectArray
{
    public GameObject[] array;
    public readonly int Length => array.Length;

    public GameObjectArray(int size)
    {
        array = new GameObject[size];
    }

    public GameObject this[int index]
    {
        get { return array[index]; }
        set { array[index] = value; }
    }
}

public class ChunksManager : MonoBehaviour
{
    public static ChunksManager instance { get; private set; }
    [SerializeField] private GameObject chunkPrefab;
    private Stack<Chunk> chunkPool = new();
    private Dictionary<Vector2Int, Chunk> chunks = new();
    [SerializeField] private Transform center;
    private Vector2Int prevCenterChunk;
    public int generateDistance;
    public int renderDistance;
    public int unloadDistance;
    public int seed;

    private List<Material> idToMaterial = new();
    private Dictionary<Material, int> materialToId = new();
    [SerializeField] private GameObjectArray[] lushPlainsTilePrefabs;
    [SerializeField] private GameObject[] lushPlainsVegetationPrefabs;
    [SerializeField] private GameObjectArray[] lushPlainsResourceNodePrefabs;
    [HideInInspector] public ThreadSafeMesh[][] lushPlainsTiles { get; private set; }
    [HideInInspector] public ThreadSafeMesh[] lushPlainsVegetation { get; private set; }
    [HideInInspector] public ThreadSafeMesh[][] lushPlainsResourceNodes { get; private set; }

    private void Awake()
    {
        if (instance) Destroy(this);
        instance = this;
    }

    private void Start()
    {
        prevCenterChunk = new Vector2Int
        (
            Mathf.FloorToInt(center.position.x / Chunk.size),
            Mathf.FloorToInt(center.position.z / Chunk.size)
        );

        // convert prefabs to thread safe meshes
        lushPlainsTiles = new ThreadSafeMesh[lushPlainsTilePrefabs.Length][];
        for (int height = 0; height < lushPlainsTiles.Length; height++)
        {
            lushPlainsTiles[height] = new ThreadSafeMesh[lushPlainsTilePrefabs[height].Length];
            for (int i = 0; i < lushPlainsTiles[height].Length; i++)
            {
                lushPlainsTiles[height][i] = new
                (
                    lushPlainsTilePrefabs[height][i].GetComponent<MeshFilter>().sharedMesh,
                    GetMaterialIds(lushPlainsTilePrefabs[height][i].GetComponent<MeshRenderer>().sharedMaterials),
                    lushPlainsTilePrefabs[height][i].transform
                );
            }
        }
        lushPlainsVegetation = new ThreadSafeMesh[lushPlainsVegetationPrefabs.Length];
        for (int i = 0; i < lushPlainsVegetation.Length; i++)
        {
            lushPlainsVegetation[i] = new
            (
                lushPlainsVegetationPrefabs[i].GetComponent<MeshFilter>().sharedMesh,
                GetMaterialIds(lushPlainsVegetationPrefabs[i].GetComponent<MeshRenderer>().sharedMaterials),
                lushPlainsVegetationPrefabs[i].transform
            );
        }
        lushPlainsResourceNodes = new ThreadSafeMesh[lushPlainsResourceNodePrefabs.Length][];
        foreach (ResourceNode node in Enum.GetValues(typeof(ResourceNode)))
        {
            int nodeIndex = (int)node;
            lushPlainsResourceNodes[nodeIndex] = new ThreadSafeMesh[lushPlainsResourceNodePrefabs[nodeIndex].Length];
            for (int i = 0; i < lushPlainsResourceNodes[nodeIndex].Length; i++)
            {
                lushPlainsResourceNodes[nodeIndex][i] = new
                (
                    lushPlainsResourceNodePrefabs[nodeIndex][i].GetComponent<MeshFilter>().sharedMesh,
                    GetMaterialIds(lushPlainsResourceNodePrefabs[nodeIndex][i].GetComponent<MeshRenderer>().sharedMaterials),
                    lushPlainsResourceNodePrefabs[nodeIndex][i].transform
                );
            }
        }
 
        // prefill chunk pool
        for (int i = 0; i < unloadDistance * unloadDistance; i++)
        {
            chunkPool.Push(Instantiate(chunkPrefab).GetComponent<Chunk>());
            chunkPool.Peek().gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        Vector2Int centerChunkCoords = new Vector2Int
        (
            Mathf.FloorToInt(center.position.x / Chunk.size),
            Mathf.FloorToInt(center.position.z / Chunk.size)
        );

        // unload chunks
        void UnloadChunk(Vector2Int chunkCoords)
        {
            if (chunks.ContainsKey(chunkCoords))
            {
                DestroyChunk(chunks[chunkCoords]);
                chunks.Remove(chunkCoords);
            }
        }
        for (int x = -unloadDistance; x < centerChunkCoords.x - prevCenterChunk.x - unloadDistance; x++)
        for (int z = -unloadDistance; z <= unloadDistance; z++)
        {
            UnloadChunk(new Vector2Int(x, z) + prevCenterChunk);
        }
        for (int x = unloadDistance; x > centerChunkCoords.x - prevCenterChunk.x + unloadDistance; x--)
        for (int z = -unloadDistance; z <= unloadDistance; z++)
        {
            UnloadChunk(new Vector2Int(x, z) + prevCenterChunk);
        }
        for (int z = -unloadDistance; z < centerChunkCoords.y - prevCenterChunk.y - unloadDistance; z++)
        for (int x = -unloadDistance; x <= unloadDistance; x++)
        {
            UnloadChunk(new Vector2Int(x, z) + prevCenterChunk);
        }
        for (int z = unloadDistance; z > centerChunkCoords.y - prevCenterChunk.y + unloadDistance; z--)
        for (int x = -unloadDistance; x <= unloadDistance; x++)
        {
            UnloadChunk(new Vector2Int(x, z) + prevCenterChunk);
        }

        // load chunks
        for (int x = -generateDistance; x <= generateDistance; x++)
        for (int z = -generateDistance; z <= generateDistance; z++)
        {
            Vector2Int chunkCoords = new Vector2Int(x, z) + centerChunkCoords;
            if (!chunks.ContainsKey(chunkCoords))
            {
                Chunk chunk = InstantiateChunk
                (
                    new Vector3(chunkCoords.x * Chunk.size, 0, chunkCoords.y * Chunk.size),
                    Quaternion.identity
                );
                _ = chunk.GenerateDataAsync(seed, chunkCoords);
                chunks.Add(chunkCoords, chunk);
            }
            if (-renderDistance <= x && x <= renderDistance && -renderDistance <= z && z <= renderDistance)
            {
                _ = chunks[chunkCoords].GenerateMeshAsync(chunkCoords);
            }
        }
    }

    private int GetMaterialId(Material material)
    {
        if (materialToId.ContainsKey(material)) return materialToId[material];

        int materialId = idToMaterial.Count;
        idToMaterial.Add(material);
        materialToId.Add(material, materialId);
        return materialId;
    }

    private int[] GetMaterialIds(Material[] materials)
    {
        int[] materialIds = new int[materials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            materialIds[i] = GetMaterialId(materials[i]);
        }
        return materialIds;
    }

    public Material GetMaterial(int materialId)
    {
        if (materialId >= idToMaterial.Count) return null;
        return idToMaterial[materialId];
    }

    public Material[] GetMaterials(int[] materialIds)
    {
        Material[] materials = new Material[materialIds.Length];
        for (int i = 0; i < materialIds.Length; i++)
        {
            materials[i] = GetMaterial(materialIds[i]);
        }
        return materials;
    }

    private Chunk InstantiateChunk(Vector3 position, Quaternion rotation)
    {
        if (chunkPool.Count == 0)
        {
            return Instantiate(chunkPrefab, position, rotation).GetComponent<Chunk>();
        }
        else
        {
            Chunk chunk = chunkPool.Pop();
            chunk.transform.SetPositionAndRotation(position, rotation);
            chunk.gameObject.SetActive(true);
            return chunk;
        }
    }

    private void DestroyChunk(Chunk chunk)
    {
        chunk.Clear();
        chunk.gameObject.SetActive(false);
        chunkPool.Append(chunk);
    }
}