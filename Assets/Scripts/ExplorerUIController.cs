using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Explorer Mode controller. The tablet acts purely as a frontend client: button presses
/// update the wheelchair's NavMesh destination rather than moving it directly, keeping the
/// movement logic clean and ready for future ROS 2 integration.
///
/// Wire the three dashboard buttons to:
///   Move Forward -> OnMoveForwardClicked()
///   Turn Left    -> OnTurnLeftClicked()
///   Turn Right   -> OnTurnRightClicked()
/// </summary>
public class ExplorerUIController : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The wheelchair's NavMeshAgent. Auto-found by name if left empty.")]
    public NavMeshAgent agent;
    [Tooltip("Name used to auto-find the avatar if 'agent' is not assigned.")]
    public string avatarName = "Wheelchair_Avatar";

    [Header("Control Feel")]
    [Tooltip("How far ahead each 'Move Forward' tap drives, in world units.")]
    public float stepDistance = 5f;
    [Tooltip("Degrees rotated in place per 'Turn' tap.")]
    public float turnAngle = 30f;
    [Tooltip("How far to search for the nearest walkable point when snapping a step/warp onto the NavMesh. Kept small so a blocked forward step snaps locally instead of across the map.")]
    public float navSampleRadius = 8f;

    void Start()
    {
        if (agent == null)
        {
            var avatar = GameObject.Find(avatarName);
            if (avatar != null) agent = avatar.GetComponent<NavMeshAgent>();
        }

        if (agent == null)
        {
            Debug.LogWarning("[ExplorerUIController] No NavMeshAgent found; buttons will do nothing.");
            return;
        }

        // The NavMesh is baked in MapGenerator.Awake, which runs before any Start(), so it is
        // valid by now. The agent may have failed to attach at load (it initialized before the
        // bake) -- snap it onto the freshly baked NavMesh so movement works.
        EnsureOnNavMesh();
    }

    public void OnMoveForwardClicked()
    {
        if (!IsReady()) return;
        Vector3 target = agent.transform.position + agent.transform.forward * stepDistance;
        DriveTo(target);
    }

    public void OnTurnLeftClicked()  { TurnBy(-turnAngle); }
    public void OnTurnRightClicked() { TurnBy(turnAngle); }

    void TurnBy(float degrees)
    {
        if (!IsReady()) return;
        // Rotate in place. While the agent is idle it does not override transform rotation, so
        // the new heading sticks and the next 'Move Forward' follows it.
        agent.transform.Rotate(0f, degrees, 0f, Space.World);
    }

    void DriveTo(Vector3 worldPoint)
    {
        // Snap the requested point to the nearest walkable spot so we never path into a wall.
        if (NavMesh.SamplePosition(worldPoint, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    void EnsureOnNavMesh()
    {
        if (agent.isOnNavMesh) return;
        if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
    }

    bool IsReady()
    {
        if (agent == null) return false;
        EnsureOnNavMesh();
        return agent.isOnNavMesh;
    }
}
