using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.Rendering;

public enum Biome
{
    LushPlains
}

[Serializable]
public sealed class ThreadSafeSubmesh
{
    public int materialId;
    public MeshTopology topology = MeshTopology.Triangles;
    public List<int> indices = new();
}

public class ThreadSafeMesh
{
    private List<Vector3> vertices = new();
    private List<Vector3> normals = new();
    private List<Vector4> tangents = new();
    private List<Color32> colors32 = new();
    private List<Vector2> uv0 = new();
    private List<Vector2> uv1 = new();
    private List<Vector2> uv2 = new();
    private List<Vector2> uv3 = new();
    private List<Vector2> uv4 = new();
    private List<Vector2> uv5 = new();
    private List<Vector2> uv6 = new();
    private List<Vector2> uv7 = new();
    private List<ThreadSafeSubmesh> submeshes = new();
    private Bounds bounds;

    public ThreadSafeMesh(Mesh unityMesh, int[] materialIds)
    {
        if (unityMesh.vertexCount == 0) throw new Exception("ThreadSafeMesh cant have 0 vertices");

        unityMesh.GetVertices(vertices);
        unityMesh.GetNormals(normals);
        unityMesh.GetTangents(tangents);
        unityMesh.GetColors(colors32);
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
            submeshes.Add(new ThreadSafeSubmesh()
            {
                materialId = materialIds[i],
                topology = unityMesh.GetTopology(i),
                indices = new List<int>(indices)
            });
        }
        bounds = unityMesh.bounds;

        // fill missing values

        static void EnsureSize<T>(List<T> list, int count, T defaultValue)
        {
            for (int i = list.Count; i < count; i++) list.Add(defaultValue);
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
        }

        if (normals.Count != vertices.Count)
        {
            unityMesh.RecalculateNormals();
            unityMesh.GetNormals(normals);
            EnsureSize(normals, vertices.Count, Vector3.up);
        }
        EnsureSize(tangents, vertices.Count, new Vector4(1, 0, 0, 1));
        EnsureSize(colors32, vertices.Count, new Color32(255, 255, 255, 255));
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
            submeshes.Add(new ThreadSafeSubmesh()
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
        colors32.AddRange(other.colors32);
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
        }

        Bounds shifted = other.bounds;
        shifted.center += offset;
        bounds.Encapsulate(shifted);
    }

    public Mesh ConvertToUnityMesh(out int[] materialIds)
    {
        var mesh = new Mesh();

        if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTangents(tangents);
        mesh.SetColors(colors32);
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

        mesh.bounds = bounds;
        
        return mesh;
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

    private List<Material> idToMaterial = new();
    private Dictionary<Material, int> materialToId = new(); 
    [SerializeField] private GameObject[][] lushPlainsTilePrefabs;
    [SerializeField] private GameObject[] lushPlainsVegetationPrefabs;
    [HideInInspector] public ThreadSafeMesh[][] lushPlainsTiles { get; private set; }
    [HideInInspector] public ThreadSafeMesh[] lushPlainsVegetation { get; private set; }

    private void Start()
    {
        lushPlainsTiles = new ThreadSafeMesh[lushPlainsTilePrefabs.Length][];
        for (int height = 0; height < lushPlainsTiles.Length; height++)
        {
            lushPlainsTiles[height] = new ThreadSafeMesh[lushPlainsTilePrefabs[height].Length];
            for (int i = 0; i < lushPlainsTiles[height].Length; i++)
            {
                lushPlainsTiles[height][i] = new
                (
                    lushPlainsTilePrefabs[height][i].GetComponent<MeshFilter>().mesh,
                    GetMaterialIds(lushPlainsTilePrefabs[height][i].GetComponent<MeshRenderer>().materials)
                );
            }
        }
        lushPlainsVegetation = new ThreadSafeMesh[lushPlainsVegetationPrefabs.Length];
        for (int i = 0; i < lushPlainsVegetation.Length; i++)
        {
            lushPlainsVegetation[i] = new
            (
                lushPlainsVegetationPrefabs[i].GetComponent<MeshFilter>().mesh,
                GetMaterialIds(lushPlainsVegetationPrefabs[i].GetComponent<MeshRenderer>().materials)
            );
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

    private Material GetMaterial(int materialId)
    {
        if (materialId >= idToMaterial.Count) return null;
        return idToMaterial[materialId];
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