using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;


[BurstCompile]
public class WaveFunction : MonoBehaviour
{
    public static WaveFunction Instance;
    private void Awake()
    {
        Instance = this;
    }



    [Header("Tiles used for generation")]
    [SerializeField] private GenerationTilesSO generationTiles;

    [Header("3D size of the Grid to generate in")]
    [SerializeField] private int3 gridSize;

    [SerializeField] private uint seed;
    [SerializeField] private bool randomSeed;


    [SerializeField] private int edgeRequiredConnection;

    [SerializeField] private bool useGPUBasedRendering;
    [SerializeField] private bool randomColor;




    //copy of tiles from scriptable object "generationTiles"
    private WaveTileData[] tilePrefabData;

    //every cell in the grid
    private NativeArray<Cell> cells;

    //every non collapsed cell in the grid
    private NativeList<Cell> nonCollapsedCells;

    //calculation array for a tiles connectionsTypes
    private NativeArray<int> tileStructConnectors;

    //Native structure based copy of generationTiles.tilePrefabs data, so burst can go brrr
    private NativeArray<TileStruct> tileStructs;

    //3d world Transform pos of this gameobject
    private float3 gridWorldPos;




    //DEBUG
    //[SerializeField]
    private Cell[] DEBUG_cells;
    //[SerializeField]
    private Cell[] DEBUG_nonCollapsedCells;



    [BurstCompile]
    private async void Start()
    {
        sw = Stopwatch.StartNew();

        if (randomSeed)
        {
            seed = Random.Range(1, uint.MaxValue);
        }
        Random.ReSeed(seed);


        tilePrefabData = generationTiles.waveTileData;

        int cellCount = gridSize.x * gridSize.y * gridSize.z;
        int tileCount = tilePrefabData.Length;

        CreateEmptyGrid(cellCount, tileCount);



        gridWorldPos = new float3(transform.position.x, transform.position.y, transform.position.z);


        SetupGPURenderMeshData(tileCount, cellCount);


        //setup tileStrycts Array
        tileStructs = new NativeArray<TileStruct>(tileCount, Allocator.Persistent);

        //6 (6 directions) bit used ints for every tileStruct
        tileStructConnectors = new NativeArray<int>(tileCount * 6, Allocator.Persistent);

        for (int i = 0; i < tileCount; i++)
        {
            tileStructs[i] = new TileStruct(i, tilePrefabData[i].weight, tilePrefabData[i].tilePrefab.flippable);


            //loop for every world direction
            for (int i2 = 0; i2 < 6; i2++)
            {
                //set every tileStruct their connector bitwise int in the 2d "tileStructConnectors" array
                tileStructConnectors[i * 6 + i2] = (int)tilePrefabData[i].tilePrefab.connectors[i2];
            }
        }



        int3 randomGridPos = Random.Range(int3.zero, gridSize);
        int startCellId = GridPosToLinearIndex(randomGridPos);

        //gets used for randomizing cell selection and tile selection, capicity is whatever are more of, cells or tileTypes
        NativeArray<int> toRandomizeTilePool = new NativeArray<int>(math.max(tileCount, cellCount), Allocator.Persistent);

        //data arrays to use for calculations
        NativeArray<int> requiredTileConnections = new NativeArray<int>(6, Allocator.Persistent);
        NativeArray<Cell> neighbours = new NativeArray<Cell>(6, Allocator.Persistent);

        await WaveFunctionLoop(startCellId, requiredTileConnections, neighbours, toRandomizeTilePool, cellCount, tileCount);
    }


    [BurstCompile]
    private void SetupGPURenderMeshData(int tileCount, int cellCount)
    {
        NativeArray<MeshDrawData> _meshDrawData = new NativeArray<MeshDrawData>(tileCount, Allocator.Persistent);

        matrixData = new NativeArray<Matrix4x4>(cellCount, Allocator.Persistent);
        meshes = new Mesh[tileCount];
        materials = new Material[tileCount];

        for (int i = 0; i < tileCount; i++)
        {
            _meshDrawData[i] = new MeshDrawData(i, cellCount);

            MeshFilter[] meshFilters = tilePrefabData[i].tilePrefab.GetComponentsInChildren<MeshFilter>(true);

            CombineInstance[] combineInstances = new CombineInstance[meshFilters.Length];

            for (int meshId = 0; meshId < meshFilters.Length; meshId++)
            {
                combineInstances[meshId].mesh = meshFilters[meshId].sharedMesh;

                Matrix4x4 meshMatrix = meshFilters[meshId].transform.localToWorldMatrix;

                combineInstances[meshId].transform = meshMatrix;
            }

            meshes[i] = new Mesh();
            meshes[i].CombineMeshes(combineInstances, true, true,true);

            MeshRenderer renderer = tilePrefabData[i].tilePrefab.GetComponentInChildren<MeshRenderer>();

            materials[i] = renderer == null ? new Material(Shader.Find("Standard")) : renderer.sharedMaterial;
        }

        meshData = _meshDrawData;
    }


    [BurstCompile]
    private void CreateEmptyGrid(int cellCount, int tileCount)
    {
        cells = new NativeArray<Cell>(cellCount, Allocator.Persistent);
        nonCollapsedCells = new NativeList<Cell>(cellCount, Allocator.Persistent);

        //create cells array, linear index based (int3 gridPos to linear)
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

                    Cell cell = new Cell(cellId, tileCount);

                    cells[cellId] = cell;
                }
            }
        }

        //create nonCollapsedCells list, index based
        for (int i = 0; i < cellCount; i++)
        {
            nonCollapsedCells.Add(new Cell(i, tileCount));
        }
    }



    #region GPU BASED RENDERING DATA

    private Mesh[] meshes;
    private Material[] materials;

    private NativeArray<MeshDrawData> meshData;
    private NativeArray<Matrix4x4> matrixData;
    private int matrixId;

    public void AddMeshData(int meshId, Matrix4x4 matrix)
    {
        matrixData[matrixId] = matrix;

        meshData[meshId].AddMatrix(matrixId);

        matrixId += 1;
    }


    [System.Serializable]
    public struct MeshDrawData
    {
        public NativeList<int> matrixIds;

        public int meshIndex;


        public MeshDrawData(int _meshIndex, int _matrixCount)
        {
            meshIndex = _meshIndex;

            matrixIds = new NativeList<int>(_matrixCount, Allocator.Persistent);
        }

        public void AddMatrix(int matrixId)
        {
            matrixIds.AddNoResize(matrixId);
        }
    }

    #endregion

    private void Update()
    {
        //DEBUG_cells = cells.ToArray();
        //DEBUG_nonCollapsedCells = nonCollapsedCells.AsArray().ToArray();

        if (useGPUBasedRendering == false)
        {
            return;
        }


        for (int i = 0; i < meshData.Length; i++)
        {
            MeshDrawData cMeshDrawData = meshData[i];

            int matrixCount = cMeshDrawData.matrixIds.Length;

            if (matrixCount == 0)
            {
                continue;
            }

            // Prepare a NativeArray of matrices for this mesh
            NativeArray<Matrix4x4> meshMatrices = new NativeArray<Matrix4x4>(matrixCount, Allocator.Temp);

            // Copy the relevant matrices into the temporary array
            for (int j = 0; j < matrixCount; j++)
            {
                meshMatrices[j] = matrixData[cMeshDrawData.matrixIds[j]];
            }

            // Create RenderParams
            RenderParams renderParams = new RenderParams(materials[i])
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 100),
            };

            // Render the instances
            Graphics.RenderMeshInstanced(renderParams, meshes[cMeshDrawData.meshIndex], 0, meshMatrices);

            // Dispose of the temporary NativeArray
            meshMatrices.Dispose();
        }
    }



    public int cellUpdateTimeMs;
    public int cellUpdateCount;

    public Stopwatch sw;

    [BurstCompile]
    private async Task WaveFunctionLoop(int startCellId, NativeArray<int> requiredTileConnections, NativeArray<Cell> neighbours, NativeArray<int> toRandomizeTilePool, int cellCount, int tileCount)
    {
        int loopsLeftBeforeTaskDelay = cellUpdateCount;

        //select first start Cell
        Cell currentCell = cells[startCellId];

        //update startCell tileRequirement Data
        UpdateCell(currentCell, requiredTileConnections, neighbours, tileCount);

        for (int i = cellCount - 1; i > -1; i--)
        {
            if (Application.isPlaying == false)
            {
                return;
            }

            //spawn tile
            GenerateCurrentCellTile(currentCell, requiredTileConnections, neighbours, toRandomizeTilePool, tileCount, cellCount);

            //if all cells have been spawned, end loop
            if (i == 0)
            {
                break;
            }

            //another cell is collapsed
            cellCount -= 1;


            //select new tile
            currentCell = SelectNewCell(toRandomizeTilePool, cellCount);



            loopsLeftBeforeTaskDelay -= 1;

            if (loopsLeftBeforeTaskDelay <= 0)
            {
                loopsLeftBeforeTaskDelay = cellUpdateCount;

                await Task.Delay(cellUpdateTimeMs);
            }
        }

        requiredTileConnections.Dispose();
        neighbours.Dispose();
        toRandomizeTilePool.Dispose();

        cells.Dispose();
        nonCollapsedCells.Dispose();
        //cellTileOptions.Dispose();
        tileStructs.Dispose();

        print("Took: " + sw.ElapsedMilliseconds + " ms");
    }




    [BurstCompile]
    private void GenerateCurrentCellTile(Cell currentCell, NativeArray<int> requiredTileConnections, NativeArray<Cell> neighbours, NativeArray<int> toRandomizeTilePool, int tileCount, int nonCollapsedCellCount)
    {
        CalculateRequiredTileConnections(currentCell, neighbours, ref requiredTileConnections);


        #region Check which tiles are an option to generate

        int toRandomizeIndex = 0;

        for (int tileId = 0; tileId < tileCount; tileId++)
        {
            bool isTileCompatible = true;

            //check if all sides of the current to Check tile match with the neigbours
            for (int connectorId = 0; connectorId < 6; connectorId++)
            {
                int toCheckTileConnection = tileStructConnectors[tileId * 6 + connectorId];
                int requiredConnection = requiredTileConnections[connectorId];


                //bitwise check operation
                //Check if requiredConnection isnt -1 (every combination allowed) and there isnt any matching bits between the two connections
                if (requiredConnection != -1 && (requiredConnection & toCheckTileConnection) == 0)
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


        #region Generate random int for selecting a random tile from the possible options

        float maxChanceValue = 0;
        for (int i = 0; i < toRandomizeIndex; i++)
        {
            maxChanceValue += tileStructs[toRandomizeTilePool[i]].weight;
        }

        float rChanceValue = Random.Range(0, maxChanceValue);
        float tileRarity;


        int r = -1;

        for (int i = 0; i < toRandomizeIndex; i++)
        {
            tileRarity = tileStructs[toRandomizeTilePool[i]].weight;
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

        if (r == -1 || toRandomizeIndex == 0)
        {
            UnityEngine.Debug.LogWarning("NO TILES FOUND, r = " + r + ", randomIndex = " + toRandomizeIndex);
            r = 0;
        }

        #endregion


        int finalTileType = toRandomizeTilePool[r];

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


        #region Instantiate Or Add To GPU Render List

        //get gridPos
        int3 gridPos = LinearIndexToGridPos(currentCell.gridId);

        if (useGPUBasedRendering)
        {
            Vector3 pos = new Vector3(
                gridWorldPos.x + gridPos.x - gridSize.x * 0.5f + 0.5f,
                gridWorldPos.y + gridPos.y - gridSize.y * 0.5f,
                gridWorldPos.z + gridPos.z - gridSize.z * 0.5f + 0.5f);

            AddMeshData(finalTileType, Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one));
        }
        else
        {
            Vector3 pos = new Vector3(
                gridWorldPos.x + gridPos.x - gridSize.x * 0.5f + 0.5f,
                gridWorldPos.y + gridPos.y - gridSize.y * 0.5f,
                gridWorldPos.z + gridPos.z - gridSize.z * 0.5f + 0.5f);

            WaveTile spawnedObj = Instantiate(tilePrefabData[finalTileType].tilePrefab, pos, Quaternion.identity);

            if (randomColor)
            {
                Color color = Random.RandomColor();
                foreach (Renderer ren in spawnedObj.GetComponentsInChildren<Renderer>(true))
                {
                    ren.material.color = color;
                }
            }

            spawnedObj.DEBUG_connectors = DEBUG_connecotrs;

            //cellTileOptionArrayIndex + tileCount stores the amount of tileOptions int
            //int tileOptionsCount = cellTileOptions[cellTileOptionArrayIndex + tileCount];

            //spawnedObj.DEBUG_tileOptions = new GameObject[tileOptionsCount];
            //for (int i = 0; i < tileOptionsCount; i++)
            //{
            //    spawnedObj.DEBUG_tileOptions[i] = tilePrefabData[cellTileOptions[cellTileOptionArrayIndex + i]].tilePrefab.gameObject;
            //}
        }

        #endregion
    }


    [BurstCompile]
    private void CalculateRequiredTileConnections(Cell currentCell, NativeArray<Cell> neighbours, ref NativeArray<int> requiredTileConnections)
    {
        GetNeighbourCells(currentCell.gridId, ref neighbours);


        for (int neigbourId = 0; neigbourId < 6; neigbourId++)
        {
            //skip unitialized neighbours and mark them as out of bounds (because they are outside of the grid
            if (neighbours[neigbourId].initialized == false)
            {
                //2 (bitId = 1) is wall
                requiredTileConnections[neigbourId] = edgeRequiredConnection;
                continue;
            }

            //if neigbour is collapsed
            if (neighbours[neigbourId].collapsed)
            {
                int tileType = neighbours[neigbourId].tileType;

                requiredTileConnections[neigbourId] = tileStructConnectors[tileType * 6 + InvertDirection(neigbourId)];
            }
            else
            {
                //-1 (bitId = everything) is fully filles with bits
                requiredTileConnections[neigbourId] = -1;
            }
        }
    }




    [BurstCompile]
    private void UpdateCell(Cell currentCell, NativeArray<int> requiredTileConnections, NativeArray<Cell> neighbours, int tileCount)
    {
        GetRequiredTileConnections(currentCell, neighbours, ref requiredTileConnections);

        int tileOptionCount = 0;

        for (int tileId = 0; tileId < tileCount; tileId++)
        {
            bool isTileCompatible = true;

            //check if all sides of the current to Check tile match with the neigbours
            for (int connectorId = 0; connectorId < 6; connectorId++)
            {
                int toCheckTileConnection = tileStructConnectors[tileId * 6 + InvertDirection(connectorId)];
                int requiredConnection = requiredTileConnections[InvertDirection(connectorId)];


                //bitwise check operation
                //Check if there isnt any matching bits between the two connections
                if ((requiredConnection & toCheckTileConnection) == 0)
                {
                    isTileCompatible = false;

                    break;
                }
            }



            //all directions of currentTile are valid, increment tileOptionsCount
            if (isTileCompatible)
            {
                tileOptionCount += 1;
            }
        }


        //get currnetCell copy
        Cell updatedCell = nonCollapsedCells[currentCell.listId];

        //modify copy
        updatedCell.tileOptionCount = tileOptionCount;

        //update cell back
        nonCollapsedCells[currentCell.listId] = updatedCell;
    }


    [BurstCompile]
    private void GetRequiredTileConnections(Cell currentCell, NativeArray<Cell> neighbours, ref NativeArray<int> requiredTileConnections)
    {
        GetNeighbourCells(currentCell.gridId, ref neighbours);

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

                requiredTileConnections[neigbourId] = tileStructConnectors[tileType * 6 + neigbourId];
            }
        }
    }


    [BurstCompile]
    private void CollapseCell(Cell currentCell, int finalTileType, int nonCollapsedCellCount)
    {
        // Use the cell's listIndex to directly access its position
        int listId = currentCell.listId;


        //get swapped from the back cell in nonCollapsedCell list
        Cell swappedCell = nonCollapsedCells[nonCollapsedCellCount - 1];
        swappedCell.listId = listId;

        //update swappedcell
        nonCollapsedCells[listId] = swappedCell;

        //remove last cell
        nonCollapsedCells.RemoveAt(nonCollapsedCellCount - 1);


        //get swapped from back cell equal in cell array
        Cell cellArrayCell = cells[swappedCell.gridId];
        cellArrayCell.listId = listId;

        //update cellArrayCell
        cells[swappedCell.gridId] = cellArrayCell;



        //collapse copy of cell
        currentCell.collapsed = true;

        //update copy of cell
        currentCell.tileType = finalTileType;

        //save copy back
        cells[currentCell.gridId] = currentCell;
    }


    [BurstCompile]
    private Cell SelectNewCell(NativeArray<int> toRandomizeTilePool, int nonCollapsedCellCount)
    {
        //if there is just 1 cell left, select it and return the function
        if (nonCollapsedCellCount == 1)
        {
            return nonCollapsedCells[0];
        }


        //select new cell based on how many tile options neighbour cells have left
        int leastOptions = int.MaxValue;
        int cellsToRandomizeId = 0;

        //pre create data for use in loop
        int tileOptionsCount;

        //loop over all cells
        for (int i = 0; i < nonCollapsedCellCount; i++)
        {
            tileOptionsCount = nonCollapsedCells[i].tileOptionCount;


            //if cell specifically has less options
            if (tileOptionsCount < leastOptions)
            {
                leastOptions = tileOptionsCount;

                cellsToRandomizeId = 0;
                toRandomizeTilePool[cellsToRandomizeId++] = i;
            }

            //else if cell had the same amount of options
            else if (tileOptionsCount == leastOptions)
            {
                toRandomizeTilePool[cellsToRandomizeId++] = i;
            }
        }

        int r = Random.Range(0, cellsToRandomizeId);

        return nonCollapsedCells[toRandomizeTilePool[r]];
    }


    [BurstCompile]
    private void GetNeighbourCells(int cellId, ref NativeArray<Cell> neighbourCells)
    {
        // Convert cellId to gridPos
        int3 gridPos = LinearIndexToGridPos(cellId);

        //reset
        for (int i = 0; i < 6; i++)
        {
            neighbourCells[i] = new Cell();
        }


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




    #region Extension Grid Methods

    [BurstCompile]
    private int InvertDirection(int side)
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
    private int3 LinearIndexToGridPos(int linearIndex)
    {
        int3 gridPos;
        gridPos.z = linearIndex / (gridSize.x * gridSize.y);
        gridPos.y = (linearIndex % (gridSize.x * gridSize.y)) / gridSize.x;
        gridPos.x = linearIndex % gridSize.x;

        return gridPos;
    }

    [BurstCompile]
    private int GridPosToLinearIndex(int3 gridPos)
    {
        return
        gridPos.x +
        gridPos.y * gridSize.x +
        gridPos.z * gridSize.x * gridSize.y;
    }

    [BurstCompile]
    private void GridPosToLinearIndex(int3[] gridPositions, ref NativeArray<int> linearIndexs)
    {
        int positionAmount = gridPositions.Length;

        for (int i = 0; i < positionAmount; i++)
        {
            int3 gridPos = gridPositions[i];

            linearIndexs[i] =
                gridPos.x +
                gridPos.y * gridSize.x +
                gridPos.z * gridSize.x * gridSize.y;
        }
    }

    #endregion




    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position, new Vector3(gridSize.x, gridSize.y, gridSize.z));
    }

    private void OnValidate()
    {
        if (seed == 0)
        {
            seed = 1;
        }
    }
}