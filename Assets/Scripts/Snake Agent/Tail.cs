using UnityEngine;

public class Tail : MonoBehaviour
{
    public Cell currentCell;
    public Cell previousCell;
    public SnakeAgent snake;
    public GridManager gridManager;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    [SerializeField]
    private bool debugCellInfo = false;
    public void InitializeTail(Cell cell, SnakeAgent snake)
    {
        currentCell = cell;
        this.snake = snake;
        gridManager = snake.gridManager;
    }


    public void AddTail()
    {
        Cell tailCell = previousCell;
        tailCell.nextDirection = previousCell.nextDirection;
        GameObject tailObj = Instantiate(snake.tailGameObject, tailCell.position + new Vector3(0, 0.15f, 0), Quaternion.identity);
        tailObj.transform.localRotation = transform.localRotation;
        Tail tail = tailObj.GetComponent<Tail>();
        snake.tails.Add(tail);
        tail.InitializeTail(tailCell,snake);
    }

    public void UpdateRotation(Vector2Int direction)
    {
        float angle = 0f;
        if (direction == Vector2Int.up)
            angle = 0f;
        else if (direction == Vector2Int.right)
            angle = 90f;
        else if (direction == Vector2Int.down)
            angle = 180f;
        else if (direction == Vector2Int.left)
            angle = 270f;

        transform.localRotation = Quaternion.Euler(90f, angle, 0f);
    }

    private void OnValidate()
    {
        if (debugCellInfo)
        {
            Debug.Log($"[Tail Debug] GameObject: {gameObject.name}");

            if (currentCell != null)
            {
                Debug.Log($"Current Cell: pos={currentCell.position}, gridPos={currentCell.gridPosition}, valid={currentCell.isValid}, nextDir={currentCell.nextDirection}");
            }
            else
            {
                Debug.Log("Current Cell: null");
            }

            if (previousCell != null)
            {
                Debug.Log($"Previous Cell: pos={previousCell.position}, gridPos={previousCell.gridPosition}, valid={previousCell.isValid}, nextDir={previousCell.nextDirection}");
            }
            else
            {
                Debug.Log("Previous Cell: null");
            }

            // Reset the bool so it only prints once per click
            debugCellInfo = false;
        }
    }

}
