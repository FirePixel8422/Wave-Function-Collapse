


public static class Ideas
{
    /*[BurstCompile]
    private void GenerateCurrentCellTile(Cell currentCell, NativeArray<int> requiredTileConnections, NativeArray<Cell> neighbours, int tileCount, int nonCollapsedCellCount)
    {
        currentCell = cells[currentCell.gridId];

        //where in the tileOptionArray lies the data of this cell (cell.id * (amount of tileOptions + 1))
        int cellTileOptionArrayIndex = currentCell.gridId * (tileCount + 1);


        //cellTileOptionArrayIndex + tileCount stores the amount of tileOptions int
        int toRandomizeIndex = cellTileOptions[cellTileOptionArrayIndex + tileCount];

        float maxChanceValue = 0;
        for (int i = 0; i < toRandomizeIndex; i++)
        {
            maxChanceValue += tileStructs[cellTileOptions[cellTileOptionArrayIndex]].weight;
        }

        float rChanceValue = Random.Range(0, maxChanceValue);
        float tileWeight;


        int r = -1;

        for (int i = 0; i < toRandomizeIndex; i++)
        {
            tileWeight = tileStructs[cellTileOptions[cellTileOptionArrayIndex + i]].weight;

            if (rChanceValue <= tileWeight)
            {
                r = i;
                break;
            }
            else
            {
                rChanceValue -= tileWeight;
            }
        }

        int finalTileType = cellTileOptions[cellTileOptionArrayIndex + r];

        //collapse current cell
        CollapseCell(currentCell, finalTileType, nonCollapsedCellCount);


        int[] DEBUG_connecotrs = new int[6];


        GetNeighbourCells(currentCell.gridId, ref neighbours);
        for (int i = 0; i < neighbours.Length; i++)
        {
            if (neighbours[i].collapsed)
            {
                DEBUG_connecotrs[i] = tileStructConnectors[neighbours[i].tileType * 6 + InvertDirection(i)];
            }

            //skip non existent or already collapsed neighbours
            if (neighbours[i].collapsed || neighbours[i].initialized == false)
            {
                continue;
            }

            UpdateCell(neighbours[i], requiredTileConnections, neighbours, tileCount);
        }


        //get gridPos
        int3 gridPos = LinearIndexToGridPos(currentCell.gridId);

        //spawn tile
        WaveTile spawnedObj = Instantiate(tilePrefabData[finalTileType].tilePrefab, new Vector3(gridPos.x - gridSize.x * 0.5f + 0.5f, gridPos.y - gridSize.y * 0.5f + 0.5f, gridPos.z - gridSize.z * 0.5f + 0.5f), tilePrefabData[finalTileType].tilePrefab.transform.rotation);

        spawnedObj.DEBUG_connectors = DEBUG_connecotrs;

        //cellTileOptionArrayIndex + tileCount stores the amount of tileOptions int
        int tileOptionsCount = cellTileOptions[cellTileOptionArrayIndex + tileCount];

        spawnedObj.DEBUG_tileOptions = new GameObject[tileOptionsCount];

        for (int i = 0; i < tileOptionsCount; i++)
        {
            spawnedObj.DEBUG_tileOptions[i] = tilePrefabData[cellTileOptions[cellTileOptionArrayIndex + i]].tilePrefab.gameObject;
        }


        if (randomColor)
        {
            Color color = Random.RandomColor();
            foreach (Renderer ren in spawnedObj.GetComponentsInChildren<Renderer>(true))
            {
                ren.material.color = color;
            }
        }
    }*/




    /*[BurstCompile]
    private void CalculateRandomGridCellIndexs(ref NativeArray<int> startCellGridIndexs, int randomStartCellCount, int cellCount)
    {
        NativeList<int> gridPositionIndexList = new NativeList<int>(cellCount, Allocator.Temp);

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                for (int z = 0; z < gridSize.z; z++)
                {
                    //calculate linear cellIndex
                    int cellId = GridPosToLinearIndex(new int3(x, y, z));

                    gridPositionIndexList.Add(cellId);
                }
            }
        }


        int r;
        for (int i = 0; i < randomStartCellCount; i++)
        {
            r = Random.Range(0, gridPositionIndexList.Length);

            startCellGridIndexs[i] = gridPositionIndexList[r];

            gridPositionIndexList.RemoveAtSwapBack(r);
        }

        gridPositionIndexList.Dispose();
    }*/


}