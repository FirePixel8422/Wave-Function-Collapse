//using System.Collections.Generic;
//using System.Threading.Tasks;
//using Unity.Burst;
//using Unity.Collections;
//using Unity.Mathematics;
//using UnityEngine;


//[BurstCompile]
//public class WaveFunction : MonoBehaviour
//{
//    public static WaveFunction Instance;
//    private void Awake()
//    {
//        Instance = this;
//    }


//    [SerializeField] private GameObject CUBE;

//    [Header("Tiles used for generation")]
//    [SerializeField] private WaveTile[] tilePrefabs;

//    [Header("3D size of the Grid to generate in")]
//    [SerializeField] private int3 gridSize;

//    [Header("Start Cell GridPositions, overriden by randomStartCellCount")]
//    [SerializeField] private int3[] startCellPositions;

//    [Header("How Many Random Start Cell Grid Positions")]
//    [Range(0, 25)]
//    [SerializeField] private int randomStartCellCount;




//    private NativeArray<Cell> cells;
//    private List<Cell> nonColapsedCells;


//    //Native structure based copy of tilePrefabs data, so burst can go brrr
//    private NativeArray<TileStruct> tileStructs;

//    [SerializeField] private bool useGPUBasedRendering;
//    [SerializeField] private bool randomColor;



//    //DEBUG
//    private Cell[] DEBUG_cells;



//    [BurstCompile]
//    private async void Start()
//    {
//        //calculate total amount of cells in the 3D grid
//        int cellCount = gridSize.x * gridSize.y * gridSize.z;

//        //get total amount of tiles
//        int tileCount = tilePrefabs.Length;


//        NativeArray<int> startCellGridIndexs;
//        NativeArray<Cell> neighbours = new NativeArray<Cell>(6, Allocator.Persistent);

//        if (randomStartCellCount > 0)
//        {
//            startCellGridIndexs = new NativeArray<int>(randomStartCellCount, Allocator.Persistent);

//            CalculateRandomGridCellIndexs(ref startCellGridIndexs, randomStartCellCount, cellCount);
//        }
//        else
//        {
//            startCellGridIndexs = new NativeArray<int>(startCellPositions.Length, Allocator.Persistent);
//            GridPosToLinearIndex(startCellPositions, ref startCellGridIndexs);
//        }




//        SetupGPURenderMeshData(tileCount, cellCount);


//        cells = new NativeArray<Cell>(cellCount, Allocator.Persistent);
//        nonColapsedCells = new List<Cell>(cellCount);


//        tileStructs = new NativeArray<TileStruct>(tileCount, Allocator.Persistent);
//        toRandomizeTilePool = new NativeArray<int>(math.max(tileCount, cellCount), Allocator.Persistent);

//        NativeArray<int> requiredTileConnections = new NativeArray<int>(6, Allocator.Persistent);

//        for (int i = 0; i < tileCount; i++)
//        {
//            tileStructs[i] = new TileStruct(i, tilePrefabs[i].rarity, tilePrefabs[i].flippable, tilePrefabs[i].connectors);
//        }

//        //create the grid
//        CreateGrid(tileCount);

//        await WaveFunctionLoop(startCellGridIndexs, requiredTileConnections, neighbours, cellCount);
//    }


//    [BurstCompile]
//    private void CalculateRandomGridCellIndexs(ref NativeArray<int> startCellGridIndexs, int randomStartCellCount, int cellCount)
//    {
//        NativeList<int> gridPositionIndexList = new NativeList<int>(cellCount, Allocator.Temp);

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

//                    gridPositionIndexList.Add(cellId);
//                }
//            }
//        }


//        int r;
//        for (int i = 0; i < randomStartCellCount; i++)
//        {
//            r = Random.Range(0, gridPositionIndexList.Length);

//            startCellGridIndexs[i] = gridPositionIndexList[r];

//            gridPositionIndexList.RemoveAtSwapBack(r);
//        }

//        gridPositionIndexList.Dispose();
//    }


//    [BurstCompile]
//    private void SetupGPURenderMeshData(int tileCount, int cellCount)
//    {
//        NativeArray<MeshDrawData> _meshDrawData = new NativeArray<MeshDrawData>(tileCount, Allocator.Persistent);

//        matrixData = new NativeArray<Matrix4x4>(cellCount, Allocator.Persistent);
//        meshes = new Mesh[tileCount];

//        for (int i = 0; i < tileCount; i++)
//        {
//            _meshDrawData[i] = new MeshDrawData(i, cellCount);

//            MeshFilter[] meshFilters = tilePrefabs[i].GetComponentsInChildren<MeshFilter>(true);

//            CombineInstance[] combineInstances = new CombineInstance[meshFilters.Length];

//            for (int meshId = 0; meshId < meshFilters.Length; meshId++)
//            {
//                combineInstances[meshId].mesh = meshFilters[meshId].sharedMesh;
//                combineInstances[meshId].transform = meshFilters[meshId].transform.localToWorldMatrix;
//            }

//            meshes[i] = new Mesh();
//            meshes[i].CombineMeshes(combineInstances);
//        }

//        meshData = _meshDrawData;
//    }


//    [BurstCompile]
//    private void CreateGrid(int tileCount)
//    {
//        NativeArray<int> tileOptions = new NativeArray<int>(tileCount, Allocator.Persistent);
//        for (int i = 0; i < tileCount; i++)
//        {
//            tileOptions[i] = i;
//        }


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

//                    Cell cell = new Cell(cellId, tileOptions, new int3(x, y, z));

//                    cells[cellId] = cell;
//                    nonColapsedCells.Add(cell);
//                }
//            }
//        }

//        DEBUG_cells = cells.ToArray();
//    }



//    #region GPU BASED RENDERING

//    private Mesh[] meshes;
//    public Material mat;

//    private NativeArray<MeshDrawData> meshData;
//    private NativeArray<Matrix4x4> matrixData;
//    private int matrixId;

//    public void AddMeshData(int meshId, Matrix4x4 matrix)
//    {
//        matrixData[matrixId] = matrix;

//        meshData[meshId].AddMatrix(matrixId);

//        matrixId += 1;
//    }


//    [System.Serializable]
//    public struct MeshDrawData
//    {
//        public NativeList<int> matrixIds;

//        public int meshIndex;


//        public MeshDrawData(int _meshIndex, int _matrixCount)
//        {
//            meshIndex = _meshIndex;

//            matrixIds = new NativeList<int>(_matrixCount, Allocator.Persistent);
//        }

//        public void AddMatrix(int matrixId)
//        {
//            matrixIds.AddNoResize(matrixId);
//        }
//    }


//    private void Update()
//    {
//        if (useGPUBasedRendering == false)
//        {
//            return;
//        }


//        for (int i = 0; i < meshData.Length; i++)
//        {
//            MeshDrawData cMeshDrawData = meshData[i];

//            int matrixCount = cMeshDrawData.matrixIds.Length;

//            if (matrixCount == 0)
//            {
//                continue;
//            }

//            // Prepare a NativeArray of matrices for this mesh
//            NativeArray<Matrix4x4> meshMatrices = new NativeArray<Matrix4x4>(matrixCount, Allocator.Temp);

//            // Copy the relevant matrices into the temporary array
//            for (int j = 0; j < matrixCount; j++)
//            {
//                meshMatrices[j] = matrixData[cMeshDrawData.matrixIds[j]];
//            }

//            // Create RenderParams
//            RenderParams renderParams = new RenderParams(mat)
//            {
//                worldBounds = new Bounds(Vector3.zero, Vector3.one * 100),
//            };

//            // Render the instances
//            Graphics.RenderMeshInstanced(renderParams, meshes[cMeshDrawData.meshIndex], 0, meshMatrices);

//            // Dispose of the temporary NativeArray
//            meshMatrices.Dispose();
//        }
//    }

//    #endregion



//    public int cellUpdateTimeMs;
//    public int cellUpdateCount;

//    [BurstCompile]
//    private async Task WaveFunctionLoop(NativeArray<int> startCellGridIndexs, NativeArray<int> requiredTileConnections, NativeArray<Cell> neighbours, int cellCount)
//    {
//        int loopsLeftBeforeTaskDelay = cellUpdateCount;

//        Cell currentCell = new Cell();

//        int startCellIndex = 0;
//        int startCellCount = startCellGridIndexs.Length;

//        //select first start Cell
//        if (startCellIndex != startCellCount)
//        {
//            currentCell = cells[startCellGridIndexs[0]];

//            //update startCell tileRequirement Data
//            UpdateCell(currentCell, requiredTileConnections, neighbours);

//            startCellIndex += 1;
//        }


//        for (int i = 0; i < cellCount; i++)
//        {
//            if (Application.isPlaying == false)
//            {
//                return;
//            }

//            //spawn tile
//            GenerateCurrentCellTile_OLD(currentCell, requiredTileConnections, neighbours);


//            //if all cells have been spawned, end loop
//            if (i == cellCount - 1)
//            {
//                break;
//            }



//            if (startCellIndex != startCellCount)
//            {
//                currentCell = cells[startCellGridIndexs[startCellIndex]];

//                //update startCell tileRequirement Data
//                UpdateCell(currentCell, requiredTileConnections, neighbours);

//                startCellIndex += 1;
//            }
//            else
//            {
//                //select new tile
//                currentCell = SelectNewCell();
//            }



//            loopsLeftBeforeTaskDelay -= 1;

//            if (loopsLeftBeforeTaskDelay <= 0)
//            {
//                loopsLeftBeforeTaskDelay = cellUpdateCount;

//                await Task.Delay(cellUpdateTimeMs);
//            }
//        }

//        //cells.Dispose();
//        //tileStructs.Dispose();

//        //toRandomizeTilePool.Dispose();
//    }




//    private NativeArray<int> toRandomizeTilePool;

//    [BurstCompile]
//    private void GenerateCurrentCellTile(Cell currentCell, NativeArray<int> requiredTileConnections, NativeArray<Cell> neighbours)
//    {
//        currentCell = cells[currentCell.id];

//        int toRandomizeIndex = currentCell.tileOptionsCount;

//        float maxChanceValue = 0;
//        for (int i = 0; i < toRandomizeIndex; i++)
//        {
//            maxChanceValue += tileStructs[currentCell.tileOptions[i]].rarity;
//        }

//        float rChanceValue = Random.Range(0, maxChanceValue);
//        float tileRarity;


//        int r = -1;

//        for (int i = 0; i < toRandomizeIndex; i++)
//        {
//            tileRarity = tileStructs[currentCell.tileOptions[i]].rarity;

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

//        int finalTileType = currentCell.tileOptions[r];

//        //collapse current cell
//        CollapseCell(currentCell, finalTileType);


//        int[] DEBUG_connecotrs = new int[6];


//        GetNeighbourCells(currentCell.id, ref neighbours);
//        for (int i = 0; i < neighbours.Length; i++)
//        {
//            if (neighbours[i].collapsed)
//            {
//                DEBUG_connecotrs[i] = tileStructs[neighbours[i].tileType].connectors[OppositeSide(i)];
//            }

//            //skip non existent or already collapsed neighbours
//            if (neighbours[i].collapsed || neighbours[i].initialized == false)
//            {
//                continue;
//            }

//            UpdateCell(neighbours[i], requiredTileConnections, neighbours);
//        }


//        //get gridPos
//        int3 gridPos = LinearIndexToGridPos(currentCell.id);

//        //spawn tile
//        WaveTile spawnedObj = Instantiate(tilePrefabs[finalTileType], new Vector3(gridPos.x - gridSize.x * 0.5f + 0.5f, gridPos.y - gridSize.y * 0.5f + 0.5f, gridPos.z - gridSize.z * 0.5f + 0.5f), tilePrefabs[finalTileType].transform.rotation);

//        spawnedObj.DEBUG_connectors = DEBUG_connecotrs;

//        spawnedObj.DEBUG_tileOptions = new GameObject[currentCell.tileOptionsCount];

//        for (int i = 0; i < currentCell.tileOptionsCount; i++)
//        {
//            spawnedObj.DEBUG_tileOptions[i] = tilePrefabs[currentCell.tileOptions[i]].gameObject;
//        }


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
//    private void GenerateCurrentCellTile_OLD(Cell currentCell, NativeArray<int> requiredTileConnections, NativeArray<Cell> neighbours)
//    {
//        int tileStructCount = tileStructs.Length;

//        CalculateRequiredTileConnections_OLD(currentCell, neighbours, ref requiredTileConnections);


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
//            maxChanceValue += tileStructs[toRandomizeTilePool[i]].rarity;
//        }

//        float rChanceValue = Random.Range(0, maxChanceValue);
//        float tileRarity;


//        int r = -1;

//        for (int i = 0; i < toRandomizeIndex; i++)
//        {
//            tileRarity = tileStructs[toRandomizeTilePool[i]].rarity;
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

//        if (r == -1 || toRandomizeIndex == 0)
//        {
//            Debug.LogWarning("NO TILES FOUND, r = " + r + ", randomIndex = " + toRandomizeIndex);
//            r = 0;
//        }
//        int finalTileType = toRandomizeTilePool[r];

//        //collapse current cell
//        CollapseCell(currentCell, finalTileType);


//        int[] DEBUG_connecotrs = new int[6];

//        GetNeighbourCells(currentCell.id, ref neighbours);

//        for (int i = 0; i < neighbours.Length; i++)
//        {
//            if (neighbours[i].collapsed)
//            {
//                DEBUG_connecotrs[i] = tileStructs[neighbours[i].tileType].connectors[OppositeSide(i)];
//            }

//            //skip non existent or already collapsed neighbours
//            if (neighbours[i].collapsed || neighbours[i].initialized == false)
//            {
//                continue;
//            }

//            UpdateCell(neighbours[i], requiredTileConnections, neighbours);
//        }


//        //get gridPos
//        int3 gridPos = LinearIndexToGridPos(currentCell.id);


//        if (useGPUBasedRendering)
//        {
//            Vector3 pos = new Vector3(gridPos.x - gridSize.x * 0.5f + 0.5f, gridPos.y - gridSize.y * 0.5f + 0.5f, gridPos.z - gridSize.z * 0.5f + 0.5f);
//            Quaternion rot = tilePrefabs[finalTileType].transform.rotation;

//            AddMeshData(finalTileType, Matrix4x4.TRS(pos, rot, Vector3.one));
//        }
//        else
//        {
//            //spawn tile
//            WaveTile spawnedObj = Instantiate(tilePrefabs[finalTileType], new Vector3(gridPos.x - gridSize.x * 0.5f + 0.5f, gridPos.y - gridSize.y * 0.5f + 0.5f, gridPos.z - gridSize.z * 0.5f + 0.5f), tilePrefabs[finalTileType].transform.rotation);

//            if (randomColor)
//            {
//                Color color = Random.RandomColor();
//                foreach (Renderer ren in spawnedObj.GetComponentsInChildren<Renderer>(true))
//                {
//                    ren.material.color = color;
//                }
//            }

//            spawnedObj.DEBUG_connectors = DEBUG_connecotrs;

//            spawnedObj.DEBUG_tileOptions = new GameObject[currentCell.tileOptionsCount];

//            for (int i = 0; i < currentCell.tileOptionsCount; i++)
//            {
//                spawnedObj.DEBUG_tileOptions[i] = tilePrefabs[currentCell.tileOptions[i]].gameObject;
//            }
//        }
//    }


//    [BurstCompile]
//    private void CalculateRequiredTileConnections_OLD(Cell currentCell, NativeArray<Cell> neighbours, ref NativeArray<int> requiredTileConnections)
//    {
//        GetNeighbourCells(currentCell.id, ref neighbours);


//        for (int neigbourId = 0; neigbourId < 6; neigbourId++)
//        {
//            //skip unitialized neighbours and mark them as out of bounds (because they are outside of the grid
//            if (neighbours[neigbourId].initialized == false)
//            {
//                //2 is wall
//                requiredTileConnections[neigbourId] = 1;
//                continue;
//            }

//            //if neigbour is collapsed
//            if (neighbours[neigbourId].collapsed)
//            {
//                int tileType = neighbours[neigbourId].tileType;

//                TileStruct targetTile = tileStructs[tileType];

//                requiredTileConnections[neigbourId] = targetTile.connectors[OppositeSide(neigbourId)];
//            }
//            else
//            {
//                requiredTileConnections[neigbourId] = 0;
//            }
//        }
//    }



//    [BurstCompile]
//    private void UpdateCell(Cell targetCell, NativeArray<int> requiredTileConnections, NativeArray<Cell> neighbours)
//    {
//        //Destroy(Instantiate(CUBE, new Vector3(targetCell.DEBUG_gridPos.x - gridSize.x * 0.5f + 0.5f, targetCell.DEBUG_gridPos.y - gridSize.y * 0.5f + 0.5f, targetCell.DEBUG_gridPos.z - gridSize.z * 0.5f + 0.5f), Quaternion.identity), 0.75f);

//        GetRequiredTileConnections(targetCell, neighbours, ref requiredTileConnections);

//        int totalTiles = tileStructs.Length;

//        int toRandomizeIndex = 0;

//        for (int tileId = 0; tileId < totalTiles; tileId++)
//        {
//            bool isTileCompatible = true;

//            //check if all sides of the current to Check tile match with the neigbours
//            for (int connectorId = 0; connectorId < 6; connectorId++)
//            {
//                int toCheckTileConnection = tileStructs[tileId].connectors[OppositeSide(connectorId)];
//                int requiredConnection = requiredTileConnections[OppositeSide(connectorId)];


//                //0 means compatible with anything, if both connections are not 0 and dont match, this tile doesnt fit, so skip it
//                if (requiredConnection != 0 && toCheckTileConnection != 0 && requiredConnection != toCheckTileConnection)
//                {
//                    isTileCompatible = false;
//                    //Debug.Log($"Tile {tileId} is not compatible with required connection {connectorId}.");
//                    break;
//                }
//            }


//            //all directions of currentTile are valid, add tileId to toRandomizeTilePool
//            if (isTileCompatible)
//            {
//                //Debug.Log($"Tile {tileId} is compatible.");
//                targetCell.tileOptions[toRandomizeIndex++] = tileId;
//            }
//        }
//        targetCell.UpdateTileOptionsInspector(toRandomizeIndex);

//        targetCell.tileOptionsCount = toRandomizeIndex;

//        //update cell back
//        cells[targetCell.id] = targetCell;

//        DEBUG_cells = cells.ToArray();
//    }


//    [BurstCompile]
//    private void GetRequiredTileConnections(Cell currentCell, NativeArray<Cell> neighbours, ref NativeArray<int> requiredTileConnections)
//    {
//        GetNeighbourCells(currentCell.id, ref neighbours);

//        for (int neigbourId = 0; neigbourId < 6; neigbourId++)
//        {
//            //skip unitialized neighbours
//            if (neighbours[neigbourId].initialized == false)
//            {
//                continue;
//            }

//            //if neigbour is collapsed
//            if (neighbours[neigbourId].collapsed)
//            {
//                int tileType = neighbours[neigbourId].tileType;

//                TileStruct targetTile = tileStructs[tileType];

//                requiredTileConnections[neigbourId] = targetTile.connectors[neigbourId];
//            }
//        }
//    }


//    [BurstCompile]
//    private void CollapseCell(Cell currentCell, int finalTileType)
//    {
//        //remove cell from nonCollapsedList
//        for (int i = 0; i < nonColapsedCells.Count; i++)
//        {
//            if (nonColapsedCells[i].id == currentCell.id)
//            {
//                nonColapsedCells.RemoveAt(i);
//                break;
//            }
//        }

//        //collapse copy of cell
//        currentCell.collapsed = true;

//        //update copy of cell
//        currentCell.tileType = finalTileType;

//        //save copy back
//        cells[currentCell.id] = currentCell;

//        DEBUG_cells = cells.ToArray();
//    }


//    [BurstCompile]
//    private Cell SelectNewCell()
//    {
//        int cellCount = nonColapsedCells.Count;

//        //if there is just 1 cell left, select it and return the function
//        if (cellCount == 1)
//        {
//            return cells[nonColapsedCells[0].id];
//        }


//        //select new cell based on how many tile options neighbour cells have left
//        int leastOptions = int.MaxValue;
//        int cellsToRandomizeId = 0;

//        //loop over all cells
//        for (int i = 0; i < cellCount; i++)
//        {
//            Cell targetCell = cells[nonColapsedCells[i].id];

//            //if cell is colapsed, skip it
//            if (targetCell.collapsed)
//            {
//                continue;
//            }

//            //if cell specifically has less options
//            if (targetCell.tileOptionsCount < leastOptions)
//            {
//                leastOptions = targetCell.tileOptionsCount;

//                cellsToRandomizeId = 0;
//                toRandomizeTilePool[cellsToRandomizeId++] = i;
//            }

//            //else if cell had the same amount of options
//            else if (targetCell.tileOptionsCount == leastOptions)
//            {
//                toRandomizeTilePool[cellsToRandomizeId++] = i;
//            }
//        }


//        int r = Random.Range(0, cellsToRandomizeId);

//        return cells[nonColapsedCells[toRandomizeTilePool[r]].id];
//    }


//    [BurstCompile]
//    private void GetNeighbourCells(int cellId, ref NativeArray<Cell> neighbourCells)
//    {
//        // Convert cellId to gridPos
//        int3 gridPos = LinearIndexToGridPos(cellId);

//        //reset
//        for (int i = 0; i < 6; i++)
//        {
//            neighbourCells[i] = new Cell();
//        }


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




//    #region Extension Grid Methods

//    [BurstCompile]
//    private int OppositeSide(int side)
//    {
//        return side switch
//        {
//            0 => 1, // Left -> Right
//            1 => 0, // Right -> Left
//            2 => 3, // Up -> Down
//            3 => 2, // Down -> Up
//            4 => 5, // Front -> Back
//            5 => 4, // Back -> Front
//            _ => -1,
//        };
//    }

//    [BurstCompile]
//    private int3 LinearIndexToGridPos(int linearIndex)
//    {
//        int3 gridPos;
//        gridPos.z = linearIndex / (gridSize.x * gridSize.y);
//        gridPos.y = (linearIndex % (gridSize.x * gridSize.y)) / gridSize.x;
//        gridPos.x = linearIndex % gridSize.x;

//        return gridPos;
//    }

//    [BurstCompile]
//    private int GridPosToLinearIndex(int3 gridPos)
//    {
//        return
//        gridPos.x +
//        gridPos.y * gridSize.x +
//        gridPos.z * gridSize.x * gridSize.y;
//    }

//    [BurstCompile]
//    private void GridPosToLinearIndex(int3[] gridPositions, ref NativeArray<int> linearIndexs)
//    {
//        int positionAmount = gridPositions.Length;

//        for (int i = 0; i < positionAmount; i++)
//        {
//            int3 gridPos = gridPositions[i];

//            linearIndexs[i] =
//                gridPos.x +
//                gridPos.y * gridSize.x +
//                gridPos.z * gridSize.x * gridSize.y;
//        }
//    }

//    #endregion




//    private void OnDrawGizmos()
//    {
//        Gizmos.DrawWireCube(Vector3.zero, new Vector3(gridSize.x, gridSize.y, gridSize.z));
//    }
//}