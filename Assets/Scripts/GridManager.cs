using Unity.VisualScripting;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [SerializeField] public Vector2Int size;
    [SerializeField] Vector2Int offset;

    public int cellSize = 1;

    public Grid2D<Cell> grid;


    private void Awake()
    {
        // Get the plane's size from its renderer
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            float planeWidth = renderer.bounds.size.x;
            float planeDepth = renderer.bounds.size.z;

            size = new Vector2Int(
                Mathf.RoundToInt(planeWidth / cellSize),
                Mathf.RoundToInt(planeDepth / cellSize)
            );
        }

        grid = new Grid2D<Cell>(size, Vector2Int.zero);


        for (int x = 0; x < grid.Size.x; x++)
        {
            for (int y = 0; y < grid.Size.y; y++)
            {
                grid[x, y] = new Cell(new Vector3(x + offset.x, 0f, y + offset.y));
                grid[x, y].gridPosition = new Vector2Int(x,y);
                if (x == 0 || y == 0 || x == grid.Size.x - 1 || y == grid.Size.y - 1)
                {
                    grid[x, y].isValid = false; // Mark border cells as invalid
                }
            }
        }

        //InitializeGrid();

    }

    private void InitializeGrid()
    {
        foreach(Cell cell in grid.data)
        {
            cell.instantiatePosition(this);
        }
    }


    // Returns a random cell position (useful for spawning food)
    public Cell GetRandomCell()
    {
        int x = Random.Range(0, size.x);
        int y = Random.Range(0, size.y);
        return grid[new Vector2Int(x,y)];
    }

    public Cell GetRandomValidCell()
    {
        int x = Random.Range(1, size.x - 1);
        int y = Random.Range(1, size.y - 1);
        return grid[new Vector2Int(x, y)];
    }


    // Checks if a cell is within grid bounds
    public bool IsCellValid(int x, int y)
    {
        return x >= 0 && x < size.x && y >= 0 && y < size.y;
    }

    /*private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        // Only draw if grid is initialized
        if (grid == null)
            return;

        for (int x = 0; x < grid.Size.x; x++)
        {
            for (int y = 0; y < grid.Size.y; y++)
            {
                Vector2Int cellPos = new Vector2Int(x, y);
                Vector3 worldPos = transform.position + new Vector3(grid.Offset.x + x * cellSize, 0f, grid.Offset.y + y * cellSize);

                // Draw cell borders (square)
                Vector3 topLeft = worldPos;
                Vector3 topRight = worldPos + new Vector3(cellSize, 0f, 0f);
                Vector3 bottomLeft = worldPos + new Vector3(0f, 0f, cellSize);
                Vector3 bottomRight = worldPos + new Vector3(cellSize, 0f, cellSize);

                Gizmos.DrawLine(topLeft, topRight);
                Gizmos.DrawLine(topRight, bottomRight);
                Gizmos.DrawLine(bottomRight, bottomLeft);
                Gizmos.DrawLine(bottomLeft, topLeft);
            }
        }
    }
    */

    // Optionally, you can add methods for marking cells as occupied, etc.
}
