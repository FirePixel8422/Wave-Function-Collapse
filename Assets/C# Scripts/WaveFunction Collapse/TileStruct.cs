using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;


[System.Serializable]
[BurstCompile]
public struct TileStruct
{
    public int id;

    public float weight;

    public bool3 flippable;



    public TileStruct(int _id, float _weight, bool3 _flippable)
    {
        id = _id;

        weight = _weight;

        flippable = _flippable;
    }
}