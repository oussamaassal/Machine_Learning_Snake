using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class SnakeAgent : Agent
{
    [Header("References")]
    [SerializeField] private Transform _food;
    [SerializeField] private Transform _poison;
    [SerializeField] private Renderer _groundRenderer;
    [SerializeField] public GridManager gridManager;
    [SerializeField] public GameObject tailGameObject;
    [SerializeField] public GameObject Environment;

    [Header("Movement Settings")]
    [SerializeField] private float moveDelay = 0.2f;
    [SerializeField] private float maxTimeWithoutFood = 10f;

    private Renderer _renderer;
    private Color _defaultGroundColor;
    private Coroutine _flashGroundCoroutine;

    private Vector2Int _currentDirection = Vector2Int.up;
    private Vector2Int _queuedDirection = Vector2Int.up;
    private Vector2Int _requestedDirection = Vector2Int.up;

    private float moveTimer = 0f;
    private float timeSinceLastFood = 0f;

    [HideInInspector] public int currentEpisode = 0;
    [HideInInspector] public float cumulativeReward = 0f;

    public Cell currentCell;
    public Cell previousCell;
    public List<Tail> tails;

    private Vector3 previousFoodVector;

    public override void Initialize()
    {
        _renderer = GetComponent<Renderer>();
        _defaultGroundColor = _groundRenderer != null ? _groundRenderer.material.color : Color.white;
        tails = new List<Tail>();
    }

    public override void OnEpisodeBegin()
    {
        foreach (Tail tail in tails) Destroy(tail.gameObject);
        tails.Clear();

        if (_groundRenderer != null && cumulativeReward != 0f)
        {
            Color flashColor = cumulativeReward > 0 ? Color.green : Color.red;
            if (_flashGroundCoroutine != null) StopCoroutine(_flashGroundCoroutine);
            _flashGroundCoroutine = StartCoroutine(FlashGround(flashColor, 1.5f));
        }

        // Clear all occupied cells first
        for (int i = 0; i < gridManager.grid.data.Length; i++)
        {
            gridManager.grid.data[i].isOccupied = false;
        }

        currentCell = gridManager.grid[new Vector2Int(gridManager.size.x / 2, gridManager.size.y / 2)];
        currentCell.isOccupied = true; // Mark head position as occupied
        transform.localPosition = currentCell.position + new Vector3(0, 0.15f, 0);
        _currentDirection = Vector2Int.up;
        _queuedDirection = Vector2Int.up;

        currentEpisode++;
        cumulativeReward = 0f;
        _renderer.material.color = Color.green;

        SpawnObjects();
        timeSinceLastFood = 0f;
        moveTimer = 0f;

        previousFoodVector = _food.localPosition - transform.localPosition;
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
        } while (foodCell.isOccupied);

        // Find a valid, unoccupied cell for poison, and not the same as food
        Cell poisonCell;
        do
        {
            poisonCell = gridManager.GetRandomValidCell();
        } while (poisonCell.isOccupied || poisonCell == foodCell);

        _food.localPosition = foodCell.position;
        _poison.localPosition = poisonCell.position;
    }


    public override void CollectObservations(VectorSensor sensor)
    {
        // Positions normalized
        sensor.AddObservation(_food.localPosition.x / gridManager.size.x);
        sensor.AddObservation(_food.localPosition.z / gridManager.size.y);
        sensor.AddObservation(_poison.localPosition.x / gridManager.size.x);
        sensor.AddObservation(_poison.localPosition.z / gridManager.size.y);
        sensor.AddObservation(transform.localPosition.x / gridManager.size.x);
        sensor.AddObservation(transform.localPosition.z / gridManager.size.y);

        // Direction & vector to food
        sensor.AddObservation(_currentDirection);
        sensor.AddObservation((_food.localPosition - transform.localPosition).normalized);

        // Distance to walls
        sensor.AddObservation(transform.localPosition.x / gridManager.size.x); // Left
        sensor.AddObservation((gridManager.size.x - transform.localPosition.x) / gridManager.size.x); // Right
        sensor.AddObservation(transform.localPosition.z / gridManager.size.y); // Bottom
        sensor.AddObservation((gridManager.size.y - transform.localPosition.z) / gridManager.size.y); // Top

        // Enhanced danger detection with multiple steps ahead
        sensor.AddObservation(CheckImmediateDanger(_currentDirection, 1)); // Forward 1 step
        sensor.AddObservation(CheckImmediateDanger(TurnLeft(_currentDirection), 1)); // Left 1 step
        sensor.AddObservation(CheckImmediateDanger(TurnRight(_currentDirection), 1)); // Right 1 step

        // Look ahead 2 steps for better planning
        sensor.AddObservation(CheckImmediateDanger(_currentDirection, 2)); // Forward 2 steps
        sensor.AddObservation(CheckImmediateDanger(TurnLeft(_currentDirection), 2)); // Left 2 steps
        sensor.AddObservation(CheckImmediateDanger(TurnRight(_currentDirection), 2)); // Right 2 steps

        // Add tail length observation
        sensor.AddObservation(tails.Count / 10.0f); // Normalized tail length

        // Add information about tail positions relative to head
        AddTailObservations(sensor);
    }

    private void AddTailObservations(VectorSensor sensor)
    {
        // Observe positions of closest tail segments (up to 4)
        for (int i = 0; i < 4; i++)
        {
            if (i < tails.Count)
            {
                Vector3 relativePos = tails[i].transform.localPosition - transform.localPosition;
                sensor.AddObservation(relativePos.x / gridManager.size.x);
                sensor.AddObservation(relativePos.z / gridManager.size.y);
            }
            else
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }
        }
    }

    private float CheckImmediateDanger(Vector2Int direction, int stepsAhead = 1)
    {
        Vector2Int pos = currentCell.gridPosition;

        // Check multiple steps ahead
        for (int step = 1; step <= stepsAhead; step++)
        {
            pos += direction;

            if (!gridManager.grid.InBounds(pos)) return 1f; // Wall

            // Check if any tail segment will be at this position
            if (WillTailBeAtPosition(pos, step)) return 1f;

            // Check poison
            if (_poison != null && Vector2Int.RoundToInt(new Vector2(_poison.localPosition.x, _poison.localPosition.z)) == pos)
                return step == 1 ? 1f : 0.5f; // Less dangerous if further away
        }

        return 0f; // Safe
    }

    private bool WillTailBeAtPosition(Vector2Int position, int stepsAhead)
    {
        // Check current tail positions
        foreach (Tail tail in tails)
        {
            if (tail.currentCell.gridPosition == position) return true;

            // Predict where tail will be in 'stepsAhead' moves
            // This is simplified - you might need more complex prediction based on your tail movement logic
        }

        return false;
    }

    private Vector2Int TurnLeft(Vector2Int dir)
    {
        if (dir == Vector2Int.up) return Vector2Int.left;
        if (dir == Vector2Int.left) return Vector2Int.down;
        if (dir == Vector2Int.down) return Vector2Int.right;
        return Vector2Int.up;
    }

    private Vector2Int TurnRight(Vector2Int dir)
    {
        if (dir == Vector2Int.up) return Vector2Int.right;
        if (dir == Vector2Int.right) return Vector2Int.down;
        if (dir == Vector2Int.down) return Vector2Int.left;
        return Vector2Int.up;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        MoveAgent(actions.DiscreteActions);

        // Reduced survival reward to prevent just wandering
        AddReward(0.005f);

        // Reward for moving closer to food
        Vector3 toFood = _food.localPosition - transform.localPosition;
        float distanceDiff = previousFoodVector.magnitude - toFood.magnitude;
        AddReward(distanceDiff * 0.3f); // Reduced from 0.5f to prevent overfitting

        // Penalty for being too close to own tail
        float tailProximityPenalty = CalculateTailProximityPenalty();
        AddReward(tailProximityPenalty);

        cumulativeReward = GetCumulativeReward();
    }

    private float CalculateTailProximityPenalty()
    {
        float penalty = 0f;
        Vector2Int headPos = currentCell.gridPosition;

        foreach (Tail tail in tails)
        {
            Vector2Int tailPos = tail.currentCell.gridPosition;
            float distance = Vector2Int.Distance(headPos, tailPos);

            // Penalize being too close to tail segments
            if (distance <= 2f)
            {
                penalty -= 0.01f * (3f - distance); // Stronger penalty for closer proximity
            }
        }

        return penalty;
    }

    private void Update()
    {
        moveTimer += Time.deltaTime;
        timeSinceLastFood += Time.deltaTime;

        if (timeSinceLastFood > maxTimeWithoutFood)
        {
            AddReward(-50f); // Increased starvation penalty
            timeSinceLastFood = 0f;
        }

        if (moveTimer < moveDelay) return;

        _currentDirection = _queuedDirection;
        currentCell.nextDirection = _currentDirection;

        Vector2Int nextPos = currentCell.gridPosition + _currentDirection;
        if (gridManager.grid.InBounds(nextPos))
        {
            // Clear previous head position
            if (previousCell != null)
            {
                previousCell.isOccupied = false;
            }

            previousCell = currentCell;
            currentCell = gridManager.grid[nextPos];
            currentCell.isOccupied = true;
        }

        transform.localPosition = currentCell.position + new Vector3(0, 0.5f, 0);

        // Update tails with proper occupancy tracking
        UpdateTails();

        UpdateRotation(_currentDirection);
        moveTimer = 0;
    }

    private void UpdateTails()
    {
        for (int i = 0; i < tails.Count; i++)
        {
            Tail tail = tails[i];

            // Clear previous tail position
            if (tail.previousCell != null)
            {
                tail.previousCell.isOccupied = false;
            }

            tail.UpdateRotation(tail.currentCell.nextDirection);
            Vector2Int tailNext = tail.currentCell.gridPosition + tail.currentCell.nextDirection;

            if (gridManager.grid.InBounds(tailNext))
            {
                tail.previousCell = tail.currentCell;
                tail.currentCell = gridManager.grid[tailNext];
                tail.currentCell.isOccupied = true;
            }

            tail.transform.localPosition = tail.currentCell.position + new Vector3(0, 0.5f, 0);
        }
    }

    public void MoveAgent(ActionSegment<int> actions)
    {
        var action = actions[0];
        Vector2Int oldDirection = _currentDirection;

        switch (action)
        {
            case 0: _requestedDirection = _currentDirection; break; // Forward
            case 1: _requestedDirection = TurnLeft(_currentDirection); break; // Left
            case 2: _requestedDirection = TurnRight(_currentDirection); break; // Right
        }

        if (_requestedDirection + _currentDirection != Vector2Int.zero)
        {
            Vector2Int nextPos = currentCell.gridPosition + _requestedDirection;

            if (gridManager.grid.InBounds(nextPos) && !gridManager.grid[nextPos].isOccupied)
            {
                // Reward moving toward food, penalize moving away
                Vector3 currentFoodVector = _food.localPosition - transform.localPosition;
                Vector3 nextFoodVector = _food.localPosition - gridManager.grid[nextPos].position;

                if (nextFoodVector.magnitude < currentFoodVector.magnitude)
                {
                    AddReward(2f); // Reward for moving closer
                }
                else
                {
                    AddReward(-1f); // Penalty for moving away
                }

                _queuedDirection = _requestedDirection;
            }
            else
            {
                AddReward(-1f);
            }
        }
        else
        {
            AddReward(-1f);
        }
    }

    private void UpdateRotation(Vector2Int direction)
    {
        float angle = 0f;
        if (direction == Vector2Int.up) angle = 0f;
        else if (direction == Vector2Int.right) angle = 90f;
        else if (direction == Vector2Int.down) angle = 180f;
        else if (direction == Vector2Int.left) angle = 270f;

        transform.localRotation = Quaternion.Euler(90f, angle, 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Food"))
        {
            Eat();
            SpawnObjects();
            timeSinceLastFood = 0f;
        }
        else if (other.CompareTag("Poison"))
        {
            AddReward(-50f); // Increased poison penalty
            SpawnObjects();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("wall") || collision.gameObject.CompareTag("Tail"))
        {
            AddReward(-200f); // Increased collision penalty
            EndEpisode();
        }
    }

    public void Eat()
    {
        AddReward(100.0f);

        // Bonus reward for longer snake
        float lengthBonus = tails.Count * 0.5f;
        AddReward(lengthBonus);

        cumulativeReward = GetCumulativeReward();
        AddTail();
    }

    public void AddTail()
    {
        if (tails.Count == 0)
        {
            Cell tailCell = previousCell;
            tailCell.nextDirection = previousCell.nextDirection;
            tailCell.isOccupied = true; // Mark as occupied

            GameObject tailObj = Instantiate(tailGameObject, tailCell.position + new Vector3(0, 0.15f, 0), Quaternion.identity);
            tailObj.transform.localRotation = transform.localRotation;
            tailObj.transform.parent = Environment.transform;
            Tail tail = tailObj.GetComponent<Tail>();
            tails.Add(tail);
            tail.InitializeTail(tailCell, this);
        }
        else
        {
            tails.Last().AddTail();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        discreteActionsOut[0] = 0; // default forward
        if (Keyboard.current.wKey.isPressed) discreteActionsOut[0] = 0;
        if (Keyboard.current.aKey.isPressed) discreteActionsOut[0] = 1;
        if (Keyboard.current.dKey.isPressed) discreteActionsOut[0] = 2;
    }
}