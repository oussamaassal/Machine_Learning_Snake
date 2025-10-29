using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Linq;
using System.Collections.Generic;

public class Snake : MonoBehaviour
{
    [SerializeField] private Transform _food;
    [SerializeField] private Transform _poison;
    [SerializeField] private Renderer _groundRenderer;
    [SerializeField] private float _moveSpeed = 1.5f; // cells per second
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

    private bool moveRequested = false; // only move when requested (no delay)

    public List<Tail> tails;
    public GameObject tailGameObject;

    public GameObject Environment;

    [SerializeField] private float maxTimeWithoutFood = 10f; // seconds
    private float timeSinceLastFood = 0f;

    private List<Vector2Int> lastPath;
    private bool isResetting = false;

    // Auto-move timers
    private float _stepTimer = 0f;
    private float _stepInterval = 0.5f;


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

        // FSM: initialize and start in Idle
        InitializeFSM();

        // Configure step interval from speed (cells per second)
        _stepInterval = 1f / Mathf.Max(0.0001f, _moveSpeed);
    }

    private void Start()
    {
        BeginEpisode();
    }


    private void BeginEpisode()
    {
        foreach (Cell cell in gridManager.grid.data)
        {
            cell.isOccupied = false;
            cell.hasTail = false;
            cell.tail = null;
        }
        foreach (Tail tail in tails)
        {
            if (tail != null)
                Destroy(tail.gameObject);
        }
        tails.Clear();

        // FSM: set to Idle at the start of an episode
        ChangeState(SnakeState.Idle);

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

        SpawnObjects();

        // Auto-start moving (Idle -> Moving)
        FSM_OnGameStart();
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

        // Find a valid, unoccupied cell for poison, and not
        Cell poisonCell;
        do
        {
            poisonCell = gridManager.GetRandomValidCell();
        } while (poisonCell.isOccupied || poisonCell == foodCell || poisonCell.hasTail);

        _food.localPosition = foodCell.position;
        _poison.localPosition = poisonCell.position;

        gridManager.grid[foodCell.gridPosition].isOccupied = true;
        gridManager.grid[poisonCell.gridPosition].isOccupied = true;

        // Pathfinding removed — only movement logic kept
    }

    private void Update()
    {
        // Auto movement only; no player input.

        timeSinceLastFood += Time.deltaTime; // Increment starvation timer

        // Starvation check
        if (timeSinceLastFood > maxTimeWithoutFood)
        {
            cumulativeReward -= 5f; // Penalize for starving
            timeSinceLastFood = 0f; // Reset timer
        }

        // Accumulate time and step while in Moving/Growing
        _stepTimer += Time.deltaTime;

        if (_currentStateId == SnakeState.Moving || _currentStateId == SnakeState.Growing)
        {
            while (_stepTimer >= _stepInterval)
            {
                _stepTimer -= _stepInterval;

                // Plan next move only while in Moving state
                if (_currentStateId == SnakeState.Moving)
                {
                    AutoPlanAndQueueDirection();
                }

                PerformStep();
            }
        }

        // Optionally tick FSM per-frame if state-specific logic needed later
        // FSM_Update(Time.deltaTime);
    }

    // Moves the snake one cell forward according to _queuedDirection.
    private void PerformStep()
    {
        // Apply queued direction
        _currentDirection = _queuedDirection;
        currentCell.nextDirection = _currentDirection;

        if (gridManager.grid.InBounds(currentCell.gridPosition + _currentDirection))
        {
            pre_previousCell = previousCell;
            previousCell = currentCell;
            currentCell = gridManager.grid[currentCell.gridPosition + _currentDirection];
            currentCell.isOccupied = true;

            if (tails.Count == 0)
            {
                if (previousCell != null) previousCell.isOccupied = true;
                if (pre_previousCell != null) pre_previousCell.isOccupied = false;
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
            tail.currentCell.tail = tail;
        }

        UpdateRotation(_currentDirection);

        // Debug: show current FSM state and head position each step
        Debug.Log($"FSM State={_currentStateId} | Head={currentCell.gridPosition} | Dir={_currentDirection}");
    }

    public void MoveAgent(int action)
    {
        if (action == 0) return; // no action -> don't request a move

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
            moveRequested = true; // <-- mark that a move should occur on the next Update (after delay)
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

            // FSM: Any -> Dead
            FSM_OnCollision();

            BeginEpisode();
        }
        else if (other.CompareTag("Tail"))
        {
            cumulativeReward -= 1f;

            // FSM: Any -> Dead
            FSM_OnCollision();

            BeginEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("wall"))
        {
            cumulativeReward -= 10f;
            Debug.Log("wall");

            // FSM: Any -> Dead
            FSM_OnCollision();

            BeginEpisode();
        }
        else if (collision.gameObject.CompareTag("Tail"))
        {
            cumulativeReward -= 1f;

            // FSM: Any -> Dead
            FSM_OnCollision();

            BeginEpisode();
        }
    }

    public void Eat()
    {
        cumulativeReward += 5.0f;

        // FSM: Moving -> Growing
        FSM_OnFoodEaten();

        AddTail();

        // FSM: Growing -> Moving (call later if growth is not instant)
        FSM_OnGrowthComplete();
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


    // Keyboard input is handled in Update now
    private void FixedUpdate()
    {
        // Intentionally left empty
    }

    // =========================
    // Finite State Machine (FSM)
    // =========================

    // States (Idle, Moving, Growing, Dead)
    public enum SnakeState { Idle, Moving, Growing, Dead }

    // Events (triggers for transitions)
    public enum SnakeEvent
    {
        OnGameStart,       // Idle -> Moving
        OnFoodEaten,       // Moving -> Growing
        OnGrowthComplete,  // Growing -> Moving
        OnCollision        // Any -> Dead
    }

    // Base state with Enter/Update/Exit hooks (no rendering or input)
    private abstract class State
    {
        public abstract SnakeState Id { get; }
        public virtual void Enter(Snake owner) { }
        public virtual void Update(Snake owner, float dt) { }
        public virtual void Exit(Snake owner) { }
    }

    // Idle — Snake is stationary, waiting for the player to start.
    private sealed class IdleState : State
    {
        public override SnakeState Id => SnakeState.Idle;
        public override void Enter(Snake owner)
        {
            Debug.Log("FSM Enter: Idle");
            // Enter Idle: waiting for OnGameStart
            // Transition defined: OnGameStart -> Moving
        }
        public override void Update(Snake owner, float dt) { }
        public override void Exit(Snake owner) { }
    }

    // Moving — Snake moves continuously in the current direction.
    private sealed class MovingState : State
    {
        public override SnakeState Id => SnakeState.Moving;
        public override void Enter(Snake owner)
        {
            Debug.Log("FSM Enter: Moving");
            // Transition defined: OnFoodEaten -> Growing
        }
        public override void Update(Snake owner, float dt) { }
        public override void Exit(Snake owner) { }
    }

    // Growing — Snake has eaten food and is extending by one segment.
    private sealed class GrowingState : State
    {
        public override SnakeState Id => SnakeState.Growing;
        public override void Enter(Snake owner)
        {
            Debug.Log("FSM Enter: Growing");
            // Transition defined: OnGrowthComplete -> Moving
        }
        public override void Update(Snake owner, float dt) { }
        public override void Exit(Snake owner) { }
    }

    // Dead — Snake has collided with a wall or itself and stops moving.
    private sealed class DeadState : State
    {
        public override SnakeState Id => SnakeState.Dead;
        public override void Enter(Snake owner)
        {
            Debug.Log("FSM Enter: Dead");
            // Transition to Dead can occur from any state via OnCollision
        }
        public override void Update(Snake owner, float dt) { }
        public override void Exit(Snake owner) { }
    }

    // Storage for state instances and transitions
    private Dictionary<SnakeState, State> _states;
    private Dictionary<(SnakeState from, SnakeEvent evt), SnakeState> _transitions;

    private State _currentState;
    private SnakeState _currentStateId = SnakeState.Idle;

    public SnakeState CurrentState => _currentStateId;

    // Initialize FSM with states and transition table
    private void InitializeFSM()
    {
        _states = new Dictionary<SnakeState, State>
        {
            { SnakeState.Idle,   new IdleState() },
            { SnakeState.Moving, new MovingState() },
            { SnakeState.Growing,new GrowingState() },
            { SnakeState.Dead,   new DeadState() }
        };

        _transitions = new Dictionary<(SnakeState, SnakeEvent), SnakeState>
        {
            // OnGameStart → Idle → Moving
            {(SnakeState.Idle,   SnakeEvent.OnGameStart),      SnakeState.Moving},

            // OnFoodEaten → Moving → Growing
            {(SnakeState.Moving, SnakeEvent.OnFoodEaten),      SnakeState.Growing},

            // OnGrowthComplete → Growing → Moving
            {(SnakeState.Growing,SnakeEvent.OnGrowthComplete), SnakeState.Moving},

            // OnCollision → Any → Dead handled as a global rule in Trigger(...)
        };

        // Start in Idle by default
        ChangeState(SnakeState.Idle);
    }

    // Change state with Exit/Enter calls
    private void ChangeState(SnakeState newState)
    {
        if (_currentState != null && _currentStateId == newState) return;

        Debug.Log($"FSM Transition: {_currentStateId} -> {newState}");
        _currentState?.Exit(this);
        _currentStateId = newState;
        _currentState = _states[newState];
        _currentState.Enter(this);
    }

    // Invoke transitions by event
    public void Trigger(SnakeEvent evt)
    {
        Debug.Log($"FSM Event: {evt} in state {_currentStateId}");

        // Global transition: OnCollision -> Dead from any state
        if (evt == SnakeEvent.OnCollision && _currentStateId != SnakeState.Dead)
        {
            ChangeState(SnakeState.Dead);
            return;
        }

        // Table-driven transitions
        if (_transitions.TryGetValue((_currentStateId, evt), out var next))
        {
            ChangeState(next);
        }
    }

    // Call from your game loop if you want per-frame FSM updates
    public void FSM_Update(float dt)
    {
        _currentState?.Update(this, dt);
    }

    // Convenience wrappers to keep game code readable
    public void FSM_OnGameStart() => Trigger(SnakeEvent.OnGameStart);
    public void FSM_OnFoodEaten() => Trigger(SnakeEvent.OnFoodEaten);
    public void FSM_OnGrowthComplete() => Trigger(SnakeEvent.OnGrowthComplete);
    public void FSM_OnCollision() => Trigger(SnakeEvent.OnCollision);

    // =========================
    // Auto movement planning
    // =========================
    private void AutoPlanAndQueueDirection()
    {
        if (currentCell == null || gridManager == null || gridManager.grid == null || _food == null)
            return;

        Vector2Int start = currentCell.gridPosition;
        // Food is placed using localPosition, so use localPosition to read it back
        Vector2Int target = new Vector2Int(Mathf.RoundToInt(_food.localPosition.x),
                                           Mathf.RoundToInt(_food.localPosition.z));

        List<Vector2Int> path = FindPathBfs(start, target);

        Vector2Int chosen = _currentDirection;

        if (path != null && path.Count >= 2)
        {
            Vector2Int next = path[1];
            chosen = next - start;
        }

        // Avoid instant reverse; pick a safe greedy alternative if needed
        if (chosen + _currentDirection == Vector2Int.zero)
        {
            var alt = GreedyFallbackDirection(start, target);
            if (alt != Vector2Int.zero) chosen = alt;
        }

        _queuedDirection = chosen;
        Debug.Log($"AI plan (Moving): from={start} to={target} dir={chosen} pathLen={(path == null ? 0 : path.Count)}");
    }

    private List<Vector2Int> FindPathBfs(Vector2Int start, Vector2Int target)
    {
        if (start == target) return new List<Vector2Int> { start };

        var q = new Queue<Vector2Int>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var visited = new HashSet<Vector2Int> { start };

        q.Enqueue(start);

        while (q.Count > 0)
        {
            var cur = q.Dequeue();

            foreach (var d in neighbors)
            {
                // From the start node, you can skip the reverse direction for smoother moves
                if (cur == start && d + _currentDirection == Vector2Int.zero) continue;

                var nxt = cur + d;
                if (visited.Contains(nxt)) continue;

                if (!IsTraversable(nxt, target)) continue;

                visited.Add(nxt);
                cameFrom[nxt] = cur;

                if (nxt == target)
                {
                    return ReconstructPath(cameFrom, start, target);
                }

                q.Enqueue(nxt);
            }
        }

        return null;
    }

    private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int start, Vector2Int goal)
    {
        var path = new List<Vector2Int>();
        var cur = goal;
        path.Add(cur);
        while (cur != start)
        {
            cur = cameFrom[cur];
            path.Add(cur);
        }
        path.Reverse();
        return path;
    }

    // Traversable if:
    // - in bounds
    // - not tail
    // - not occupied, unless it's the target (food occupies its cell)
    private bool IsTraversable(Vector2Int pos, Vector2Int target)
    {
        if (!gridManager.grid.InBounds(pos)) return false;

        var cell = gridManager.grid[pos];

        if (pos == target) return true;         // allow stepping onto food
        if (cell.hasTail) return false;         // avoid body
        if (cell.isOccupied) return false;      // avoid anything else marked occupied

        return true;
    }

    private Vector2Int GreedyFallbackDirection(Vector2Int start, Vector2Int target)
    {
        int Heuristic(Vector2Int p) => Mathf.Abs(p.x - target.x) + Mathf.Abs(p.y - target.y);

        Vector2Int bestDir = Vector2Int.zero;
        int bestH = int.MaxValue;

        foreach (var d in neighbors)
        {
            if (d + _currentDirection == Vector2Int.zero) continue; // no reverse

            var nxt = start + d;
            if (!IsTraversable(nxt, target)) continue;

            int h = Heuristic(nxt);
            if (h < bestH)
            {
                bestH = h;
                bestDir = d;
            }
        }

        return bestDir;
    }
}