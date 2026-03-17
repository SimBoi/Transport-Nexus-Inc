using System.CodeDom.Compiler;
using JetBrains.Annotations;
using UnityEngine;

public enum Biome
{
    LushPlains,
    ScorchedLands,
    CrimsonDunes,
    MagnetisedHills
}

public class Chunk : MonoBehaviour
{
    public static int size = 8;
    private Vector2Int[,] heights = new Vector2Int[size, size];

    public void Generate(Vector2Int chunkCoords)
    {
        
    }

    private static Biome GenBiome(Vector2Int chunkCoords)
    {
        return Biome.LushPlains;
    }

    private static float GenTemperature(Vector2Int chunkCoords)
    {
        return 0;
    }

    private static float GenHumidity(Vector2Int chunkCoords)
    {
        return 0;
    }

    private static float GenHeight(Vector2Int tileCoords)
    {
        return 0;
    }
}