using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using Graphs;


public class Snake : MonoBehaviour
{
    [SerializeField] private Transform _food;
    [SerializeField] private Transform _poison;
    [SerializeField] private Renderer _groundRenderer;
    [SerializeField] private float _moveSpeed = 1.5f;
    [SerializeField] private float _rotationSpeed = 180f;

    private Renderer _renderer;

    [HideInInspector] public int currentEpisode = 0;
    [HideInInspector] public float cumulativeReward = 0f;

    private Color _defaultGroundColor;
    private Coroutine _flashGroundCoroutine;

    [SerializeField] public GridManager gridManager;

    private Vector2Int _currentDirection = Vector2Int.up; // Default: up
    private Vector2Int _queuedDirection = Vector2Int.up;
    private Vector2Int _requestedDirection = Vector2Int.up;
    private bool directionChanged = false;

    public Cell currentCell;
    public Cell previousCell;
    public Cell pre_previousCell;

    [SerializeField] private float moveDelay = 0.2f; // Time between moves (seconds)
    private float moveTimer = 0f;

    public List<Tail> tails;
    public GameObject tailGameObject;

    public GameObject Environment;

    [SerializeField] private float maxTimeWithoutFood = 10f; // seconds
    private float timeSinceLastFood = 0f;

    private List<Vector2Int> lastPath;
    private bool isResetting = false;


    static readonly Vector2Int[] neighbors = {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
    };


    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        currentEpisode = 0;
        cumulativeReward = 0f;

        if (_groundRenderer != null)
        {
            _defaultGroundColor = _groundRenderer.material.color;
        }
    }

    private void Start()
    {
        BeginEpisode();
    }


    private void BeginEpisode()
    {
        foreach(Cell cell in gridManager.grid.data)
        {
            cell.isOccupied = false;
            cell.hasTail = false;
            cell.tail = null;
            cell.cost = 0;
        }
        foreach (Tail tail in tails)
        {
            if (tail != null)
                Destroy(tail.gameObject);
        }
        tails.Clear();

        if (_groundRenderer != null && cumulativeReward != 0f)
        {
            Color flashColor = cumulativeReward > 0 ? Color.green : Color.red;

            if (_flashGroundCoroutine != null)
            {
                StopCoroutine(_flashGroundCoroutine);
            }

            _flashGroundCoroutine = StartCoroutine(FlashGround(flashColor, 3.0f));
        }

        currentCell = gridManager.grid[new Vector2Int(gridManager.size.x / 2, gridManager.size.y / 2)];
        transform.localPosition = currentCell.position + new Vector3(0, 0.15f, 0);

        currentEpisode++;
        cumulativeReward = 0f;
        //_renderer.material.color = Color.green;

        SpawnObjects();

    }


    private IEnumerator FlashGround(Color color, float duration)
    {
        float elapsedTime = 0f;

        _groundRenderer.material.color = color;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            _groundRenderer.material.color = Color.Lerp(color, _defaultGroundColor, elapsedTime / duration);
            yield return null;
        }
    }

    private void SpawnObjects()
    {  
        // Find a valid, unoccupied cell for food
        Cell foodCell;
        do
        {
            foodCell = gridManager.GetRandomValidCell();
        } while (foodCell.isOccupied || foodCell.hasTail);

        // Find a valid, unoccupied cell for poison, and not the same as food
        Cell poisonCell;
        do
        {
            poisonCell = gridManager.GetRandomValidCell();
        } while (poisonCell.isOccupied || poisonCell == foodCell || poisonCell.hasTail);

        _food.localPosition = foodCell.position;
        _poison.localPosition = poisonCell.position;

        gridManager.grid[foodCell.gridPosition].isOccupied = true;
        gridManager.grid[poisonCell.gridPosition].isOccupied = true;

        PathfindHallways(); // Call pathfinding to visualize path
    }

    private void Update()
    {
        moveTimer += Time.deltaTime;
        timeSinceLastFood += Time.deltaTime; // Increment starvation timer

        // Starvation check
        if (timeSinceLastFood > maxTimeWithoutFood)
        {
            cumulativeReward -= 5f; // Penalize for starving
            timeSinceLastFood = 0f; // Reset timer
        }

        foreach(Vector2Int neighbor in neighbors)
        {
            if (gridManager.grid[currentCell.gridPosition + neighbor].state == Cell.CellState.Path)
            {
                _requestedDirection = neighbor;

                if (_requestedDirection + _currentDirection != Vector2Int.zero)
                {
                    _queuedDirection = _requestedDirection;
                }
                break;
            }
        }

        if (moveTimer < moveDelay)
        {
            return; // Skip movement until delay is met
        }
        _currentDirection = _queuedDirection;
        currentCell.nextDirection = _currentDirection;

        if (gridManager.grid.InBounds(currentCell.gridPosition + _currentDirection))
        {
            pre_previousCell = previousCell;
            previousCell = currentCell;
            currentCell = gridManager.grid[currentCell.gridPosition + _currentDirection];
            currentCell.isOccupied = true;

            if(tails.Count == 0)
            {
                if(previousCell!= null) previousCell.isOccupied = true;
                if(pre_previousCell!= null) pre_previousCell.isOccupied = false;
            }
            
        }

        transform.localPosition = currentCell.position + new Vector3(0, 0.15f, 0);

        foreach (Tail tail in tails)
        {
            tail.UpdateRotation(tail.currentCell.nextDirection);
            if (gridManager.grid.InBounds(tail.currentCell.gridPosition + tail.currentCell.nextDirection))
            {
                tail.previousCell = tail.currentCell;
                tail.currentCell = gridManager.grid[tail.currentCell.gridPosition + tail.currentCell.nextDirection];
                tail.currentCell.isOccupied = true;
                tail.previousCell.isOccupied = false;
                tail.currentCell.hasTail = true;
                tail.previousCell.hasTail = false;
            }

            tail.transform.localPosition = tail.currentCell.position + new Vector3(0, 0.15f, 0);

            if (tail.isLastTail)
            {
                tail.currentCell.state = Cell.CellState.LastTail;
                tail.previousCell.state = Cell.CellState.None;
            }

            tail.currentCell.tail = tail;

        }



        UpdateRotation(_currentDirection);
        moveTimer = 0;
        PathfindHallways(); // Call pathfinding to visualize path
        gridManager.UpdateCells();

    }

    public void MoveAgent(int action)
    {
        switch (action)
        {
            case 1: // Move forward
                _requestedDirection = Vector2Int.up;
                break;
            case 2: // Rotate left
                _requestedDirection = Vector2Int.left;
                break;
            case 3: // Rotate right
                _requestedDirection = Vector2Int.right;
                break;
            case 4: // Move backward
                _requestedDirection = Vector2Int.down;
                break;
        }

        // Prevent reversing direction
        if (_requestedDirection + _currentDirection != Vector2Int.zero)
        {
            _queuedDirection = _requestedDirection;
        }
    }

    private void UpdateRotation(Vector2Int direction)
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

        transform.localRotation = Quaternion.Euler(0f, angle, 0f);
    }

    public void UpdateCellDirection()
    {
        foreach (Cell cell in gridManager.grid.data)
        {
            if (!cell.isOccupied) continue;
            if (tails.Count == 0) cell.nextDirection = _currentDirection;
            else cell.nextDirection = tails.Last().currentCell.nextDirection;
        }
    }

    void PathfindHallways()
    {
        DungeonPathfinder2D aStar = new DungeonPathfinder2D(gridManager.size, gridManager.offset);

            var startPosf = currentCell.position;
            var endPosf = _food.position;
            var startPos = new Vector2Int((int)startPosf.x, (int)startPosf.z);
            var endPos = new Vector2Int((int)endPosf.x, (int)endPosf.z);
        
        foreach(Cell cell in gridManager.grid.data)
        {
            cell.state = Cell.CellState.None;
        }

        var path = aStar.FindPath(startPos, endPos, (DungeonPathfinder2D.Node a, DungeonPathfinder2D.Node b) =>
        {
            var pathCost = new DungeonPathfinder2D.PathCost();

            pathCost.cost = Vector2Int.Distance(b.Position, endPos);    //heuristic

            if (gridManager.grid[b.Position].isOccupied)
            {
                pathCost.cost += 100;
            }

            if (!gridManager.grid[b.Position].isValid)
            {
                pathCost.cost += 1000000;
            }
            if(gridManager.grid[b.Position].hasTail)
            {
                int index = tails.IndexOf(gridManager.grid[b.Position].tail) + 1;
                pathCost.cost += (tails.Count * 50) / index;
            }

            if (gridManager.grid[b.Position].state == Cell.CellState.Wall)
            {
                pathCost.cost += 10;
            }
            else if (gridManager.grid[b.Position].state == Cell.CellState.Obstacle)
            {
                pathCost.cost += 5;
            }
            else if (gridManager.grid[b.Position].state == Cell.CellState.None)
            {
                pathCost.cost += 1;
            }

            pathCost.traversable = true;

            gridManager.grid[b.Position].cost = (int) pathCost.cost;

            return pathCost;
        });

        lastPath = path; // Store the path for Gizmos

        if (path != null)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    var current = path[i];

                    if (gridManager.grid[current].state == Cell.CellState.None)
                    {
                        gridManager.grid[current].state = Cell.CellState.Path;
                    }

                    if (i > 0)
                    {
                        var prev = path[i - 1];

                        var delta = current - prev;
                    }
                }
            }
        
    }

    private void OnDrawGizmos()
    {
        if (lastPath == null || gridManager == null)
            return;

        Gizmos.color = Color.black;
        for (int i = 1; i < lastPath.Count; i++)
        {
            Vector3 from = gridManager.grid[lastPath[i - 1]].position + new Vector3(0, 0.2f, 0);
            Vector3 to = gridManager.grid[lastPath[i]].position + new Vector3(0, 0.2f, 0);
            Gizmos.DrawLine(from, to);
            Gizmos.DrawSphere(from, 0.1f);
        }
        // Draw last point
        if (lastPath.Count > 0)
        {
            Vector3 last = gridManager.grid[lastPath[lastPath.Count - 1]].position + new Vector3(0, 0.2f, 0);
            Gizmos.DrawSphere(last, 0.1f);
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Food"))
        { 
            Eat();
            gridManager.grid[new Vector2Int((int)_food.position.x, (int)_food.position.z)].isOccupied = false;
            gridManager.grid[new Vector2Int((int)_poison.position.x, (int)_poison.position.z)].isOccupied = false;
            SpawnObjects();
            timeSinceLastFood = 0f; // Reset starvation timer
        }
        else if (other.CompareTag("Poison"))
        {
            gridManager.grid[new Vector2Int((int)_food.position.x, (int)_food.position.z)].isOccupied = false;
            gridManager.grid[new Vector2Int((int)_poison.position.x, (int)_poison.position.z)].isOccupied = false;
            cumulativeReward -= 5f;
            SpawnObjects();
        }
        if (other.CompareTag("wall"))
        {
            cumulativeReward -= 10f;
            Debug.Log("wall");
            BeginEpisode();
        }
        else if (other.CompareTag("Tail"))
        {
            cumulativeReward -= 1f;
            BeginEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("wall"))
        {
            cumulativeReward -= 10f;
            Debug.Log("wall");
            BeginEpisode();
        }
        else if (collision.gameObject.CompareTag("Tail"))
        {
            cumulativeReward -= 1f;
            BeginEpisode();
        }
    }

    public void Eat()
    {
        
        cumulativeReward += 5.0f;
        AddTail();
    }

    public void AddTail()
    {
        if (tails.Count == 0)
        {
            Cell tailCell = previousCell;
            tailCell.isOccupied = true;
            tailCell.nextDirection = previousCell.nextDirection;
            GameObject tailObj = Instantiate(tailGameObject, tailCell.position + new Vector3(0, 0.15f, 0), Quaternion.identity);
            tailObj.transform.localRotation = transform.localRotation;
            Tail tail = tailObj.GetComponent<Tail>();
            tails.Add(tail);
            tail.InitializeTail(tailCell, this);
        }
        else
        {
            tails.Last().isLastTail = false;
            tails.Last().AddTail();
        }
    }


    // Keyboard input for manual control
    private void FixedUpdate()
    {
        int action = 0;
        if (Keyboard.current.wKey.isPressed)
            action = 1; // Move forward
        else if (Keyboard.current.aKey.isPressed)
            action = 2; // Rotate left
        else if (Keyboard.current.dKey.isPressed)
            action = 3; // Rotate right
        else if (Keyboard.current.sKey.isPressed)
            action = 4; // Move backward

        //MoveAgent(action);
    }
}
