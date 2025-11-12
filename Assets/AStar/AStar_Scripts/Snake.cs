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

    private Vector2Int _currentDirection = Vector2Int.up;
    private Vector2Int _queuedDirection = Vector2Int.up;
    private Vector2Int _requestedDirection = Vector2Int.up;
    private bool directionChanged = false;

    public Cell currentCell;
    public Cell previousCell;
    public Cell pre_previousCell;

    [SerializeField] private float moveDelay = 0.2f;
    private float moveTimer = 0f;

    public List<Tail> tails;
    public GameObject tailGameObject;
    public GameObject Environment;

    [SerializeField] private float maxTimeWithoutFood = 25f;
    private float timeSinceLastFood = 0f;

    private List<Vector2Int> lastPath;
    private bool isResetting = false;

    // MCTS Parameters
    [SerializeField] private int mctsIterations = 200; // Increased for better decisions
    [SerializeField] private float explorationConstant = 1.41f; // c = sqrt(2) as per theory

    // Decision caching to prevent re-calculation every frame
    private Vector2Int cachedDecision;
    private bool hasDecision = false;
    private Vector2Int lastDecisionPosition;

    static readonly Vector2Int[] neighbors = {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
    };

    // MCTS Node Class - Stores full state for accurate simulation
    private class MCTSNode
    {
        // Reference to the outer Snake instance - Keep it private
        private Snake snakeInstance;

        // Public property to expose the Snake instance to the outer class
        public Snake SnakeInstance
        {
            get { return snakeInstance; }
        }

        public Vector2Int head;
        public List<Vector2Int> tail; // Store full tail positions
        public Vector2Int foodPos;
        public Vector2Int poisonPos; // Include poison in state
        public Vector2Int direction;
        public MCTSNode parent;
        public List<MCTSNode> children = new List<MCTSNode>();
        public int visits = 0;
        // Using totalReward to store cumulative wins (for win rate calculation)
        // Wins are +1, losses are -1, draws/neutral are 0.
        // Average reward = totalReward / visits = (wins - losses) / visits
        // This aligns with the concept of maximizing expected reward (or win rate).
        public float totalReward = 0f; // Store wins (rewards)
        public List<Vector2Int> untriedActions;

        public MCTSNode(Snake snake, Vector2Int h, List<Vector2Int> t, Vector2Int f, Vector2Int p, Vector2Int dir, MCTSNode par = null)
        {
            this.snakeInstance = snake; // Store the reference to the Snake instance
            head = h;
            tail = new List<Vector2Int>(t); // Copy tail list
            foodPos = f;
            poisonPos = p; // Store poison position
            direction = dir;
            parent = par;

            // Generate valid actions (no reversal, in bounds, no self-collision)
            // Use the stored Snake instance to call GetValidDirections
            untriedActions = snakeInstance.GetValidDirections(h, dir, t);
        }

        public bool IsFullyExpanded() => untriedActions.Count == 0;
        public bool IsTerminal()
        {
            // Use the stored Snake instance to access gridManager via the property
            return head == foodPos || head == poisonPos ||
                   !SnakeInstance.gridManager.grid.InBounds(head) ||
                   !SnakeInstance.gridManager.grid[head].isValid ||
                   tail.Contains(head);
        }

        // UCB1 formula for node selection
        // w = totalReward (cumulative reward, e.g., wins - losses)
        // n = visits
        // t = parent.visits
        // c = explorationConstant
        public float UCB1(MCTSNode parent, float c = 1.41f) // Use theoretical value
        {
            if (visits == 0) return float.MaxValue; // Prioritize unvisited nodes
            float exploitation = totalReward / visits; // Average reward (win rate approximation)
            float exploration = c * Mathf.Sqrt(Mathf.Log(parent.visits) / visits);
            return exploitation + exploration;
        }
    }


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
        foreach (Cell cell in gridManager.grid.data)
        {
            cell.isOccupied = false;
            cell.hasTail = false;
            cell.tail = null;
            cell.cost = 0;
            cell.state = Cell.CellState.None;
        }
        foreach (Tail tail in tails)
        {
            if (tail != null)
                Destroy(tail.gameObject);
        }
        tails.Clear();
        hasDecision = false;

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
        timeSinceLastFood = 0f;

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
        Cell foodCell;
        do
        {
            foodCell = gridManager.GetRandomValidCell();
        } while (foodCell.isOccupied || foodCell.hasTail);

        Cell poisonCell;
        do
        {
            poisonCell = gridManager.GetRandomValidCell();
        } while (poisonCell.isOccupied || poisonCell == foodCell || poisonCell.hasTail);

        _food.localPosition = foodCell.position;
        _poison.localPosition = poisonCell.position;

        gridManager.grid[foodCell.gridPosition].isOccupied = true;
        gridManager.grid[poisonCell.gridPosition].isOccupied = true;

        // Invalidate cached decision when objects spawn
        hasDecision = false;
    }

    private void Update()
    {
        moveTimer += Time.deltaTime;
        timeSinceLastFood += Time.deltaTime;

        if (timeSinceLastFood > maxTimeWithoutFood)
        {
            cumulativeReward -= 10f;
            Debug.Log("Starved!");
            BeginEpisode();
            return;
        }

        // Only run MCTS when we're about to move (not every frame)
        if (moveTimer >= moveDelay)
        {
            // Only recalculate if we don't have a decision or position changed
            if (!hasDecision || lastDecisionPosition != currentCell.gridPosition)
            {
                Vector2Int bestDirection = RunMCTS();

                // Validate the decision before applying (avoid reversal)
                if (bestDirection + _currentDirection != Vector2Int.zero)
                {
                    cachedDecision = bestDirection;
                    hasDecision = true;
                    lastDecisionPosition = currentCell.gridPosition;
                    _queuedDirection = bestDirection;
                }
                else
                {
                    // Fallback if best direction is reversal
                    List<Vector2Int> currentTailPos = tails.Select(t => t.currentCell.gridPosition).ToList();
                    List<Vector2Int> validDirs = GetValidDirections(currentCell.gridPosition, _currentDirection, currentTailPos);
                    if (validDirs.Count > 0)
                    {
                        _queuedDirection = validDirs[Random.Range(0, validDirs.Count)]; // Pick random valid
                    }
                    else
                    {
                        _queuedDirection = _currentDirection; // No valid moves, move forward (will die)
                    }
                }
            }

            // Execute movement
            ExecuteMove();
        }
    }

    private void ExecuteMove()
    {
        _currentDirection = _queuedDirection;
        currentCell.nextDirection = _currentDirection;

        Vector2Int nextPos = currentCell.gridPosition + _currentDirection;

        // Safety check before moving
        if (!gridManager.grid.InBounds(nextPos))
        {
            cumulativeReward -= 10f;
            Debug.Log("Out of bounds!");
            BeginEpisode();
            return;
        }

        Cell nextCell = gridManager.grid[nextPos];

        // Check for wall collision
        if (nextCell.state == Cell.CellState.Wall || !nextCell.isValid)
        {
            cumulativeReward -= 10f;
            Debug.Log("Hit wall!");
            BeginEpisode();
            return;
        }

        // Check for tail collision
        if (nextCell.hasTail && tails.Count > 0)
        {
            // Allow moving through the last tail segment (it's about to move)
            if (!tails.Last().isLastTail || nextCell.gridPosition != tails.Last().currentCell.gridPosition)
            {
                cumulativeReward -= 10f;
                Debug.Log("Hit tail!");
                BeginEpisode();
                return;
            }
        }

        // Update previous cells
        pre_previousCell = previousCell;
        previousCell = currentCell;
        currentCell = nextCell;

        // Mark current cell as occupied
        currentCell.isOccupied = true;

        // Handle occupancy for snake without tail
        if (tails.Count == 0)
        {
            if (previousCell != null) previousCell.isOccupied = true;
            if (pre_previousCell != null) pre_previousCell.isOccupied = false;
        }

        // Update snake position
        transform.localPosition = currentCell.position + new Vector3(0, 0.15f, 0);

        // Update all tail segments
        UpdateTails();

        UpdateRotation(_currentDirection);
        moveTimer = 0f;
        hasDecision = false; // Invalidate decision after move

        VisualizePath();
        gridManager.UpdateCells();
    }

    private void UpdateTails()
    {
        if (tails.Count == 0) return;

        // Update each tail segment to follow the one in front
        for (int i = 0; i < tails.Count; i++)
        {
            Tail tail = tails[i];

            // Get direction from current cell
            Vector2Int nextDir = tail.currentCell.nextDirection;
            tail.UpdateRotation(nextDir);

            Vector2Int nextPos = tail.currentCell.gridPosition + nextDir;

            if (gridManager.grid.InBounds(nextPos))
            {
                tail.previousCell = tail.currentCell;
                tail.currentCell = gridManager.grid[nextPos];

                // Update occupancy
                tail.currentCell.isOccupied = true;
                tail.previousCell.isOccupied = false;
                tail.currentCell.hasTail = true;
                tail.previousCell.hasTail = false;
                tail.currentCell.tail = tail;
                tail.previousCell.tail = null;

                tail.transform.localPosition = tail.currentCell.position + new Vector3(0, 0.15f, 0);

                // Mark last tail
                if (tail.isLastTail)
                {
                    tail.currentCell.state = Cell.CellState.LastTail;
                    tail.previousCell.state = Cell.CellState.None;
                }
            }
        }
    }

    // ==================== MCTS IMPLEMENTATION ====================

    private Vector2Int RunMCTS()
    {
        Vector2Int foodPos = new Vector2Int((int)_food.position.x, (int)_food.position.z);
        Vector2Int poisonPos = new Vector2Int((int)_poison.position.x, (int)_poison.position.z);
        List<Vector2Int> currentTail = tails.Select(t => t.currentCell.gridPosition).ToList();

        // Pass 'this' (the Snake instance) to the MCTSNode constructor
        MCTSNode root = new MCTSNode(this, currentCell.gridPosition, currentTail, foodPos, poisonPos, _currentDirection);

        for (int i = 0; i < mctsIterations; i++)
        {
            // 1. Selection
            MCTSNode node = root;
            while (!node.IsTerminal() && node.IsFullyExpanded())
            {
                // Safety check: Ensure children list is not empty before selecting
                if (node.children.Count == 0)
                {
                    Debug.LogWarning("Selection reached a node with no children. Breaking selection.");
                    break; // Exit selection loop if no children exist
                }

                // Select child with highest UCB1 value
                node = node.children.OrderByDescending(child => child.UCB1(node, explorationConstant)).First();
            }

            // 2. Expansion
            if (!node.IsTerminal() && !node.IsFullyExpanded())
            {
                // Select an untried action randomly
                Vector2Int action = node.untriedActions[Random.Range(0, node.untriedActions.Count)];
                node.untriedActions.Remove(action);

                // Simulate the action to get the new state
                Vector2Int newHead = node.head + action;
                List<Vector2Int> newTail = new List<Vector2Int>(node.tail);
                bool ateFood = (newHead == node.foodPos);

                if (ateFood)
                {
                    // Add old head as new tail segment (snake grows)
                    newTail.Insert(0, node.head);
                }
                else if (newTail.Count > 0)
                {
                    // Move tail: remove old tail end, add old head
                    newTail.RemoveAt(newTail.Count - 1);
                    newTail.Insert(0, node.head);
                }

                // Create new child node - Pass the Snake instance here too
                MCTSNode child = new MCTSNode(this, newHead, newTail, node.foodPos, node.poisonPos, action, node);
                node.children.Add(child);
                // node = child; // Don't reassign node here for MCTS logic
            }

            // 3. Simulation (Pure Random Rollout)
            float reward = RunRandomSimulation(node);

            // 4. Backpropagation
            while (node != null)
            {
                node.visits++;
                node.totalReward += reward; // Accumulate reward (win/loss/draw)
                node = node.parent;
            }
        }

        // Return the action corresponding to the child of the root with the highest visit count
        if (root.children.Count == 0)
        {
            Debug.LogWarning("MCTS returned no children. Likely trapped. Returning current direction.");
            return _currentDirection; // Fallback: return current direction if no options found
        }
        MCTSNode bestChild = root.children.OrderByDescending(c => c.visits).First();
        return bestChild.direction;
    }

    // Pure Random Simulation Policy
    private float RunRandomSimulation(MCTSNode node)
    {
        Vector2Int simHead = node.head;
        List<Vector2Int> simTail = new List<Vector2Int>(node.tail);
        Vector2Int simDir = node.direction;
        int steps = 0;
        const int maxSteps = 50; // Prevent infinite loops in simulation

        while (steps < maxSteps)
        {
            // Check for terminal states first
            if (simHead == node.foodPos)
            {
                return +1.0f; // Win: ate food
            }
            if (simHead == node.poisonPos)
            {
                return -1.0f; // Loss: ate poison
            }
            // Use the Snake instance stored in the node via the property to check bounds/collision
            if (!node.SnakeInstance.gridManager.grid.InBounds(simHead) || // Use property
                !node.SnakeInstance.gridManager.grid[simHead].isValid || // Use property
                simTail.Contains(simHead))
            {
                return -1.0f; // Loss: hit wall or tail
            }

            // Get valid actions for the simulated snake using the Snake instance via the property
            List<Vector2Int> validDirs = node.SnakeInstance.GetValidDirections(simHead, simDir, simTail); // Use property
            if (validDirs.Count == 0)
            {
                return -1.0f; // Loss: no valid moves
            }

            // Choose a random valid action
            Vector2Int action = validDirs[Random.Range(0, validDirs.Count)];
            Vector2Int newSimHead = simHead + action;

            // Update simulated tail based on the action
            List<Vector2Int> newSimTail = new List<Vector2Int>(simTail);
            bool ateFoodInSim = (newSimHead == node.foodPos);

            if (ateFoodInSim)
            {
                // Add old head as new tail segment (snake grows in sim)
                newSimTail.Insert(0, simHead);
            }
            else if (newSimTail.Count > 0)
            {
                // Move tail: remove old tail end, add old head
                newSimTail.RemoveAt(newSimTail.Count - 1);
                newSimTail.Insert(0, simHead);
            }

            // Update simulation state
            simHead = newSimHead;
            simTail = newSimTail;
            simDir = action;
            steps++;
        }

        // If max steps reached without termination, return a neutral reward (or a small penalty)
        // A neutral reward (0) is common if no terminal state is reached.
        // A small penalty (-0.1) could encourage shorter paths, but 0 is simpler.
        return 0.0f;
    }

    // Helper to get valid directions for a given head position, direction, and tail
    private List<Vector2Int> GetValidDirections(Vector2Int pos, Vector2Int currentDir, List<Vector2Int> tailPositions)
    {
        List<Vector2Int> valid = new List<Vector2Int>();

        foreach (Vector2Int dir in neighbors)
        {
            // Avoid reversing direction
            if (dir + currentDir == Vector2Int.zero) continue;

            Vector2Int newPos = pos + dir;

            // Check bounds, validity, and collision with tail using the instance's gridManager
            if (gridManager.grid.InBounds(newPos) &&
                gridManager.grid[newPos].isValid &&
                gridManager.grid[newPos].state != Cell.CellState.Wall &&
                !tailPositions.Contains(newPos))
            {
                valid.Add(dir);
            }
        }

        return valid;
    }

    // ==================== Visualization ====================

    private void VisualizePath()
    {
        foreach (Cell cell in gridManager.grid.data)
        {
            if (cell.state == Cell.CellState.Path)
                cell.state = Cell.CellState.None;
        }

        if (lastPath != null)
        {
            foreach (Vector2Int pos in lastPath)
            {
                if (gridManager.grid.InBounds(pos) &&
                    gridManager.grid[pos].state == Cell.CellState.None)
                {
                    gridManager.grid[pos].state = Cell.CellState.Path;
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (lastPath == null || gridManager == null)
            return;

        Gizmos.color = Color.yellow;
        for (int i = 1; i < lastPath.Count; i++)
        {
            Vector3 from = gridManager.grid[lastPath[i - 1]].position + new Vector3(0, 0.2f, 0);
            Vector3 to = gridManager.grid[lastPath[i]].position + new Vector3(0, 0.2f, 0);
            Gizmos.DrawLine(from, to);
            Gizmos.DrawSphere(from, 0.1f);
        }
        if (lastPath.Count > 0)
        {
            Vector3 last = gridManager.grid[lastPath[lastPath.Count - 1]].position + new Vector3(0, 0.2f, 0);
            Gizmos.DrawSphere(last, 0.1f);
        }
    }

    // ==================== Input / Rotation / Updates ====================

    public void MoveAgent(int action)
    {
        switch (action)
        {
            case 1: _requestedDirection = Vector2Int.up; break;
            case 2: _requestedDirection = Vector2Int.left; break;
            case 3: _requestedDirection = Vector2Int.right; break;
            case 4: _requestedDirection = Vector2Int.down; break;
        }

        if (_requestedDirection + _currentDirection != Vector2Int.zero)
        {
            _queuedDirection = _requestedDirection;
        }
    }

    private void UpdateRotation(Vector2Int direction)
    {
        float angle = 0f;
        if (direction == Vector2Int.up) angle = 0f;
        else if (direction == Vector2Int.right) angle = 90f;
        else if (direction == Vector2Int.down) angle = 180f;
        else if (direction == Vector2Int.left) angle = 270f;

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

    // ==================== Collision Handling ====================

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Food"))
        {
            Eat();
            gridManager.grid[new Vector2Int((int)_food.position.x, (int)_food.position.z)].isOccupied = false;
            gridManager.grid[new Vector2Int((int)_poison.position.x, (int)_poison.position.z)].isOccupied = false;
            SpawnObjects();
            timeSinceLastFood = 0f;
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
            Debug.Log("wall collision");
            BeginEpisode();
        }
        else if (other.CompareTag("Tail"))
        {
            cumulativeReward -= 10f;
            Debug.Log("tail collision");
            BeginEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("wall"))
        {
            cumulativeReward -= 10f;
            Debug.Log("wall collision");
            BeginEpisode();
        }
        else if (collision.gameObject.CompareTag("Tail"))
        {
            cumulativeReward -= 10f;
            Debug.Log("tail collision");
            BeginEpisode();
        }
    }

    public void Eat()
    {
        cumulativeReward += 10.0f;
        AddTail();
    }

    public void AddTail()
    {
        if (tails.Count == 0)
        {
            Cell tailCell = previousCell;
            if (tailCell == null) { Debug.LogError("Cannot add first tail: previousCell is null!"); return; } // Safety check

            tailCell.isOccupied = true;
            tailCell.hasTail = true;
            tailCell.nextDirection = _currentDirection;
            GameObject tailObj = Instantiate(tailGameObject, tailCell.position + new Vector3(0, 0.15f, 0), Quaternion.identity);
            tailObj.transform.localRotation = transform.localRotation;
            Tail tail = tailObj.GetComponent<Tail>();
            if (tail == null) { Debug.LogError("Tail prefab does not have a Tail component!"); return; } // Safety check
            tails.Add(tail);
            tail.InitializeTail(tailCell, this);
            tail.isLastTail = true;
        }
        else
        {
            // Safety check: ensure tails list is not empty before accessing Last()
            if (tails.Count == 0)
            {
                Debug.LogError("AddTail called in 'else' block but tails list is unexpectedly empty!");
                return; // Exit to prevent error
            }

            Tail lastTail = tails.Last();
            if (lastTail == null)
            {
                Debug.LogError("Last tail in list is null!");
                return; // Exit to prevent error
            }

            lastTail.isLastTail = false;
            lastTail.AddTail(); // This should add the new tail segment to the Snake.tails list via the Tail script
        }
    }

    private void FixedUpdate()
    {
        int action = 0;
        if (Keyboard.current.wKey.isPressed) action = 1;
        else if (Keyboard.current.aKey.isPressed) action = 2;
        else if (Keyboard.current.dKey.isPressed) action = 3;
        else if (Keyboard.current.sKey.isPressed) action = 4;

        // MoveAgent(action); // Uncomment if you want keyboard control
    }
}