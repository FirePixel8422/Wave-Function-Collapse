using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;


[System.Serializable]
[BurstCompile]
public struct Cell
{
    public int worldId;
    public int3 DEBUG_gridPos;

    public NativeArray<int> tileOptions;

    public int tileOptionsCount;

    public bool collapsed;


    public bool initialized;
    public int tileType;

    public Cell(int _id, NativeArray<int> _tileOptions, int3 _gridPos, bool _collapsed = false)
    {
        worldId = _id;

        tileOptions = _tileOptions;
        tileOptionsCount = tileOptions.Length;

        collapsed = _collapsed;

        initialized = true;
        tileType = -1;

        DEBUG_gridPos = _gridPos;

        tileOptions1 = 0;
        tileOptions2 = 0;
        tileOptions3 = 0;
        tileOptions4 = 0;
        tileOptions5 = 0;
        tileOptions6 = 0;
        tileOptions7 = 0;
        tileOptions8 = 0;
        tileOptions9 = 0;
        tileOptions10 = 0;
        tileOptions11 = 0;
        tileOptions12 = 0;
        tileOptions13 = 0;
        tileOptions14 = 0;
        tileOptions15 = 0;
        tileOptions16 = 0;
        tileOptions17 = 0;
        tileOptions18 = 0;
        tileOptions19 = 0;
        tileOptions20 = 0;
    }


    public int tileOptions1, tileOptions2, tileOptions3, tileOptions4, tileOptions5, tileOptions6, tileOptions7, tileOptions8, tileOptions9, tileOptions10,
        tileOptions11, tileOptions12, tileOptions13, tileOptions14, tileOptions15, tileOptions16, tileOptions17, tileOptions18, tileOptions19, tileOptions20;

    public void UpdateTileOptionsInspector(int count)
    {
        tileOptions1 = 0;
        tileOptions2 = 0;
        tileOptions3 = 0;
        tileOptions4 = 0;
        tileOptions5 = 0;
        tileOptions6 = 0;
        tileOptions7 = 0;
        tileOptions8 = 0;
        tileOptions9 = 0;
        tileOptions10 = 0;
        tileOptions11 = 0;
        tileOptions12 = 0;
        tileOptions13 = 0;
        tileOptions14 = 0;
        tileOptions15 = 0;
        tileOptions16 = 0;
        tileOptions17 = 0;
        tileOptions18 = 0;
        tileOptions19 = 0;
        tileOptions20 = 0;


        // Make sure the array has at least 20 elements to avoid out-of-bounds errors.
        for (int i = 0; i < math.min(count, tileOptions.Length); i++)
        {
            // Assign each element in tileOptions to its respective tileOptions field.
            switch (i)
            {
                case 0: tileOptions1 = tileOptions[i]; break;
                case 1: tileOptions2 = tileOptions[i]; break;
                case 2: tileOptions3 = tileOptions[i]; break;
                case 3: tileOptions4 = tileOptions[i]; break;
                case 4: tileOptions5 = tileOptions[i]; break;
                case 5: tileOptions6 = tileOptions[i]; break;
                case 6: tileOptions7 = tileOptions[i]; break;
                case 7: tileOptions8 = tileOptions[i]; break;
                case 8: tileOptions9 = tileOptions[i]; break;
                case 9: tileOptions10 = tileOptions[i]; break;
                case 10: tileOptions11 = tileOptions[i]; break;
                case 11: tileOptions12 = tileOptions[i]; break;
                case 12: tileOptions13 = tileOptions[i]; break;
                case 13: tileOptions14 = tileOptions[i]; break;
                case 14: tileOptions15 = tileOptions[i]; break;
                case 15: tileOptions16 = tileOptions[i]; break;
                case 16: tileOptions17 = tileOptions[i]; break;
                case 17: tileOptions18 = tileOptions[i]; break;
                case 18: tileOptions19 = tileOptions[i]; break;
                case 19: tileOptions20 = tileOptions[i]; break;
            }
        }
    }
}