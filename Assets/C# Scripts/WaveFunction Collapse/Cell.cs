﻿using Unity.Burst;


[System.Serializable]
[BurstCompile]
public struct Cell
{
    /// <summary>
    /// the cells array index
    /// </summary>
    public int gridId;

    /// <summary>
    /// the nonCollapsedCell list index
    /// </summary>
    public int listId;


    public bool collapsed;


    public int tileType;
    public int tileOptionCount;


    public bool initialized;

    public readonly bool IsInitializedAndCollapsed => initialized && collapsed;
    public readonly bool IsUnInitializedOrCollapsed => (initialized == false) || collapsed;


    public Cell(int _id, int tileCount, bool _collapsed = false)
    {
        gridId = _id;
        listId = _id;

        collapsed = _collapsed;

        tileType = -1;
        tileOptionCount = tileCount;

        initialized = true;
    }

    public static Cell Uninitialized()
    {
        return new Cell();
    }
}