//using System.Collections.Generic;
//using System.Threading.Tasks;
//using Unity.Burst;
//using Unity.Collections;
//using Unity.Mathematics;
//using UnityEngine;


//[BurstCompile]
//public class TWaveFunction : MonoBehaviour
//{
//    public static TWaveFunction Instance;
//    private void Awake()
//    {
//        Instance = this;
//    }



//    private int gridLength;

//    public int3 gridSize;
//    public int3 startCellGridPos;
//    public bool randomStartCell;

//    private NativeArray<Cell> cells;
//    private List<Cell> nonColapsedCells;

//    public WaveTile[] tilePrefabs;

//    public NativeArray<TileStruct> tileStructs;
//    private TileStruct[] DEBUG_tileStructs;

//    public bool randomColor;


//    [BurstCompile]
//    private void Start()
//    {
//        //generate random gridcell startPos
//        if (randomStartCell)
//        {
//            startCellGridPos = Random.Range(int3.zero, gridSize);
//        }

//        gridLength = gridSize.x * gridSize.y * gridSize.z;

//        cells = new NativeArray<Cell>(gridLength, Allocator.Persistent);
//        nonColapsedCells = new List<Cell>(gridLength);

//        int tileCount = tilePrefabs.Length;


//        tileStructs = new NativeArray<TileStruct>(tileCount, Allocator.Persistent);
//        toRandomizeTilePool = new NativeArray<int>(math.max(tileCount, gridLength), Allocator.Persistent);

//        requiredTileConnections = new NativeArray<int>(6, Allocator.Persistent);

//        for (int i = 0; i < tileCount; i++)
//        {
//            tileStructs[i] = new TileStruct(i, tilePrefabs[i].rarity, tilePrefabs[i].flippable, tilePrefabs[i].connectors);
//        }

//        DEBUG_tileStructs = tileStructs.ToArray();

//        CreateGrid(tileCount);
//    }


//    [BurstCompile]
//    private async void CreateGrid(int tileCount)
//    {
//        for (int x = 0; x < gridSize.x; x++)
//        {
//            for (int y = 0; y < gridSize.y; y++)
//            {
//                for (int z = 0; z < gridSize.z; z++)
//                {
//                    //calculate linear cellIndex
//                    int cellId =
//                        x +
//                        y * gridSize.x +
//                        z * gridSize.x * gridSize.y;

//                    Cell cell = new Cell(cellId, tileCount);

//                    cells[cellId] = cell;
//                    nonColapsedCells.Add(cell);
//                }
//            }
//        }


//        int startCellId =
//            startCellGridPos.x +
//            startCellGridPos.y * startCellGridPos.x +
//            startCellGridPos.z * startCellGridPos.x * startCellGridPos.y;

//        await WaveFunctionLoop(cells[startCellId]);
//    }




//    public int cellUpdateTimeMs;
//    public int cellUpdateCount;

//    [BurstCompile]
//    private async Task WaveFunctionLoop(Cell currentCell)
//    {
//        int loopsLeftBeforeTaskDelay = cellUpdateCount;

//        for (int i = 0; i < gridLength; i++)
//        {
//            if (Application.isPlaying == false)
//            {
//                return;
//            }

//            //spawn tile
//            GenerateCurrentCellTile(currentCell);


//            //if all cells have been spawned, end loop
//            if (i == gridLength - 1)
//            {
//                break;
//            }



//            //select new tile
//            SelectNewCell(out currentCell);



//            loopsLeftBeforeTaskDelay -= 1;

//            if (loopsLeftBeforeTaskDelay <= 0)
//            {
//                loopsLeftBeforeTaskDelay = cellUpdateCount;

//                await Task.Delay(cellUpdateTimeMs);
//            }
//        }

//        cells.Dispose();
//        tileStructs.Dispose();

//        toRandomizeTilePool.Dispose();
//    }





//    private NativeArray<int> requiredTileConnections;
//    private NativeArray<int> toRandomizeTilePool;

//    [BurstCompile]
//    private void GenerateCurrentCellTile(Cell currentCell)
//    {
//        int tileStructCount = tileStructs.Length;


//        CalculateRequiredTileConnections(currentCell);


//        #region Check which tiles are an option to generate

//        int toRandomizeIndex = 0;

//        for (int tileId = 0; tileId < tileStructCount; tileId++)
//        {
//            bool isTileCompatible = true;

//            //check if all sides of the current to Check tile match with the neigbours
//            for (int connectorId = 0; connectorId < 6; connectorId++)
//            {
//                int toCheckTileConnection = tileStructs[tileId].connectors[connectorId];
//                int requiredConnection = requiredTileConnections[connectorId];


//                //0 means compatible with anything, if both connections are not 0 and dont match, this tile doesnt fit, so skip it
//                if (requiredConnection != 0 && toCheckTileConnection != 0 && requiredConnection != toCheckTileConnection)
//                {
//                    isTileCompatible = false;

//                    break;
//                }
//            }

//            //all directions of currentTile are valid, add tileId to toRandomizeTilePool
//            if (isTileCompatible)
//            {
//                toRandomizeTilePool[toRandomizeIndex++] = tileId;
//            }
//        }

//        #endregion



//        float maxChanceValue = 0;
//        for (int i = 0; i < toRandomizeIndex; i++)
//        {
//            maxChanceValue += tileStructs[i].rarity;
//        }

//        float rChanceValue = Random.Range(0, maxChanceValue);
//        float tileRarity;


//        int r = -1;

//        for (int i = 0; i < toRandomizeIndex; i++)
//        {
//            tileRarity = tileStructs[i].rarity;
//            if (rChanceValue <= tileRarity)
//            {
//                r = i;
//                break;
//            }
//            else
//            {
//                rChanceValue -= tileRarity;
//            }
//        }

//        int finalTileType = toRandomizeTilePool[r];

//        //collapse current cell
//        CollapseCell(currentCell, finalTileType);

//        //get gridPos
//        LinearIndexToGridPos(currentCell.worldId, out int3 gridPos);

//        //spawn tile
//        WaveTile spawnedObj = Instantiate(tilePrefabs[finalTileType], new Vector3(gridPos.x - gridSize.x * 0.5f + 0.5f, gridPos.y - gridSize.y * 0.5f + 0.5f, gridPos.z - gridSize.z * 0.5f + 0.5f), tilePrefabs[finalTileType].transform.rotation);

//        if (randomColor)
//        {
//            Color color = Random.RandomColor();
//            foreach (Renderer ren in spawnedObj.GetComponentsInChildren<Renderer>(true))
//            {
//                ren.material.color = color;
//            }
//        }
//    }

//    [BurstCompile]
//    private void CalculateRequiredTileConnections(Cell currentCell)
//    {
//        GetNeighbourCells(currentCell.worldId, out Cell[] neighbours);


//        for (int neigbourId = 0; neigbourId < 6; neigbourId++)
//        {
//            //skip unitialized neighbours
//            if (neighbours[neigbourId].initialized == false)
//            {
//                requiredTileConnections[neigbourId] = 0;
//                continue;
//            }

//            //if neigbour is collapsed
//            if (neighbours[neigbourId].collapsed)
//            {
//                int tileType = neighbours[neigbourId].tileType;

//                TileStruct targetTile = tileStructs[tileType];

//                requiredTileConnections[neigbourId] = targetTile.connectors[neigbourId];

//                switch (neigbourId)
//                {
//                    case 0: // left -> right
//                        requiredTileConnections[neigbourId] = targetTile.connectors[1]; // right
//                        break;
//                    case 1: // right -> left
//                        requiredTileConnections[neigbourId] = targetTile.connectors[0]; // left
//                        break;
//                    case 2: // up -> down
//                        requiredTileConnections[neigbourId] = targetTile.connectors[3]; // down
//                        break;
//                    case 3: // down -> up
//                        requiredTileConnections[neigbourId] = targetTile.connectors[2]; // up
//                        break;
//                    case 4: // front -> bacl
//                        requiredTileConnections[neigbourId] = targetTile.connectors[5]; // back
//                        break;
//                    case 5: // back -> front
//                        requiredTileConnections[neigbourId] = targetTile.connectors[4]; // front
//                        break;
//                }
//            }
//            else
//            {
//                requiredTileConnections[neigbourId] = 0;
//            }
//        }
//    }


//    [BurstCompile]
//    private void CollapseCell(Cell currentCell, int finalTileType)
//    {
//        //remove cell from nonCollapsedList
//        nonColapsedCells.Remove(currentCell);

//        //collapse copy of cell
//        currentCell.collapsed = true;

//        //update copy of cell
//        currentCell.tileType = finalTileType;

//        //save copy back
//        cells[currentCell.worldId] = currentCell;
//    }


//    [BurstCompile]
//    private void SelectNewCell(out Cell newCell)
//    {
//        int cellCount = nonColapsedCells.Count;

//        //if there is just 1 cell left, select it and return the function
//        if (cellCount == 1)
//        {
//            newCell = nonColapsedCells[0];
//            return;
//        }


//        //select new cell based on how many tile options neighbour cells have left
//        int leastOptions = int.MaxValue;
//        int cellsToRandomizeId = 0;

//        //loop over all cells
//        for (int i = 0; i < cellCount; i++)
//        {
//            Cell targetCell = nonColapsedCells[i];

//            //if cell is colapsed, skip it
//            if (targetCell.collapsed)
//            {
//                continue;
//            }

//            //if cell specifically has less options
//            if (targetCell.optionsLeft < leastOptions)
//            {
//                leastOptions = targetCell.optionsLeft;

//                cellsToRandomizeId = 0;
//            }

//            //else if cell had the same amount of options
//            else if (targetCell.optionsLeft == leastOptions)
//            {
//                toRandomizeTilePool[cellsToRandomizeId++] = i;
//            }
//        }


//        int r = Random.Range(0, cellsToRandomizeId);

//        newCell = nonColapsedCells[toRandomizeTilePool[r]];
//    }


//    [BurstCompile]
//    private void GetNeighbourCells(int cellId, out Cell[] neighbourCells)
//    {
//        neighbourCells = new Cell[6];


//        // Convert cellId to gridPos
//        LinearIndexToGridPos(cellId, out int3 gridPos);



//        #region Check and add neighbors in all directions

//        if (gridPos.x > 0) // Left
//        {
//            int leftId = cellId - 1;
//            neighbourCells[0] = cells[leftId];
//        }
//        if (gridPos.x < gridSize.x - 1) // Right
//        {
//            int rightId = cellId + 1;
//            neighbourCells[1] = cells[rightId];
//        }


//        if (gridPos.y < gridSize.y - 1) // Up
//        {
//            int upId = cellId + gridSize.x;
//            neighbourCells[2] = cells[upId];
//        }
//        if (gridPos.y > 0) // Down
//        {
//            int downId = cellId - gridSize.x;
//            neighbourCells[3] = cells[downId];
//        }


//        if (gridPos.z < gridSize.z - 1) // Front
//        {
//            int frontId = cellId + gridSize.x * gridSize.y;
//            neighbourCells[4] = cells[frontId];
//        }
//        if (gridPos.z > 0) // Back
//        {
//            int backId = cellId - gridSize.x * gridSize.y;
//            neighbourCells[5] = cells[backId];
//        }

//        #endregion
//    }


//    [BurstCompile]
//    private void LinearIndexToGridPos(int linearIndex, out int3 gridPos)
//    {
//        gridPos.z = linearIndex / (gridSize.x * gridSize.y);
//        gridPos.y = (linearIndex % (gridSize.x * gridSize.y)) / gridSize.x;
//        gridPos.x = linearIndex % gridSize.x;
//    }
//}