using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Warps the wheelchair avatar to a guaranteed-clear spot once the runtime map is
/// ready. Replaces manual "pause the game and drag the capsule" calibration: the
/// spawn is sampled from the occupancy grid (free cell nearest the map center with
/// clear surroundings) so it survives map swaps without any saved transforms.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class AvatarSpawner : MonoBehaviour
{
    [Tooltip("The map generator to query. Auto-found if left empty.")]
    public MapGenerator map;
    [Tooltip("How many grid cells of free space the spawn point must have on every side.")]
    public int clearanceCells = 2;

    private NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (map == null) map = FindObjectOfType<MapGenerator>();

        if (MapGenerator.IsMapReady) Spawn();
        else MapGenerator.OnMapReady += Spawn;
    }

    void OnDestroy()
    {
        MapGenerator.OnMapReady -= Spawn;
    }

    private void Spawn()
    {
        if (map != null && map.TryFindClearPosition(out Vector3 pos, clearanceCells))
        {
            agent.Warp(pos);
            SnapCamera();
            Debug.Log("[AvatarSpawner] Spawned at clear position " + pos);
            return;
        }

        // Fallback: snap wherever we already are onto the NavMesh.
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            SnapCamera();
            Debug.LogWarning("[AvatarSpawner] No clear cell found; snapped in place at " + hit.position);
        }
        else
        {
            Debug.LogError("[AvatarSpawner] Could not place the avatar on the NavMesh at all.");
        }
    }

    /// <summary>A warp teleports the chair - the follow camera must jump with it, not ease.</summary>
    private void SnapCamera()
    {
        var follow = FindObjectOfType<SmoothCameraFollow>();
        if (follow != null) follow.SnapToTarget();
    }
}
