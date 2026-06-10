using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Draws a glowing neon track on the floor along the wheelchair's planned route.
/// Event-driven: re-paths immediately when a new goal is set and at most every
/// repathInterval while driving (throttled off the bridge's pose event).
/// Pre-allocated NavMeshPath/corner buffer - zero steady-state GC allocation.
/// Pure visualization: reads NavMesh path data, never commands the agent.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class PathVisualizer : MonoBehaviour
{
    [Tooltip("Glowing line material (NeonPath_Mat).")]
    public Material lineMaterial;
    public float width = 0.35f;
    [Tooltip("Lift above the floor so the line never z-fights.")]
    public float yOffset = 0.2f;
    [Tooltip("Minimum seconds between path recalculations while driving.")]
    public float repathInterval = 0.5f;
    [Tooltip("The line clears once the chair is this close to the goal.")]
    public float arriveDistance = 1.5f;

    private LineRenderer line;
    private NavMeshPath path;
    private readonly Vector3[] corners = new Vector3[64];
    private Vector3 goal;
    private bool hasGoal;
    private float nextRepathTime;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.widthMultiplier = width;
        if (lineMaterial != null) line.material = lineMaterial;
        line.textureMode = LineTextureMode.Tile;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.positionCount = 0;
        path = new NavMeshPath();
    }

    void OnEnable()
    {
        WheelchairStateBridge.OnNavigationGoalSet += HandleGoal;
        WheelchairStateBridge.OnWheelchairPoseUpdated += HandlePose;
    }

    void OnDisable()
    {
        WheelchairStateBridge.OnNavigationGoalSet -= HandleGoal;
        WheelchairStateBridge.OnWheelchairPoseUpdated -= HandlePose;
    }

    private void HandleGoal(Vector3 newGoal)
    {
        goal = newGoal;
        hasGoal = true;
        Repath(transform.position);
    }

    private void HandlePose(Vector3 pose)
    {
        if (!hasGoal) return;
        if ((pose - goal).sqrMagnitude <= arriveDistance * arriveDistance)
        {
            Clear();
            return;
        }
        if (Time.time >= nextRepathTime) Repath(pose);
    }

    private void Repath(Vector3 from)
    {
        nextRepathTime = Time.time + repathInterval;
        if (!NavMesh.CalculatePath(from, goal, NavMesh.AllAreas, path))
        {
            Clear();
            return;
        }
        int count = path.GetCornersNonAlloc(corners);
        line.positionCount = count;
        for (int i = 0; i < count; i++)
            line.SetPosition(i, corners[i] + Vector3.up * yOffset);
    }

    private void Clear()
    {
        hasGoal = false;
        line.positionCount = 0;
    }
}
