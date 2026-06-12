using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The single architecture bridge between the Unity frontend and the wheelchair.
/// ALL motion commands and pose reads go through this component - nothing else in
/// the project is allowed to touch the NavMeshAgent. When the real robot comes
/// online this summer, flipping currentMode to ROS_CONNECTED reroutes the same
/// calls to the ROS 2 Nav2 action server / cmd_vel topic with zero changes to the
/// UI, gamification, or accessibility layers.
/// </summary>
public class WheelchairStateBridge : MonoBehaviour
{
    public enum ControlState { SIMULATION_PLACEHOLDER, ROS_CONNECTED }

    [Header("Architecture Toggle")]
    public ControlState currentMode = ControlState.SIMULATION_PLACEHOLDER;

    [Header("Simulated Placeholders")]
    public NavMeshAgent placeholderAgent; // Temp local navigation tool
    [Tooltip("Search radius when snapping goals or the chair itself onto the NavMesh.")]
    public float navSampleRadius = 8f;
    [Tooltip("HasGoal clears once the remaining path is this short (arrival).")]
    public float arriveTolerance = 0.5f;

    // Action triggered whenever the wheelchair moves (updates gamification/UI)
    public static Action<Vector3> OnWheelchairPoseUpdated;

    // Fired when a new navigation goal is accepted (path line + coin spawner listen).
    public static event Action<Vector3> OnNavigationGoalSet;

    public static WheelchairStateBridge Instance { get; private set; }

    public Vector3 CurrentGoal { get; private set; }
    public bool HasGoal { get; private set; }

    void Awake()
    {
        Instance = this;
        if (placeholderAgent == null) placeholderAgent = GetComponent<NavMeshAgent>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SendNavigationGoal(Vector3 globalCoordinates)
    {
        if (currentMode == ControlState.SIMULATION_PLACEHOLDER)
        {
            // RIGHT NOW: Use local Unity engine to route the placeholder capsule
            if (placeholderAgent == null || !EnsureOnNavMesh()) return;
            if (NavMesh.SamplePosition(globalCoordinates, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
                globalCoordinates = hit.position;
            placeholderAgent.SetDestination(globalCoordinates);
        }
        else
        {
            // LATER THIS SUMMER: Send the target vector data directly to the ROS 2 Nav2 Action Server
            // ros2Publisher.PublishGoal(globalCoordinates);
        }

        CurrentGoal = globalCoordinates;
        HasGoal = true;
        OnNavigationGoalSet?.Invoke(CurrentGoal);
    }

    public void SendLowLevelVelocity(float linearX, float angularZ)
    {
        if (currentMode == ControlState.SIMULATION_PLACEHOLDER)
        {
            // RIGHT NOW: Drive the capsule locally. Linear motion goes through
            // agent.Move (instead of transform.Translate) so the NavMeshAgent
            // keeps ownership of position and the chair can never clip walls.
            if (placeholderAgent != null && placeholderAgent.isOnNavMesh && linearX != 0f)
                placeholderAgent.Move(transform.forward * linearX * Time.deltaTime);
            if (angularZ != 0f)
                transform.Rotate(Vector3.up * angularZ * Time.deltaTime);
        }
        else
        {
            // LATER THIS SUMMER: Route these values directly into a ROS 2 'cmd_vel' topic publisher
            // ros2Publisher.PublishCmdVel(linearX, angularZ);
        }
    }

    public void StopMotion()
    {
        if (currentMode == ControlState.SIMULATION_PLACEHOLDER
            && placeholderAgent != null && placeholderAgent.isOnNavMesh)
        {
            placeholderAgent.ResetPath();
        }
        HasGoal = false;
    }

    /// <summary>Warps the agent onto the NavMesh if it isn't already. True when navigable.</summary>
    public bool EnsureOnNavMesh()
    {
        if (placeholderAgent == null) return false;
        if (placeholderAgent.isOnNavMesh) return true;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
            return placeholderAgent.Warp(hit.position);
        return false;
    }

    void Update()
    {
        if (currentMode == ControlState.SIMULATION_PLACEHOLDER)
        {
            // HasGoal must mean "motion in flight" - the Explorer panel shows a
            // STOP button while it is true - so clear it once the agent arrives
            // (StopMotion is the only other thing that clears it).
            if (HasGoal && placeholderAgent != null && placeholderAgent.isOnNavMesh
                && !placeholderAgent.pathPending
                && placeholderAgent.remainingDistance <= Mathf.Max(placeholderAgent.stoppingDistance, arriveTolerance))
            {
                HasGoal = false;
            }

            // Broadcast the local Unity capsule position to run the gamification engine
            OnWheelchairPoseUpdated?.Invoke(transform.position);
        }
        else
        {
            // LATER THIS SUMMER: Read the real incoming telemetry from the ROS 2 '/odom' topic
            // transform.position = ros2IncomingOdomPosition;
            // OnWheelchairPoseUpdated?.Invoke(transform.position);
        }
    }
}
