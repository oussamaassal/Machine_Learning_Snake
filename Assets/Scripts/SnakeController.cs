using System.Collections.Generic;
using UnityEngine;

public class SnakeController : MonoBehaviour
{
    public Vector3 HeadPosition => segments[0].localPosition;
    public Vector3 CurrentDirection { get; private set; } = Vector3.forward;
    public List<Transform> TailSegments => segments.GetRange(1, segments.Count - 1);

    public bool HasEatenFood { get; set; }
    public bool HasEatenPoison { get; set; }
    public bool HasDied { get; set; }

    [SerializeField] private Transform segmentPrefab;
    [SerializeField] private int initialSize = 3;
    [SerializeField] private float gridSize = 1.0f;
    [SerializeField] private Vector2 gridBounds = new Vector2(8, 8);

    public List<Transform> segments = new List<Transform>();

    public void ResetSnake()
    {
        // Destroy old segments
        for (int i = 1; i < segments.Count; i++)
        {
            Destroy(segments[i].gameObject);
        }
        segments.Clear();

        // Create head
        Transform head = Instantiate(segmentPrefab, transform);
        head.localPosition = Vector3.zero;
        segments.Add(head);

        // Create initial tail
        for (int i = 1; i < initialSize; i++)
        {
            Transform segment = Instantiate(segmentPrefab, transform);
            segment.localPosition = new Vector3(0, 0, -i * gridSize);
            segments.Add(segment);
        }

        CurrentDirection = Vector3.forward;
        HasEatenFood = false;
        HasEatenPoison = false;
        HasDied = false;
    }

    public void Move(int action)
    {
        // 0: forward, 1: left, 2: right
        if (action == 1)
            CurrentDirection = Quaternion.Euler(0, -90, 0) * CurrentDirection;
        else if (action == 2)
            CurrentDirection = Quaternion.Euler(0, 90, 0) * CurrentDirection;

        Vector3 nextPosition = segments[0].localPosition + CurrentDirection * gridSize;

        // Check wall collision
        if (Mathf.Abs(nextPosition.x) > gridBounds.x || Mathf.Abs(nextPosition.z) > gridBounds.y)
        {
            HasDied = true;
            return;
        }

        // Move tail
        for (int i = segments.Count - 1; i > 0; i--)
        {
            segments[i].localPosition = segments[i - 1].localPosition;
        }
        segments[0].localPosition = nextPosition;

        // Check self collision
        for (int i = 1; i < segments.Count; i++)
        {
            if (segments[0].localPosition == segments[i].localPosition)
            {
                HasDied = true;
                return;
            }
        }

        // Check food/poison collision
        Collider[] hitColliders = Physics.OverlapSphere(segments[0].position, gridSize * 0.5f);
        foreach (var hit in hitColliders)
        {
            if (hit.CompareTag("food"))
            {
                HasEatenFood = true;
                Grow();
            }
            else if (hit.CompareTag("poison"))
            {
                HasEatenPoison = true;
            }
        }
    }

    private void Grow()
    {
        Transform segment = Instantiate(segmentPrefab, transform);
        segment.localPosition = segments[segments.Count - 1].localPosition;
        segments.Add(segment);
    }
}
