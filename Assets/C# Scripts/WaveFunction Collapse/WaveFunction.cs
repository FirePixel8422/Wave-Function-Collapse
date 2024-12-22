using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
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

    [SerializeField] private int3 splittedChunkSize;


    [SerializeField] private uint seed;
    [SerializeField] private bool randomSeed;

    [Header("what tile connection bitId are gridEdges")]
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

        int cellCount = gridSize.x / splittedChunkSize.x * gridSize.y / splittedChunkSize.y * gridSize.z / splittedChunkSize.z;
        int tileCount = tilePrefabData.Length;

        float3 gridWorldPos = new float3(transform.position.x, transform.position.y, transform.position.z);



        #region Setup Cell Data Job

        cells = new NativeArray<Cell>(cellCount, Allocator.Persistent);
        nonCollapsedCells = new NativeList<Cell>(cellCount, Allocator.Persistent);

        SetupCellData_JobParallel setupDataJob = new SetupCellData_JobParallel()
        {
            cells = cells,
            nonCollapsedCells = nonCollapsedCells.AsParallelWriter(),
            tileCount = tileCount
        };

        JobHandle mainJobHandle = setupDataJob.Schedule(cellCount, cellCount);

        #endregion


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


        //selec random startCell
        int startCellId = Random.Range(0, cellCount);

        //gets used for randomizing cell selection and tile selection, capicity is whatever are more of, cells or tileTypes
        NativeArray<int> toRandomizeTilePool = new NativeArray<int>(math.max(tileCount, cellCount), Allocator.Persistent);

        //data arrays to use for calculations
        NativeArray<int> requiredTileConnections = new NativeArray<int>(6, Allocator.Persistent);
        NativeArray<Cell> neighbours = new NativeArray<Cell>(6, Allocator.Persistent);


        //force job completion before starting generation
        mainJobHandle.Complete();

        await WaveFunctionLoop(startCellId, requiredTileConnections, neighbours, toRandomizeTilePool, cellCount, tileCount, gridWorldPos);

        //debug
        print("Took: " + sw.ElapsedMilliseconds + " ms");
    }


    [BurstCompile]
    private struct SetupCellData_JobParallel : IJobParallelFor
    {
        [NoAlias][WriteOnly] public NativeArray<Cell> cells;

        [NativeDisableParallelForRestriction]
        [NoAlias][WriteOnly] public NativeList<Cell>.ParallelWriter nonCollapsedCells;

        [NoAlias][ReadOnly] public int tileCount;


        [BurstCompile]
        public void Execute(int cellId)
        {
            Cell addedCell = new Cell(cellId, tileCount);

            cells[cellId] = addedCell;
            nonCollapsedCells.AddNoResize(addedCell);
        }
    }


    [BurstCompile]
    private void SetupGPURenderMeshData(int tileCount, int cellCount)
    {
        meshData = new NativeArray<MeshDrawData>(tileCount, Allocator.Persistent);

        meshes = new Mesh[tileCount];

        matrixData = new NativeArray<Matrix4x4>(cellCount, Allocator.Persistent);
        meshMatrices = new NativeArray<Matrix4x4>(cellCount, Allocator.Persistent);

        renderParams = new RenderParams[tileCount];

        for (int i = 0; i < tileCount; i++)
        {
            meshData[i] = new MeshDrawData(cellCount);

            MeshFilter[] meshFilters = tilePrefabData[i].tilePrefab.GetComponentsInChildren<MeshFilter>(true);

            CombineInstance[] combineInstances = new CombineInstance[meshFilters.Length];

            for (int meshId = 0; meshId < meshFilters.Length; meshId++)
            {
                combineInstances[meshId].mesh = meshFilters[meshId].sharedMesh;

                Matrix4x4 meshMatrix = meshFilters[meshId].transform.localToWorldMatrix;

                combineInstances[meshId].transform = meshMatrix;
            }

            meshes[i] = new Mesh();
            meshes[i].CombineMeshes(combineInstances, true, true, true);

            MeshRenderer renderer = tilePrefabData[i].tilePrefab.GetComponentInChildren<MeshRenderer>();

            //save material to renderParams
            renderParams[i] = new RenderParams()
            {
                material = renderer == null ? new Material(Shader.Find("Standard")) : renderer.sharedMaterial,

                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                receiveShadows = true,

                motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,

                worldBounds = new Bounds(Vector3.zero, Vector3.one * 100),
            };
        }
    }




    #region GPU BASED RENDERING DATA

    private Mesh[] meshes;

    //stores material
    private RenderParams[] renderParams;

    private NativeArray<MeshDrawData> meshData;

    private NativeArray<Matrix4x4> matrixData;
    private int matrixId;

    private NativeArray<Matrix4x4> meshMatrices;

    public void AddMeshData(int meshId, Matrix4x4 matrix)
    {
        matrixData[matrixId] = matrix;

        meshData[meshId].AddMatrixId(matrixId);

        matrixId += 1;
    }


    [System.Serializable]
    public struct MeshDrawData
    {
        //last id is the total added matrixCount
        public NativeArray<int> matrixIds;
        private readonly int matrixCountId;

        public readonly int MatrixCount => matrixIds[matrixCountId];


        public MeshDrawData(int _matrixCount)
        {
            matrixIds = new NativeArray<int>(_matrixCount + 1, Allocator.Persistent);
            matrixCountId = _matrixCount;
        }

        public void AddMatrixId(int matrixId)
        {
            int cMatrixId = matrixIds[matrixCountId]++;

            matrixIds[cMatrixId] = matrixId;
        }
    }

    #endregion

   


    private void Update()
    {
        if (useGPUBasedRendering == false)
        {
            return;
        }


        for (int meshIndex = 0; meshIndex < meshData.Length; meshIndex++)
        {
            MeshDrawData cMeshDrawData = meshData[meshIndex];

            int matrixCount = cMeshDrawData.MatrixCount;

            if (matrixCount == 0)
            {
                continue;
            }

            // Copy the relevant matrices into the temporary array
            for (int i2 = 0; i2 < matrixCount; i2++)
            {
                meshMatrices[i2] = matrixData[cMeshDrawData.matrixIds[i2]];
            }

            // Render the instances
            Graphics.RenderMeshInstanced(renderParams[meshIndex], meshes[meshIndex], 0, meshMatrices, matrixCount);
        }
    }




    public int cellUpdateTimeMs;
    public int cellUpdateCount;

    public Stopwatch sw;


    [BurstCompile]
    private async Task WaveFunctionLoop(int startCellId, NativeArray<int> requiredTileConnections, NativeArray<Cell> neighbours, NativeArray<int> toRandomizeTilePool, int cellCount, int tileCount, float3 gridWorldPos)
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
            GenerateCurrentCellTile(currentCell, requiredTileConnections, neighbours, toRandomizeTilePool, tileCount, cellCount, gridWorldPos);

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
        tileStructs.Dispose();
    }




    [BurstCompile]
    private void GenerateCurrentCellTile(Cell currentCell, NativeArray<int> requiredTileConnections, NativeArray<Cell> neighbours, NativeArray<int> toRandomizeTilePool, int tileCount, int nonCollapsedCellCount, float3 gridWorldPos)
    {
        CalculateTileConnections(currentCell, neighbours, ref requiredTileConnections);


        #region Check which tiles are an option to generate

        int toRandomizeIndex = 0;

        for (int tileId = 0; tileId < tileCount; tileId++)
        {
            bool isTileCompatible = true;

            //check if all sides of the current to Check tile match with the neigbours
            for (int connectorId = 0; connectorId < 6; connectorId++)
            {
                int requiredConnection = requiredTileConnections[connectorId];


                //bitwise check operation
                //Check if requiredConnection isnt -1 (every combination allowed) and there isnt any matching bits between the two connections
                if (requiredConnection != -1 && (requiredConnection & tileStructConnectors[tileId * 6 + connectorId]) == 0)
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

        //if (r == -1 || toRandomizeIndex == 0)
        //{
        //    UnityEngine.Debug.LogWarning("NO TILES FOUND, r = " + r + ", randomIndex = " + toRandomizeIndex);
        //    r = 0;
        //}

        #endregion


        int finalTileType = toRandomizeTilePool[r];

        //collapse current cell
        CollapseCell(currentCell, finalTileType, nonCollapsedCellCount);


        GetNeighbourCells(currentCell.gridId, ref neighbours);

        for (int i = 0; i < neighbours.Length; i++)
        {
            //skip non existent or already collapsed neighbours
            if (neighbours[i].IsUnInitializedOrCollapsed)
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
                foreach (Renderer renderer in spawnedObj.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.material.color = color;
                }
            }

            //spawnedObj.DEBUG_connectors = DEBUG_connecotrs;

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
    private void CalculateTileConnections(Cell currentCell, NativeArray<Cell> neighbours, ref NativeArray<int> requiredTileConnections)
    {
        GetNeighbourCells(currentCell.gridId, ref neighbours);


        for (int neigbourId = 0; neigbourId < 6; neigbourId++)
        {
            //skip unitialized neighbours and mark them as out of bounds (because they are outside of the grid
            if (neighbours[neigbourId].initialized == false)
            {
                requiredTileConnections[neigbourId] = edgeRequiredConnection;
                continue;
            }

            //if neigbour is collapsed
            if (neighbours[neigbourId].collapsed)
            {
                requiredTileConnections[neigbourId] = tileStructConnectors[neighbours[neigbourId].tileType * 6 + InvertDirection(neigbourId)];
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

            int tileConnectorStartIndex = tileId * 6;

            //check if all sides of the current to Check tile match with the neigbours
            for (int connectorId = 0; connectorId < 6; connectorId++)
            {
                //Check if there isnt any matching bits between the two connections
                if ((requiredTileConnections[connectorId] & tileStructConnectors[tileConnectorStartIndex + connectorId]) == 0)
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


        //get currentCell copy, modify it and write it back
        Cell updatedCell = nonCollapsedCells[currentCell.listId];

        updatedCell.tileOptionCount = tileOptionCount;

        nonCollapsedCells[currentCell.listId] = updatedCell;
    }


    [BurstCompile]
    private void GetRequiredTileConnections(Cell currentCell, NativeArray<Cell> neighbours, ref NativeArray<int> requiredTileConnections)
    {
        GetNeighbourCells(currentCell.gridId, ref neighbours);


        for (int neigbourId = 0; neigbourId < 6; neigbourId++)
        {
            //skip unitialized and uncollapsed neighbours
            if (neighbours[neigbourId].IsInitializedAndCollapsed == false)
            {
                continue;
            }

            requiredTileConnections[neigbourId] = tileStructConnectors[neighbours[neigbourId].tileType * 6 + neigbourId];
        }
    }


    [BurstCompile]
    private void GetNeighbourCells(int cellId, ref NativeArray<Cell> neighbourCells)
    {
        // Convert cellId to gridPos
        int3 gridPos = LinearIndexToGridPos(cellId);


        #region Check and add / mark unintialized neighbors in all directions

        if (gridPos.x > 0) // Left
        {
            neighbourCells[0] = cells[cellId - 1];
        }
        else
        {
            neighbourCells[0] = Cell.Uninitialized();
        }

        if (gridPos.x < gridSize.x - 1) // Right
        {
            neighbourCells[1] = cells[cellId + 1];
        }
        else
        {
            neighbourCells[1] = Cell.Uninitialized();
        }


        if (gridPos.y < gridSize.y - 1) // Up
        {
            neighbourCells[2] = cells[cellId + gridSize.x];
        }
        else
        {
            neighbourCells[2] = Cell.Uninitialized();
        }

        if (gridPos.y > 0) // Down
        {
            neighbourCells[3] = cells[cellId - gridSize.x];
        }
        else
        {
            neighbourCells[3] = Cell.Uninitialized();
        }


        if (gridPos.z < gridSize.z - 1) // Front
        {
            neighbourCells[4] = cells[cellId + gridSize.x * gridSize.y];
        }
        else
        {
            neighbourCells[4] = Cell.Uninitialized();
        }

        if (gridPos.z > 0) // Back
        {
            neighbourCells[5] = cells[cellId - gridSize.x * gridSize.y];
        }
        else
        {
            neighbourCells[5] = Cell.Uninitialized();
        }

        #endregion
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


        //get swapped from back cell counterpart from cell array
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

                //reset randomize pool
                toRandomizeTilePool[0] = i;
                cellsToRandomizeId = 1;
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

        //force 1,1,1 splitChunkSize
        for (int i = 0; i < 3; i++)
        {
            if (splittedChunkSize[i] < 1)
            {
                splittedChunkSize[i] = 1;
            }
        }
    }
}