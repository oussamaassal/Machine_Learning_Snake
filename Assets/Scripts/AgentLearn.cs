using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class AgentLearn : Agent
{
    [SerializeField] private SnakeController snakeController;
    [SerializeField] private Transform foodTransform;
    [SerializeField] private Transform poisonTransform;
    [SerializeField] private float moveSpeed = 5f;

    public override void OnEpisodeBegin()
    {
        // Reset snake, food, and poison positions
        snakeController.ResetSnake();
        foodTransform.localPosition = GetRandomGridPosition();
        poisonTransform.localPosition = GetRandomGridPosition();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Snake head position and direction
        sensor.AddObservation(snakeController.HeadPosition);
        sensor.AddObservation(snakeController.CurrentDirection);

        // Food and poison positions
        sensor.AddObservation(foodTransform.localPosition);
        sensor.AddObservation(poisonTransform.localPosition);

        // Tail positions (flattened for observation)
        foreach (var tailSegment in snakeController.TailSegments)
        {
            sensor.AddObservation(tailSegment.localPosition);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int action = actions.DiscreteActions[0];
        // 0: forward, 1: left, 2: right
        snakeController.Move(action);

        // Reward for surviving
        AddReward(0.01f);

        // Reward for eating food
        if (snakeController.HasEatenFood)
        {
            AddReward(1.0f);
            foodTransform.localPosition = GetRandomGridPosition();
            snakeController.HasEatenFood = false;
        }

        // Penalty for eating poison
        if (snakeController.HasEatenPoison)
        {
            AddReward(-1.0f);
            poisonTransform.localPosition = GetRandomGridPosition();
            snakeController.HasEatenPoison = false;
        }

        // Penalty for biting tail or hitting wall
        if (snakeController.HasDied)
        {
            AddReward(-2.0f);
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        // Example: map arrow keys to actions
        if (Input.GetKey(KeyCode.LeftArrow))
            discreteActionsOut[0] = 1;
        else if (Input.GetKey(KeyCode.RightArrow))
            discreteActionsOut[0] = 2;
        else
            discreteActionsOut[0] = 0;
    }

    private Vector3 GetRandomGridPosition()
    {
        // Implement grid logic as needed
        float x = Random.Range(-8, 8);
        float z = Random.Range(-8, 8);
        return new Vector3(x, 0.5f, z);
    }
}
