using UnityEngine;

public class Cell
{
    public Vector3 position;
    public Vector2Int gridPosition;

    public bool isValid = true;

    public Vector2Int[] neighbors;

    public GridManager gridManager;

    public Vector2Int nextDirection;

    public Cell(Vector3 position)
    {
        this.position = position;

    }

    public void instantiatePosition(GridManager gridManager)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position = position;
        cube.transform.localScale = new Vector3(0.9f, 0.1f, 0.9f);
        cube.GetComponent<Renderer>().material.color = Color.cyan;

        this.gridManager = gridManager;
        // Initialize neighbors here since cellSize is non-static and cannot be used in field initializers
        neighbors = new Vector2Int[]
        {
            new Vector2Int(gridManager.cellSize, 0),
            new Vector2Int(-gridManager.cellSize, 0),
            new Vector2Int(0, gridManager.cellSize),
            new Vector2Int(0, -gridManager.cellSize),
        };
    }

}


