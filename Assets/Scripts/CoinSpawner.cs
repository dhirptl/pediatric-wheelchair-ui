using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Drops pooled coins along the NavMesh route every time a navigation goal is
/// set, sampling evenly spaced points between the path corners. Coins are
/// pre-instantiated (no runtime Instantiate churn) and registered with the
/// ScoreManager, which handles distance-based pickup.
/// </summary>
public class CoinSpawner : MonoBehaviour
{
    public GameObject coinPrefab;
    public int poolSize = 32;
    [Tooltip("Meters between coins along the path.")]
    public float spacing = 3f;
    [Tooltip("Meters of path to leave empty right in front of the chair.")]
    public float skipLead = 2f;
    [Tooltip("Coin float height above the floor.")]
    public float coinHeight = 0.7f;

    private Coin[] pool;
    private NavMeshPath path;
    private readonly Vector3[] corners = new Vector3[64];

    void Start()
    {
        path = new NavMeshPath();
        pool = new Coin[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            var go = Instantiate(coinPrefab, transform);
            go.name = "Coin_" + i;
            pool[i] = go.GetComponent<Coin>();
            go.SetActive(false);
        }
    }

    void OnEnable()
    {
        WheelchairStateBridge.OnNavigationGoalSet += HandleGoal;
    }

    void OnDisable()
    {
        WheelchairStateBridge.OnNavigationGoalSet -= HandleGoal;
    }

    private void HandleGoal(Vector3 goal)
    {
        if (pool == null) return;
        DespawnAll();

        var bridge = WheelchairStateBridge.Instance;
        if (bridge == null) return;
        if (!NavMesh.CalculatePath(bridge.transform.position, goal, NavMesh.AllAreas, path)) return;

        int cornerCount = path.GetCornersNonAlloc(corners);
        float nextAt = skipLead;
        float traveled = 0f;
        int spawned = 0;

        for (int i = 1; i < cornerCount && spawned < pool.Length; i++)
        {
            Vector3 a = corners[i - 1];
            Vector3 b = corners[i];
            float segment = Vector3.Distance(a, b);
            while (traveled + segment >= nextAt && spawned < pool.Length)
            {
                float t = (nextAt - traveled) / segment;
                Vector3 p = Vector3.Lerp(a, b, t) + Vector3.up * coinHeight;
                pool[spawned].Spawn(p);
                if (ScoreManager.Instance != null) ScoreManager.Instance.Register(pool[spawned]);
                spawned++;
                nextAt += spacing;
            }
            traveled += segment;
        }
    }

    private void DespawnAll()
    {
        foreach (Coin c in pool)
            if (c != null) c.Despawn();
    }
}
