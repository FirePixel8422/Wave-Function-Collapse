using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.UI;


[System.Serializable]
public struct TileStruct
{
    public int id;

    public float weight;

    public bool3 flippable;

    public NativeArray<int> connectors;



    public TileStruct(int _id, float _weight, bool3 _flippable, int[] _connectors)
    {
        id = _id;

        weight = _weight;

        flippable = _flippable;

        connectors = new NativeArray<int>(_connectors, Allocator.Persistent);
    }
}