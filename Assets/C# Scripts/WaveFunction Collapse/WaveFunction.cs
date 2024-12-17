using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;


[BurstCompile]
public class WaveFunction : MonoBehaviour
{
    public static WaveFunction Instance;
    private void Awake()
    {
        Instance = this;
    }


    [SerializeField] private GameObject CUBE;

    [Header("Tiles used for generation")]
    [SerializeField] private GenerationTilesSO generationTiles;

    [Header("3D size of the Grid to generate in")]
    [SerializeField] private int3 gridSize;

    [Header("Start Cell GridPositions, overriden by randomStartCellCount")]
    [SerializeField] private int3[] startCellPositions;

    [Header("How Many Random Start Cell Grid Positions")]
    [Range(0, 25)]
    [SerializeField] private int randomStartCellCount;

    [SerializeField] private bool useGPUBasedRendering;
    [SerializeField] private bool randomColor;



    private NativeArray<Cell> cells;
    private NativeList<Cell> nonColapsedCells;

    private NativeArray<int> cellTileOptions;

    //Native structure based copy of generationTiles.tilePrefabs data, so burst can go brrr
    private NativeArray<TileStruct> tileStructs;

    [SerializeField] private float3 gridWorldPos;




    //DEBUG
    private Cell[] DEBUG_cells;



    [BurstCompile]
    private async void Start()
    {
        sw = Stopwatch.StartNew();

        //calculate total amount of cells in the 3D grid
        int cellCount = gridSize.x * gridSize.y * gridSize.z;

        //get total amount of tiles
        int tileCount = generationTiles.tilePrefabs.Length;

        gridWorldPos = new float3(transform.position.x, transform.position.y, transform.position.z);


        NativeArray<int> startCellGridIndexs;

        if (randomStartCellCount > 0)
        {
            int startCellCount = math.min(cellCount, randomStartCellCount);

            startCellGridIndexs = new NativeArray<int>(startCellCount, Allocator.Persistent);

            CalculateRandomGridCellIndexs(ref startCellGridIndexs, startCellCount, cellCount);
        }
        else
        {
            startCellGridIndexs = new NativeArray<int>(startCellPositions.Length, Allocator.Persistent);

            GridPosToLinearIndex(startCellPositions, ref startCellGridIndexs);
        }




        SetupGPURenderMeshData(tileCount, cellCount);


        cells = new NativeArray<Cell>(cellCount, Allocator.Persistent);
        nonColapsedCells = new NativeList<Cell>(cellCount, Allocator.Persistent);

        //works as 2d array, every cell gets tileCount + 1 amount of this array, the +1 is for the tileOptionsCount of every cell
        cellTileOptions = new NativeArray<int>(cellCount * (tileCount + 1), Allocator.Persistent);
        for (int i = 0; i < cellCount; i++)
        {
            //set every tileOptionsCount equal to the amount of tiles
            cellTileOptions[i * (tileCount + 1) + tileCount] = tileCount;
        }


        tileStructs = new NativeArray<TileStruct>(tileCount, Allocator.Persistent);
        for (int i = 0; i < tileCount; i++)
        {
            tileStructs[i] = new TileStruct(i, generationTiles.tilePrefabs[i].weight, generationTiles.tilePrefabs[i].flippable, generationTiles.tilePrefabs[i].connectors);
        }

        //gets used for randomizing cell selection and tile selection, capicity is whatever are more of, cells or tileTypes
        NativeArray<int> toRandomizeTilePool = new NativeArray<int>(math.max(tileCount, cellCount), Allocator.Persistent);

        //data arrays to use for calculations
        NativeArray<int> requiredTileConnections = new NativeArray<int>(6, Allocator.Persistent);
        NativeArray<Cell> neighbours = new NativeArray<Cell>(6, Allocator.Persistent);

        //create the grid
        CreateGrid(tileCount);

        await WaveFunctionLoop(startCellGridIndexs, requiredTileConnections, neighbours, toRandomizeTilePool, cellCount, tileCount);
    }


    [BurstCompile]
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
    }


    [BurstCompile]
    private void SetupGPURenderMeshData(int tileCount, int cellCount)
    {
        NativeArray<MeshDrawData> _meshDrawData = new NativeArray<MeshDrawData>(tileCount, Allocator.Persistent);

        matrixData = new NativeArray<Matrix4x4>(cellCount, Allocator.Persistent);
        meshes = new Mesh[tileCount];

        for (int i = 0; i < tileCount; i++)
        {
            _meshDrawData[i] = new MeshDrawData(i, cellCount);

            MeshFilter[] meshFilters = generationTiles.tilePrefabs[i].GetComponentsInChildren<MeshFilter>(true);

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
    }


    [BurstCompile]
    private void CreateGrid(int tileCount)
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

                    Cell cell = new Cell(cellId);

                    cells[cellId] = cell;
                    nonColapsedCells.Add(cell);
                }
            }
        }

        DEBUG_cells = cells.ToArray();
    }



    #region GPU BASED RENDERING

    private Mesh[] meshes;
    public Material mat;

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


    private void Update()
    {
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
            RenderParams renderParams = new RenderParams(mat)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 100),
            };

            // Render the instances
            Graphics.RenderMeshInstanced(renderParams, meshes[cMeshDrawData.meshIndex], 0, meshMatrices);

            // Dispose of the temporary NativeArray
            meshMatrices.Dispose();
        }
    }

    #endregion



    public int cellUpdateTimeMs;
    public int cellUpdateCount;

    public Stopwatch sw;

    [BurstCompile]
    private async Task WaveFunctionLoop(NativeArray<int> startCellGridIndexs, NativeArray<int> requiredTileConnections, NativeArray<Cell> neighbours, NativeArray<int> toRandomizeTilePool, int cellCount, int tileCount)
    {
        int loopsLeftBeforeTaskDelay = cellUpdateCount;


        int startCellIndex = 0;
        int startCellCount = startCellGridIndexs.Length;

        //select first start Cell
        Cell currentCell = cells[startCellGridIndexs[0]];

        //update startCell tileRequirement Data
        UpdateCell(currentCell, requiredTileConnections, neighbours, tileCount);

        startCellIndex += 1;
    


        for (int i = 0; i < cellCount; i++)
        {
            if (Application.isPlaying == false)
            {
                return;
            }

            //spawn tile
            GenerateCurrentCellTile_OLD(currentCell, requiredTileConnections, neighbours, toRandomizeTilePool, tileCount);


            //if all cells have been spawned, end loop
            if (i == cellCount - 1)
            {
                break;
            }



            if (startCellIndex != startCellCount)
            {
                currentCell = cells[startCellGridIndexs[startCellIndex]];

                //update startCell tileRequirement Data
                UpdateCell(currentCell, requiredTileConnections, neighbours, tileCount);

                startCellIndex += 1;

                //dispose startCellGridIndexs after last cell is selected
                if (startCellIndex == startCellCount)
                {
                    startCellGridIndexs.Dispose();
                }
            }
            else
            {
                //select new tile
                currentCell = SelectNewCell(toRandomizeTilePool, tileCount);
            }



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
        nonColapsedCells.Dispose();
        cellTileOptions.Dispose();
        tileStructs.Dispose();

        print("Took: " + sw.ElapsedMilliseconds + " ms");
    }





    [BurstCompile]
    private void GenerateCurrentCellTile(Cell currentCell, NativeArray<int> requiredTileConnections, NativeArray<Cell> neighbours, int tileCount)
    {
        currentCell = cells[currentCell.worldId];

        //where in the tileOptionArray lies the data of this cell (cell.worldId * (amount of tileOptions + 1))
        int cellTileOptionArrayIndex = currentCell.worldId * (tileCount + 1);

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
        CollapseCell(currentCell, finalTileType);


        int[] DEBUG_connecotrs = new int[6];


        GetNeighbourCells(currentCell.worldId, ref neighbours);
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

            UpdateCell(neighbours[i], requiredTileConnections, neighbours, tileCount);
        }


        //get gridPos
        int3 gridPos = LinearIndexToGridPos(currentCell.worldId);

        //spawn tile
        WaveTile spawnedObj = Instantiate(generationTiles.tilePrefabs[finalTileType], new Vector3(gridPos.x - gridSize.x * 0.5f + 0.5f, gridPos.y - gridSize.y * 0.5f + 0.5f, gridPos.z - gridSize.z * 0.5f + 0.5f), generationTiles.tilePrefabs[finalTileType].transform.rotation);

        spawnedObj.DEBUG_connectors = DEBUG_connecotrs;

        //cellTileOptionArrayIndex + tileCount stores the amount of tileOptions int
        int tileOptionsCount = cellTileOptions[cellTileOptionArrayIndex + tileCount];

        spawnedObj.DEBUG_tileOptions = new GameObject[tileOptionsCount];

        for (int i = 0; i < tileOptionsCount; i++)
        {
            spawnedObj.DEBUG_tileOptions[i] = generationTiles.tilePrefabs[cellTileOptions[cellTileOptionArrayIndex + i]].gameObject;
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
    private void GenerateCurrentCellTile_OLD(Cell currentCell, NativeArray<int> requiredTileConnections, NativeArray<Cell> neighbours, NativeArray<int> toRandomizeTilePool, int tileCount)
    {
        //where in the tileOptionArray lies the data of this cell (cell.worldId * (amount of tileOptions + 1))
        int cellTileOptionArrayIndex = currentCell.worldId * (tileCount + 1);

        CalculateRequiredTileConnections_OLD(currentCell, neighbours, ref requiredTileConnections);


        #region Check which tiles are an option to generate

        int toRandomizeIndex = 0;

        for (int tileId = 0; tileId < tileCount; tileId++)
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

        int finalTileType = toRandomizeTilePool[r];

        //collapse current cell
        CollapseCell(currentCell, finalTileType);


        int[] DEBUG_connecotrs = new int[6];

        GetNeighbourCells(currentCell.worldId, ref neighbours);

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

            UpdateCell(neighbours[i], requiredTileConnections, neighbours, tileCount);
        }


        //get gridPos
        int3 gridPos = LinearIndexToGridPos(currentCell.worldId);


        if (useGPUBasedRendering)
        {
            Vector3 pos = new Vector3(
                gridWorldPos.x + gridPos.x - gridSize.x * 0.5f + 0.5f,
                gridWorldPos.y + gridPos.y - gridSize.y * 0.5f,
                gridWorldPos.z + gridPos.z - gridSize.z * 0.5f + 0.5f);
            Quaternion rot = generationTiles.tilePrefabs[finalTileType].transform.rotation;

            AddMeshData(finalTileType, Matrix4x4.TRS(pos, rot, Vector3.one));
        }
        else
        {
            //spawn tile
            WaveTile spawnedObj = Instantiate(generationTiles.tilePrefabs[finalTileType],
                new Vector3(
                gridWorldPos.x + gridPos.x - gridSize.x * 0.5f + 0.5f,
                gridWorldPos.y + gridPos.y - gridSize.y * 0.5f,
                gridWorldPos.z + gridPos.z - gridSize.z * 0.5f + 0.5f),
                generationTiles.tilePrefabs[finalTileType].transform.rotation);

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
            int tileOptionsCount = cellTileOptions[cellTileOptionArrayIndex + tileCount];

            spawnedObj.DEBUG_tileOptions = new GameObject[tileOptionsCount];
            for (int i = 0; i < tileOptionsCount; i++)
            {
                spawnedObj.DEBUG_tileOptions[i] = generationTiles.tilePrefabs[cellTileOptions[cellTileOptionArrayIndex + i]].gameObject;
            }
        }
    }


    [BurstCompile]
    private void CalculateRequiredTileConnections_OLD(Cell currentCell, NativeArray<Cell> neighbours, ref NativeArray<int> requiredTileConnections)
    {
        GetNeighbourCells(currentCell.worldId, ref neighbours);


        for (int neigbourId = 0; neigbourId < 6; neigbourId++)
        {
            //skip unitialized neighbours and mark them as out of bounds (because they are outside of the grid
            if (neighbours[neigbourId].initialized == false)
            {
                //2 is wall
                requiredTileConnections[neigbourId] = 1;
                continue;
            }

            //if neigbour is collapsed
            if (neighbours[neigbourId].collapsed)
            {
                int tileType = neighbours[neigbourId].tileType;

                TileStruct targetTile = tileStructs[tileType];

                requiredTileConnections[neigbourId] = targetTile.connectors[OppositeSide(neigbourId)];
            }
            else
            {
                requiredTileConnections[neigbourId] = 0;
            }
        }
    }



    [BurstCompile]
    private void UpdateCell(Cell currentCell, NativeArray<int> requiredTileConnections, NativeArray<Cell> neighbours, int tileCount)
    {
        GetRequiredTileConnections(currentCell, neighbours, ref requiredTileConnections);

        //where in the tileOptionArray lies the data of this cell (cell.worldId * (amount of tileOptions + 1))
        int cellTileOptionArrayIndex = currentCell.worldId * (tileCount + 1);

        int toRandomizeIndex = 0;

        for (int tileId = 0; tileId < tileCount; tileId++)
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
                cellTileOptions[cellTileOptionArrayIndex + toRandomizeIndex++] = tileId;
            }
        }

        //cellTileOptionArrayIndex + tileCount + 1 stores the amount of tileOptions int
        cellTileOptions[cellTileOptionArrayIndex + tileCount] = toRandomizeIndex;

        //update cell back
        cells[currentCell.worldId] = currentCell;

        DEBUG_cells = cells.ToArray();
    }


    [BurstCompile]
    private void GetRequiredTileConnections(Cell currentCell, NativeArray<Cell> neighbours, ref NativeArray<int> requiredTileConnections)
    {
        GetNeighbourCells(currentCell.worldId, ref neighbours);

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
        int cellCount = nonColapsedCells.Length;

        int startId = math.min(cellCount - 1, currentCell.worldId);

        bool cellFound = false;

        //remove cell from nonCollapsedList
        for (int i = startId; i > -1; i--)
        {
            if (nonColapsedCells[i].worldId == currentCell.worldId)
            {
                nonColapsedCells.RemoveAt(i);
                cellFound = true;
                break;
            }
        }

        if (cellFound == false)
        {
            for (int i = startId; i < cellCount; i++)
            {
                if (nonColapsedCells[i].worldId == currentCell.worldId)
                {
                    nonColapsedCells.RemoveAt(i);
                    break;
                }
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
    private Cell SelectNewCell(NativeArray<int> toRandomizeTilePool, int tileCount)
    {
        int nonCollapsedCellCount = nonColapsedCells.Length;

        //if there is just 1 cell left, select it and return the function
        if (nonCollapsedCellCount == 1)
        {
            return cells[nonColapsedCells[0].worldId];
        }


        //select new cell based on how many tile options neighbour cells have left
        int leastOptions = int.MaxValue;
        int cellsToRandomizeId = 0;

        //loop over all cells
        for (int i = 0; i < nonCollapsedCellCount; i++)
        {
            Cell targetCell = nonColapsedCells[i];

            //if cell is colapsed, skip it
            if (targetCell.collapsed)
            {
                continue;
            }

            //where in the tileOptionArray lies the data of this cell (cell.worldId * (amount of tileOptions + 1))
            int cellTileOptionArrayIndex = targetCell.worldId * (tileCount + 1);

            //cellTileOptionArrayIndex + tileCount stores the amount of tileOptions int
            int tileOptionsCount = cellTileOptions[cellTileOptionArrayIndex + tileCount];


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

        return cells[nonColapsedCells[toRandomizeTilePool[r]].worldId];
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
}