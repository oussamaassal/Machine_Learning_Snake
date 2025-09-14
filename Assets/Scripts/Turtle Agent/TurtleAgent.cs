using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.InputSystem;
using System.Collections;
using Unity.Hierarchy;

public class TurtleAgent : Agent
{
    [SerializeField] private Transform _goal;
    [SerializeField] private Renderer _groundRenderer;
    [SerializeField] private float _moveSpeed = 1.5f;
    [SerializeField] private float _rotationSpeed= 180f;

    private Renderer _renderer;

    [HideInInspector] public int currentEpisode = 0;
    [HideInInspector] public float cumulativeReward = 0f;

    private Color _defaultGroundColor;
    private Coroutine _flashGroundCoroutine;

    public override void Initialize()
    {
        base.Initialize();

        _renderer = GetComponent<Renderer>();
        currentEpisode = 0;
        cumulativeReward = 0f;

        if(_groundRenderer != null)
        {
            _defaultGroundColor = _groundRenderer.material.color;
        }
    }

    public override void OnEpisodeBegin()
    {
        if(_groundRenderer != null && cumulativeReward != 0f)
        {
            Color flashColor = cumulativeReward > 0 ? Color.green : Color.red;

            // Stop any existing FlashGround coroutine before starting a new one
            if(_flashGroundCoroutine != null)
            {
                StopCoroutine(_flashGroundCoroutine);
            }

            _flashGroundCoroutine = StartCoroutine(FlashGround(flashColor, 3.0f));
        }

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
        transform.localRotation = Quaternion.identity;
        transform.localPosition = new Vector3(0, 0.15f, 0);
        
        float randomAngle = Random.Range(0f, 360f);
        Vector3 randomDirection = Quaternion.Euler(0, randomAngle, 0) * Vector3.forward;

        float randomDistance = Random.Range(1f, 2.5f);

        Vector3 goalPosition = transform.localPosition + randomDirection * randomDistance;

        _goal.localPosition = new Vector3(goalPosition.x, 0.3f, goalPosition.z);
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        float goalPoxX_normalized = _goal.localPosition.x / 5f;
        float goalPoxZ_normalized = _goal.localPosition.z / 5f;

        float turtlePosX_normalized = transform.localPosition.x / 5f;
        float turtlePosZ_normalized = transform.localPosition.z / 5f;

        float turtleRotationY_normalized = (transform.localEulerAngles.y / 360f) * 2f - 1f;

        sensor.AddObservation(goalPoxX_normalized);
        sensor.AddObservation(goalPoxZ_normalized);
        sensor.AddObservation(turtlePosX_normalized);
        sensor.AddObservation(turtlePosZ_normalized);
        sensor.AddObservation(turtleRotationY_normalized);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        MoveAgent(actions.DiscreteActions);

        AddReward(-2f / MaxStep);

        cumulativeReward = GetCumulativeReward();
    }

    public void MoveAgent(ActionSegment<int> actions) 
    {
        var action = actions[0];

        switch (action)
        {
            case 1: // Move forward
                transform.position += transform.forward * _moveSpeed * Time.deltaTime;
                break;
            case 2: // Rotate left
                transform.Rotate(0f, -_rotationSpeed * Time.deltaTime, 0f);
                break;
            case 3: // Rotate right
                transform.Rotate(0f,_rotationSpeed * Time.deltaTime, 0f);
                break;
        }
    }



    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("goal"))
        {
            GoalReached();
        }

    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("wall"))
        {
            SetReward(-1f);
            _renderer.material.color = Color.red;
            //EndEpisode();
        }
    }

    public void GoalReached()
    {
        AddReward(1.0f);
        cumulativeReward =GetCumulativeReward();

        EndEpisode();
    }

    public void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("wall"))
        {
            AddReward(-0.01f * Time.fixedDeltaTime);
        }
    }

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
}



}
