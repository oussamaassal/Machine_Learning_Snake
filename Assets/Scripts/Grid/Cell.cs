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

    public int cost;

    public bool hasTail;

    public Tail tail;

    // Add a field for the TextMesh
    public TextMesh costTextMesh;

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

        // Create and position the TextMesh above the cube
        GameObject textObj = new GameObject("CostText");
        textObj.transform.SetParent(cubeObject.transform);
        textObj.transform.localPosition = new Vector3(0, 0.3f, 0); // Slightly above the cube

        costTextMesh = textObj.AddComponent<TextMesh>();
        costTextMesh.text = cost.ToString("0.##");
        costTextMesh.characterSize = 0.2f;
        costTextMesh.fontSize = 20;
        costTextMesh.anchor = TextAnchor.MiddleCenter;
        costTextMesh.alignment = TextAlignment.Center;
        costTextMesh.color = Color.black;
        textObj.transform.localScale += new Vector3(0, -9f, 0); // Scale down the text

    }

    public void CellUpdate()
    {
        if (isOccupied)
        {
            cubeObject.GetComponent<Renderer>().material.color = Color.red;
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
            cubeObject.GetComponent<Renderer>().material.color = Color.yellow;
        }

        if(state == CellState.LastTail)
        {
            cubeObject.GetComponent<Renderer>().material.color = Color.blue;
        }

        // Update the cost text
        if (costTextMesh != null)
        {
            costTextMesh.text = cost.ToString("0.##");

            costTextMesh.transform.localRotation = Quaternion.Euler(90, 0, 0);

        }
    }

}


