using Unity.Burst;


[System.Serializable]
[BurstCompile]
public struct Cell
{
    public int id;

    public bool collapsed;


    public bool initialized;
    public int tileType;

    public Cell(int _id, bool _collapsed = false)
    {
        id = _id;

        collapsed = _collapsed;

        initialized = true;
        tileType = -1;
    }
}