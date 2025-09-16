using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.InputSystem;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using Unity.MLAgents.Policies;

public class SnakeAgent : Agent
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

    [SerializeField] private float moveDelay = 0.2f; // Time between moves (seconds)
    private float moveTimer = 0f;

    public List<Tail> tails;
    public GameObject tailGameObject;

    public GameObject Environment;

    public BehaviorParameters behaviorParameters;

    [SerializeField] private float maxTimeWithoutFood = 10f; // seconds
    private float timeSinceLastFood = 0f;


    public override void Initialize()
    {
        base.Initialize();

        _renderer = GetComponent<Renderer>();
        behaviorParameters = GetComponent<BehaviorParameters>();
        currentEpisode = 0;
        cumulativeReward = 0f;

        if (_groundRenderer != null)
        {
            _defaultGroundColor = _groundRenderer.material.color;
        }
    }

    public override void OnEpisodeBegin()
    {
        foreach (Tail tail in tails)
        {
            Destroy(tail.gameObject);
            //behaviorParameters.BrainParameters.VectorObservationSize -= 2;
        }
        tails.Clear();

        if (_groundRenderer != null && cumulativeReward != 0f)
        {
            Color flashColor = cumulativeReward > 0 ? Color.green : Color.red;

            // Stop any existing FlashGround coroutine before starting a new one
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
        _renderer.material.color = Color.green;

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
        int foodIndex = 0;
        int poisonIndex = 0;

        foodIndex = gridManager.grid.GetIndex(gridManager.GetRandomValidCell().gridPosition);
        do
        {
            poisonIndex = gridManager.grid.GetIndex(gridManager.GetRandomValidCell().gridPosition);
        } while (poisonIndex == foodIndex);


        _food.localPosition = gridManager.grid.data[foodIndex].position;
        _poison.localPosition = gridManager.grid.data[poisonIndex].position;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        float gridCenterX = gridManager.size.x * gridManager.cellSize / 2f;
        float gridCenterZ = gridManager.size.y * gridManager.cellSize / 2f;
        float gridHalfX = gridManager.size.x * gridManager.cellSize / 2f;
        float gridHalfZ = gridManager.size.y * gridManager.cellSize / 2f;

        float foodPoxX_normalized = _food.localPosition.x / gridManager.size.x;
        float foodPoxZ_normalized = _food.localPosition.z / gridManager.size.y;

        float poisonPoxX_normalized = _poison.localPosition.x / gridManager.size.x;
        float poisonPoxZ_normalized = _poison.localPosition.z / gridManager.size.y;

        float snakePosX_normalized = transform.localPosition.x / gridManager.size.x;
        float snakePosZ_normalized = transform.localPosition.z / gridManager.size.y;

        float snakeRotationY_normalized = (transform.localEulerAngles.y / 360f) * 2f - 1f;

        sensor.AddObservation(foodPoxX_normalized);
        sensor.AddObservation(foodPoxZ_normalized);
        sensor.AddObservation(poisonPoxX_normalized);
        sensor.AddObservation(poisonPoxZ_normalized);
        sensor.AddObservation(snakePosX_normalized);
        sensor.AddObservation(snakePosZ_normalized);
        sensor.AddObservation(snakeRotationY_normalized);

        // Direction vector to food (normalized)
        Vector3 toFood = _food.localPosition - transform.localPosition;
        sensor.AddObservation(toFood.normalized);

        // Current moving direction
        sensor.AddObservation(_currentDirection);

        float distanceToLeftWall = transform.localPosition.x / gridManager.size.x;
        float distanceToRightWall = (gridManager.size.x - transform.localPosition.x) / gridManager.size.x;
        float distanceToBottomWall = transform.localPosition.z / gridManager.size.y;
        float distanceToTopWall = (gridManager.size.y - transform.localPosition.z) / gridManager.size.y;

        sensor.AddObservation(distanceToLeftWall);    // distance to left wall
        sensor.AddObservation(distanceToRightWall);   // distance to right wall
        sensor.AddObservation(distanceToBottomWall);  // distance to bottom wall
        sensor.AddObservation(distanceToTopWall);     // distance to top wall

        /*foreach(Tail tail in tails)
        {
            if(tail.isObserved) continue;

            float tailPosX_normalized = (tail.transform.localPosition.x - gridCenterX) / gridHalfX;
            float tailPosZ_normalized = (tail.transform.localPosition.z - gridCenterZ) / gridHalfZ;
            sensor.AddObservation(tailPosX_normalized);
            sensor.AddObservation(tailPosZ_normalized);
            tail.isObserved = true;
            behaviorParameters.BrainParameters.VectorObservationSize += 2;
        }*/
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        MoveAgent(actions.DiscreteActions);

        AddReward(5f / MaxStep);

        cumulativeReward = GetCumulativeReward();
    }

    private void Update()
    {
        moveTimer += Time.deltaTime;
        timeSinceLastFood += Time.deltaTime; // Increment starvation timer

        // Starvation check
        if (timeSinceLastFood > maxTimeWithoutFood)
        {
            AddReward(-5f); // Penalize for starving
            timeSinceLastFood = 0f; // Reset timer
        }

        if (moveTimer < moveDelay)
        {
            return; // Skip movement until delay is met
        }
        _currentDirection = _queuedDirection;
        currentCell.nextDirection = _currentDirection;


        if (gridManager.grid.InBounds(currentCell.gridPosition + _currentDirection))
        {
            previousCell = currentCell;
            currentCell = gridManager.grid[currentCell.gridPosition + _currentDirection];
            currentCell.isOccupied = true;
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
            }
                

            tail.transform.localPosition = tail.currentCell.position + new Vector3(0, 0.15f, 0);
        }

        UpdateRotation(_currentDirection);
        moveTimer = 0;
    }

    public void MoveAgent(ActionSegment<int> actions)
    {
        var action = actions[0];


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

        transform.localRotation = Quaternion.Euler(90f, angle, 0f);
    }

    public void UpdateCellDirection()
    {
        foreach (Cell cell in gridManager.grid.data)
        {
            if(!cell.isOccupied) continue;
            if (tails.Count == 0) cell.nextDirection = _currentDirection;
            else cell.nextDirection = tails.Last().currentCell.nextDirection;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Food"))
        {
            Eat();
            SpawnObjects();
            timeSinceLastFood = 0f; // Reset starvation timer

        }
        else if (other.CompareTag("Poison"))
        {
            AddReward(-5f);
            SpawnObjects();
        }
        
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("wall"))
        {
            AddReward(-10f);
            Debug.Log("wall");
            EndEpisode();
        }
        else if (collision.gameObject.CompareTag("Tail"))
        {
            AddReward(-1f);
            EndEpisode();
        }
    }

    public void Eat()
    {
        AddReward(5.0f);
        cumulativeReward = GetCumulativeReward();
        AddTail();
    }

    public void AddTail()
    {
        if(tails.Count == 0)
        {
            Cell tailCell = previousCell;
            tailCell.nextDirection = previousCell.nextDirection;
            GameObject tailObj = Instantiate(tailGameObject, tailCell.position + new Vector3(0, 0.15f, 0), Quaternion.identity);
            tailObj.transform.localRotation = transform.localRotation;
            tailObj.transform.parent = Environment.transform;
            Tail tail = tailObj.GetComponent<Tail>();
            tails.Add(tail);
            tail.InitializeTail(tailCell,this);
        }
        else
        {
            tails.Last().AddTail();
        }

        
    }

    /*public void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("wall"))
        {
            AddReward(-0.01f * Time.fixedDeltaTime);
        }
    }*/

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("wall"))
        {
            //AddReward(0.01f);
            _renderer.material.color = Color.green;

        }
    }


    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        discreteActionsOut[0] = 0; // Default: no action



        if (Keyboard.current.wKey.isPressed)
            discreteActionsOut[0] = 1; // Move forward
        else if (Keyboard.current.aKey.isPressed)
            discreteActionsOut[0] = 2; // Rotate left
        else if (Keyboard.current.dKey.isPressed)
            discreteActionsOut[0] = 3; // Rotate right
        else if (Keyboard.current.sKey.isPressed)
            discreteActionsOut[0] = 4; // Move Backward
    }



}
