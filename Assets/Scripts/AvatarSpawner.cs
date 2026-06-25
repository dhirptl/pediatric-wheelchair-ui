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

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        // The runtime NavMesh isn't baked until the map loads. Awake runs before the
        // agent's OnEnable, so disabling it here stops it from logging "Failed to
        // create agent because there is no valid NavMesh" at scene load. Spawn()
        // re-enables it once the bake is done.
        if (!MapGenerator.IsMapReady) agent.enabled = false;
    }

    void Start()
    {
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
        // NavMesh exists now, so it's safe to create the agent (no warning).
        if (!agent.enabled) agent.enabled = true;

        if (map != null && map.TryFindClearPosition(out Vector3 pos, clearanceCells))
        {
            agent.Warp(pos);
            Debug.Log("[AvatarSpawner] Spawned at clear position " + pos);
            return;
        }

        // Fallback: snap wherever we already are onto the NavMesh.
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            Debug.LogWarning("[AvatarSpawner] No clear cell found; snapped in place at " + hit.position);
        }
        else
        {
            Debug.LogError("[AvatarSpawner] Could not place the avatar on the NavMesh at all.");
        }
    }
}
