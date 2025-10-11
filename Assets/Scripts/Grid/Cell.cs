using UnityEngine;

public class Cell
{
    public enum CellState
    {
        None,
        Wall,
        Obstacle,
        Path,
        LastTail,
        Poison
    }
    public Vector3 position;
    public Vector2Int gridPosition;

    public bool isValid = true;

    public Vector2Int[] neighbors;

    public GridManager gridManager;

    public Vector2Int nextDirection;

    public bool isOccupied = false;

    public CellState state = CellState.None;

    public GameObject cubeObject;

    public Cell(Vector3 position)
    {
        this.position = position;

    }

    public void instantiatePosition(GridManager gridManager)
    {
        cubeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeObject.transform.position = position;
        cubeObject.transform.localScale = new Vector3(0.9f, 0.1f, 0.9f);
        cubeObject.GetComponent<Renderer>().material.color = Color.cyan;

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

    public void CellUpdate()
    {
        if (isOccupied)
        {
            //cubeObject.GetComponent<Renderer>().material.color = Color.red;
        }
        else
        {
            cubeObject.GetComponent<Renderer>().material.color = Color.green;
        }
        if (!isValid)
        {
            cubeObject.GetComponent<Renderer>().material.color = Color.black;
        }

        if(state == CellState.Path)
        {
            cubeObject.GetComponent<Renderer>().material.color = Color.pink;
        }

        if(state == CellState.LastTail)
        {
            cubeObject.GetComponent<Renderer>().material.color = Color.blue;
        }

        
    }

}


