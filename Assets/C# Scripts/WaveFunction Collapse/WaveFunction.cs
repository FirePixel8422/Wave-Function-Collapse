using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static UnityEngine.Mesh;


[BurstCompile]
public class WaveFunction : MonoBehaviour
{
    public static WaveFunction Instance;
    private void Awake()
    {
        Instance = this;
    }


    public GameObject CUBE;


    private int gridLength;

    public int3 gridSize;
    [Range(0, 25)]
    public int randomStartCellCount;


    private NativeArray<Cell> cells;
    private Cell[] DEBUG_cells;
    private List<Cell> nonColapsedCells;

    public WaveTile[] tilePrefabs;

    private NativeArray<TileStruct> tileStructs;

    public bool randomColor;



    [BurstCompile]
    private void Start()
    {
        //calculate total gridLength
        gridLength = gridSize.x * gridSize.y * gridSize.z;
        int tileCount = tilePrefabs.Length;

        int[] startCellGridIndexs = new int[randomStartCellCount];

        //generate random gridcell startpositions
        if (randomStartCellCount > 0)
        {
            NativeList<int> gridPositinIndexList = new NativeList<int>(gridLength, Allocator.Temp);

            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    for (int z = 0; z < gridSize.z; z++)
                    {
                        //calculate linear cellIndex
                        int cellId =
                            x +
                            y * gridSize.x +
                            z * gridSize.x * gridSize.y;

                        gridPositinIndexList.Add(cellId);
                    }
                }
            }


            int r;
            for (int i = 0; i < randomStartCellCount; i++)
            {
                r = Random.Range(0, gridPositinIndexList.Length);

                startCellGridIndexs[i] = gridPositinIndexList[r];

                gridPositinIndexList.RemoveAtSwapBack(r);
            }

            gridPositinIndexList.Dispose();
        }




        MeshDrawData[] _meshDrawData = new MeshDrawData[tileCount];
        meshes = new Mesh[tileCount];

        for (int i = 0; i < tileCount; i++)
        {
            _meshDrawData[i] = new MeshDrawData(i, gridLength);

            MeshFilter[] meshFilters = tilePrefabs[i].GetComponentsInChildren<MeshFilter>(true);

            CombineInstance[] combineInstances = new CombineInstance[meshFilters.Length];

            for (int meshId = 0; meshId < meshFilters.Length; meshId++)
            {
                combineInstances[meshId].mesh = meshFilters[meshId].sharedMesh;
                combineInstances[meshId].transform = meshFilters[meshId].transform.localToWorldMatrix;
            }

            meshes[i] = new Mesh();
            meshes[i].CombineMeshes(combineInstances);
        }

        meshData = _meshDrawData;


        cells = new NativeArray<Cell>(gridLength, Allocator.Persistent);
        nonColapsedCells = new List<Cell>(gridLength);


        tileStructs = new NativeArray<TileStruct>(tileCount, Allocator.Persistent);
        toRandomizeTilePool = new NativeArray<int>(math.max(tileCount, gridLength), Allocator.Persistent);

        requiredTileConnections = new NativeArray<int>(6, Allocator.Persistent);

        for (int i = 0; i < tileCount; i++)
        {
            tileStructs[i] = new TileStruct(i, tilePrefabs[i].rarity, tilePrefabs[i].flippable, tilePrefabs[i].connectors);
        }

        CreateGrid(tileCount, startCellGridIndexs);
    }


    [BurstCompile]
    private async void CreateGrid(int tileCount, int[] startCellGridIndexs)
    {
        NativeArray<int> tileOptions = new NativeArray<int>(tileCount, Allocator.Persistent);
        for (int i = 0; i < tileCount; i++)
        {
            tileOptions[i] = i;
        }


        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                for (int z = 0; z < gridSize.z; z++)
                {
                    //calculate linear cellIndex
                    int cellId =
                        x +
                        y * gridSize.x +
                        z * gridSize.x * gridSize.y;

                    Cell cell = new Cell(cellId, tileOptions, new int3(x, y, z));

                    cells[cellId] = cell;
                    nonColapsedCells.Add(cell);
                }
            }
        }

        DEBUG_cells = cells.ToArray();

        await WaveFunctionLoop(startCellGridIndexs);
    }



    private Mesh[] meshes;
    public Material mat;

    public MeshDrawData[] meshData;


    [System.Serializable]
    public struct MeshDrawData
    {
        public NativeList<Matrix4x4> matrices;
        public int matrixCount;

        public int meshIndex;


        public MeshDrawData(int _meshIndex, int _matrixCount)
        {
            meshIndex = _meshIndex;

            matrixCount = _matrixCount;
            matrices = new NativeList<Matrix4x4>(matrixCount, Allocator.Persistent);
        }

        public void AddMeshData(Matrix4x4 matrix)
        {
            matrices.AddNoResize(matrix);
        }
    }

    private void Update()
    {
        for (int i = 0; i < meshData.Length; i++)
        {
            if (meshData[i].matrices.Length == 0)
            {
                continue;
            }

            Graphics.DrawMeshInstanced(meshes[meshData[i].meshIndex], 0, mat, meshData[i].matrices.AsArray().ToArray());
        }
    }




    public int cellUpdateTimeMs;
    public int cellUpdateCount;

    [BurstCompile]
    private async Task WaveFunctionLoop(int[] startCellGridIndexs)
    {
        int loopsLeftBeforeTaskDelay = cellUpdateCount;

        Cell currentCell = new Cell();

        int startCellIndex = 0;
        int startCellCount = startCellGridIndexs.Length;

        //select first start Cell
        if (startCellIndex != startCellCount)
        {
            currentCell = cells[startCellGridIndexs[0]];

            //update startCell tileRequirement Data
            UpdateCell(currentCell);

            startCellIndex += 1;
        }


        for (int i = 0; i < gridLength; i++)
        {
            if (Application.isPlaying == false)
            {
                return;
            }

            //spawn tile
            GenerateCurrentCellTile_OLD(currentCell);


            //if all cells have been spawned, end loop
            if (i == gridLength - 1)
            {
                break;
            }



            if (startCellIndex != startCellCount)
            {
                currentCell = cells[startCellGridIndexs[startCellIndex]];

                //update startCell tileRequirement Data
                UpdateCell(currentCell);

                startCellIndex += 1;
            }
            else
            {
                //select new tile
                SelectNewCell(out currentCell);
            }



                loopsLeftBeforeTaskDelay -= 1;

            if (loopsLeftBeforeTaskDelay <= 0)
            {
                loopsLeftBeforeTaskDelay = cellUpdateCount;

                await Task.Delay(cellUpdateTimeMs);
            }
        }

        cells.Dispose();
        tileStructs.Dispose();

        toRandomizeTilePool.Dispose();
    }




    private NativeArray<int> requiredTileConnections;
    private NativeArray<int> toRandomizeTilePool;

    [BurstCompile]
    private void GenerateCurrentCellTile(Cell currentCell)
    {
        currentCell = cells[currentCell.worldId];

        int toRandomizeIndex = currentCell.tileOptionsCount;

        float maxChanceValue = 0;
        for (int i = 0; i < toRandomizeIndex; i++)
        {
            maxChanceValue += tileStructs[currentCell.tileOptions[i]].rarity;
        }

        float rChanceValue = Random.Range(0, maxChanceValue);
        float tileRarity;


        int r = -1;

        for (int i = 0; i < toRandomizeIndex; i++)
        {
            tileRarity = tileStructs[currentCell.tileOptions[i]].rarity;

            if (rChanceValue <= tileRarity)
            {
                r = i;
                break;
            }
            else
            {
                rChanceValue -= tileRarity;
            }
        }

        int finalTileType = currentCell.tileOptions[r];

        //collapse current cell
        CollapseCell(currentCell, finalTileType);


        int[] DEBUG_connecotrs = new int[6];

        GetNeighbourCells(currentCell.worldId, out Cell[] neighbours);
        for (int i = 0; i < neighbours.Length; i++)
        {
            if (neighbours[i].collapsed)
            {
                DEBUG_connecotrs[i] = tileStructs[neighbours[i].tileType].connectors[OppositeSide(i)];
            }

            //skip non existent or already collapsed neighbours
            if (neighbours[i].collapsed || neighbours[i].initialized == false)
            {
                continue;
            }

            UpdateCell(neighbours[i]);
        }


        //get gridPos
        LinearIndexToGridPos(currentCell.worldId, out int3 gridPos);

        //spawn tile
        WaveTile spawnedObj = Instantiate(tilePrefabs[finalTileType], new Vector3(gridPos.x - gridSize.x * 0.5f + 0.5f, gridPos.y - gridSize.y * 0.5f + 0.5f, gridPos.z - gridSize.z * 0.5f + 0.5f), tilePrefabs[finalTileType].transform.rotation);

        spawnedObj.DEBUG_connectors = DEBUG_connecotrs;

        spawnedObj.DEBUG_tileOptions = new GameObject[currentCell.tileOptionsCount];

        for (int i = 0; i < currentCell.tileOptionsCount; i++)
        {
            spawnedObj.DEBUG_tileOptions[i] = tilePrefabs[currentCell.tileOptions[i]].gameObject;
        }


        if (randomColor)
        {
            Color color = Random.RandomColor();
            foreach (Renderer ren in spawnedObj.GetComponentsInChildren<Renderer>(true))
            {
                ren.material.color = color;
            }
        }
    }



    [BurstCompile]
    private void GenerateCurrentCellTile_OLD(Cell currentCell)
    {
        int tileStructCount = tileStructs.Length;


        CalculateRequiredTileConnections_OLD(currentCell);


        #region Check which tiles are an option to generate

        int toRandomizeIndex = 0;

        for (int tileId = 0; tileId < tileStructCount; tileId++)
        {
            bool isTileCompatible = true;

            //check if all sides of the current to Check tile match with the neigbours
            for (int connectorId = 0; connectorId < 6; connectorId++)
            {
                int toCheckTileConnection = tileStructs[tileId].connectors[connectorId];
                int requiredConnection = requiredTileConnections[connectorId];


                //0 means compatible with anything, if both connections are not 0 and dont match, this tile doesnt fit, so skip it
                if (requiredConnection != 0 && toCheckTileConnection != 0 && requiredConnection != toCheckTileConnection)
                {
                    isTileCompatible = false;

                    break;
                }
            }

            //all directions of currentTile are valid, add tileId to toRandomizeTilePool
            if (isTileCompatible)
            {
                toRandomizeTilePool[toRandomizeIndex++] = tileId;
            }
        }

        #endregion



        float maxChanceValue = 0;
        for (int i = 0; i < toRandomizeIndex; i++)
        {
            maxChanceValue += tileStructs[i].rarity;
        }

        float rChanceValue = Random.Range(0, maxChanceValue);
        float tileRarity;


        int r = -1;

        for (int i = 0; i < toRandomizeIndex; i++)
        {
            tileRarity = tileStructs[i].rarity;
            if (rChanceValue <= tileRarity)
            {
                r = i;
                break;
            }
            else
            {
                rChanceValue -= tileRarity;
            }
        }

        int finalTileType = toRandomizeTilePool[r];

        //collapse current cell
        CollapseCell(currentCell, finalTileType);


        int[] DEBUG_connecotrs = new int[6];

        GetNeighbourCells(currentCell.worldId, out Cell[] neighbours);
        for (int i = 0; i < neighbours.Length; i++)
        {
            if (neighbours[i].collapsed)
            {
                DEBUG_connecotrs[i] = tileStructs[neighbours[i].tileType].connectors[OppositeSide(i)];
            }

            //skip non existent or already collapsed neighbours
            if (neighbours[i].collapsed || neighbours[i].initialized == false)
            {
                continue;
            }

            UpdateCell(neighbours[i]);
        }


        //get gridPos
        LinearIndexToGridPos(currentCell.worldId, out int3 gridPos);

        //spawn tile
        //WaveTile spawnedObj = Instantiate(tilePrefabs[finalTileType], new Vector3(gridPos.x - gridSize.x * 0.5f + 0.5f, gridPos.y - gridSize.y * 0.5f + 0.5f, gridPos.z - gridSize.z * 0.5f + 0.5f), tilePrefabs[finalTileType].transform.rotation);

        //spawnedObj.DEBUG_connectors = DEBUG_connecotrs;

        //spawnedObj.DEBUG_tileOptions = new GameObject[currentCell.tileOptionsCount];

        //for (int i = 0; i < currentCell.tileOptionsCount; i++)
        //{
        //    spawnedObj.DEBUG_tileOptions[i] = tilePrefabs[currentCell.tileOptions[i]].gameObject;
        //}

        Vector3 pos = new Vector3(gridPos.x - gridSize.x * 0.5f + 0.5f, gridPos.y - gridSize.y * 0.5f + 0.5f, gridPos.z - gridSize.z * 0.5f + 0.5f);
        Quaternion rot = tilePrefabs[finalTileType].transform.rotation;

        meshData[finalTileType].AddMeshData(Matrix4x4.TRS(pos, rot, Vector3.one));


        //if (randomColor)
        //{
        //    Color color = Random.RandomColor();
        //    foreach (Renderer ren in spawnedObj.GetComponentsInChildren<Renderer>(true))
        //    {
        //        ren.material.color = color;
        //    }
        //}
    }

    [BurstCompile]
    private void CalculateRequiredTileConnections_OLD(Cell currentCell)
    {
        GetNeighbourCells(currentCell.worldId, out Cell[] neighbours);


        for (int neigbourId = 0; neigbourId < 6; neigbourId++)
        {
            //skip unitialized neighbours
            if (neighbours[neigbourId].initialized == false)
            {
                requiredTileConnections[neigbourId] = 0;
                continue;
            }

            //if neigbour is collapsed
            if (neighbours[neigbourId].collapsed)
            {
                int tileType = neighbours[neigbourId].tileType;

                TileStruct targetTile = tileStructs[tileType];

                requiredTileConnections[neigbourId] = targetTile.connectors[neigbourId];

                switch (neigbourId)
                {
                    case 0: // left -> right
                        requiredTileConnections[neigbourId] = targetTile.connectors[1]; // right
                        break;
                    case 1: // right -> left
                        requiredTileConnections[neigbourId] = targetTile.connectors[0]; // left
                        break;
                    case 2: // up -> down
                        requiredTileConnections[neigbourId] = targetTile.connectors[3]; // down
                        break;
                    case 3: // down -> up
                        requiredTileConnections[neigbourId] = targetTile.connectors[2]; // up
                        break;
                    case 4: // front -> bacl
                        requiredTileConnections[neigbourId] = targetTile.connectors[5]; // back
                        break;
                    case 5: // back -> front
                        requiredTileConnections[neigbourId] = targetTile.connectors[4]; // front
                        break;
                }
            }
            else
            {
                requiredTileConnections[neigbourId] = 0;
            }
        }
    }




    [BurstCompile]
    private void UpdateCell(Cell targetCell)
    {
        //Destroy(Instantiate(CUBE, new Vector3(targetCell.DEBUG_gridPos.x - gridSize.x * 0.5f + 0.5f, targetCell.DEBUG_gridPos.y - gridSize.y * 0.5f + 0.5f, targetCell.DEBUG_gridPos.z - gridSize.z * 0.5f + 0.5f), Quaternion.identity), 0.75f);

        GetRequiredTileConnections(targetCell, out int[] requiredTileConnections);

        int totalTiles = tileStructs.Length;

        int toRandomizeIndex = 0;

        for (int tileId = 0; tileId < totalTiles; tileId++)
        {
            bool isTileCompatible = true;

            //check if all sides of the current to Check tile match with the neigbours
            for (int connectorId = 0; connectorId < 6; connectorId++)
            {
                int toCheckTileConnection = tileStructs[tileId].connectors[OppositeSide(connectorId)];
                int requiredConnection = requiredTileConnections[OppositeSide(connectorId)];


                //0 means compatible with anything, if both connections are not 0 and dont match, this tile doesnt fit, so skip it
                if (requiredConnection != 0 && toCheckTileConnection != 0 && requiredConnection != toCheckTileConnection)
                {
                    isTileCompatible = false;
                    //Debug.Log($"Tile {tileId} is not compatible with required connection {connectorId}.");
                    break;
                }
            }


            //all directions of currentTile are valid, add tileId to toRandomizeTilePool
            if (isTileCompatible)
            {
                //Debug.Log($"Tile {tileId} is compatible.");
                targetCell.tileOptions[toRandomizeIndex++] = tileId;
            }
        }
        targetCell.UpdateTileOptionsInspector(toRandomizeIndex);

        targetCell.tileOptionsCount = toRandomizeIndex;

        //update cell back
        cells[targetCell.worldId] = targetCell;

        DEBUG_cells = cells.ToArray();
    }

    [BurstCompile]
    private int OppositeSide(int side)
    {
        return side switch
        {
            0 => 1, // Left -> Right
            1 => 0, // Right -> Left
            2 => 3, // Up -> Down
            3 => 2, // Down -> Up
            4 => 5, // Front -> Back
            5 => 4, // Back -> Front
            _ => -1,
        };
    }


    [BurstCompile]
    private void GetRequiredTileConnections(Cell currentCell, out int[] requiredTileConnections)
    {
        requiredTileConnections = new int[6];


        GetNeighbourCells(currentCell.worldId, out Cell[] neighbours);

        for (int neigbourId = 0; neigbourId < 6; neigbourId++)
        {
            //skip unitialized neighbours
            if (neighbours[neigbourId].initialized == false)
            {
                continue;
            }

            //if neigbour is collapsed
            if (neighbours[neigbourId].collapsed)
            {
                int tileType = neighbours[neigbourId].tileType;

                TileStruct targetTile = tileStructs[tileType];

                requiredTileConnections[neigbourId] = targetTile.connectors[neigbourId];
            }
        }
    }


    [BurstCompile]
    private void CollapseCell(Cell currentCell, int finalTileType)
    {
        //remove cell from nonCollapsedList
        for (int i = 0; i < nonColapsedCells.Count; i++)
        {
            if (nonColapsedCells[i].worldId == currentCell.worldId)
            {
                nonColapsedCells.RemoveAt(i);
                break;
            }
        }

        //collapse copy of cell
        currentCell.collapsed = true;

        //update copy of cell
        currentCell.tileType = finalTileType;

        //save copy back
        cells[currentCell.worldId] = currentCell;

        DEBUG_cells = cells.ToArray();
    }


    [BurstCompile]
    private void SelectNewCell(out Cell newCell)
    {
        int cellCount = nonColapsedCells.Count;

        //if there is just 1 cell left, select it and return the function
        if (cellCount == 1)
        {
            newCell = cells[nonColapsedCells[0].worldId];
            return;
        }


        //select new cell based on how many tile options neighbour cells have left
        int leastOptions = int.MaxValue;
        int cellsToRandomizeId = 0;

        //loop over all cells
        for (int i = 0; i < cellCount; i++)
        {
            Cell targetCell = cells[nonColapsedCells[i].worldId];

            //if cell is colapsed, skip it
            if (targetCell.collapsed)
            {
                continue;
            }

            //if cell specifically has less options
            if (targetCell.tileOptionsCount < leastOptions)
            {
                leastOptions = targetCell.tileOptionsCount;

                cellsToRandomizeId = 0;
                toRandomizeTilePool[cellsToRandomizeId++] = i;
            }

            //else if cell had the same amount of options
            else if (targetCell.tileOptionsCount == leastOptions)
            {
                toRandomizeTilePool[cellsToRandomizeId++] = i;
            }
        }


        int r = Random.Range(0, cellsToRandomizeId);

        newCell = cells[nonColapsedCells[toRandomizeTilePool[r]].worldId];
    }


    [BurstCompile]
    private void GetNeighbourCells(int cellId, out Cell[] neighbourCells)
    {
        neighbourCells = new Cell[6];


        // Convert cellId to gridPos
        LinearIndexToGridPos(cellId, out int3 gridPos);



        #region Check and add neighbors in all directions

        if (gridPos.x > 0) // Left
        {
            int leftId = cellId - 1;
            neighbourCells[0] = cells[leftId];
        }
        if (gridPos.x < gridSize.x - 1) // Right
        {
            int rightId = cellId + 1;
            neighbourCells[1] = cells[rightId];
        }


        if (gridPos.y < gridSize.y - 1) // Up
        {
            int upId = cellId + gridSize.x;
            neighbourCells[2] = cells[upId];
        }
        if (gridPos.y > 0) // Down
        {
            int downId = cellId - gridSize.x;
            neighbourCells[3] = cells[downId];
        }


        if (gridPos.z < gridSize.z - 1) // Front
        {
            int frontId = cellId + gridSize.x * gridSize.y;
            neighbourCells[4] = cells[frontId];
        }
        if (gridPos.z > 0) // Back
        {
            int backId = cellId - gridSize.x * gridSize.y;
            neighbourCells[5] = cells[backId];
        }

        #endregion
    }


    [BurstCompile]
    private void LinearIndexToGridPos(int linearIndex, out int3 gridPos)
    {
        gridPos.z = linearIndex / (gridSize.x * gridSize.y);
        gridPos.y = (linearIndex % (gridSize.x * gridSize.y)) / gridSize.x;
        gridPos.x = linearIndex % gridSize.x;
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(gridSize.x, gridSize.y, gridSize.z));
    }
}