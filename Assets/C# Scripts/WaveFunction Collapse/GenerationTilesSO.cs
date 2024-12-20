using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "GenerationTiles", menuName = "Generation/GenerationTiles")]
public class GenerationTilesSO : ScriptableObject
{
    [Header("Tiles used for generation")]
    public WaveTileData[] waveTileData;
}


[System.Serializable]
public struct WaveTileData
{
    public WaveTile tilePrefab;

    public float weight;
}