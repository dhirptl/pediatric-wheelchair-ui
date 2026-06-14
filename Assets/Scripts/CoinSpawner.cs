using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Spawns pooled coins for both game modes:
///   - Explorer  : ~explorerCoinCount coins scattered at random walkable spots
///                 across the whole map (a free-roam collect-a-thon). They persist
///                 across forward/turn nav goals and are only cleared on a mode swap.
///   - MagicTravel: coins dropped along the NavMesh route on each navigation goal,
///                 spaced out (larger `spacing`) so the trip yields only a few.
///
/// Coins are pre-instantiated (no runtime Instantiate churn) and registered with the
/// ScoreManager, which handles distance-based pickup.
/// </summary>
public class CoinSpawner : MonoBehaviour
{
    public GameObject coinPrefab;
    public int poolSize = 32;
    [Tooltip("Meters between coins along the Magic Travel path. Larger = fewer coins per trip.")]
    public float spacing = 6f;
    [Tooltip("Meters of path to leave empty right in front of the chair (Magic Travel).")]
    public float skipLead = 2f;
    [Tooltip("Coin float height above the floor.")]
    public float coinHeight = 0.7f;
    [Tooltip("How many coins to scatter around the map in Explorer mode.")]
    public int explorerCoinCount = 15;
    [Tooltip("Keep scattered Explorer coins at least this far from the chair's start.")]
    public float scatterMinFromChair = 3f;
    [Tooltip("Minimum spacing between two scattered Explorer coins.")]
    public float scatterMinSeparation = 2f;

    private Coin[] pool;
    private NavMeshPath path;
    private readonly Vector3[] corners = new Vector3[64];
    private GameModeManager modeManager;
    private MapGenerator map;

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

        map = FindObjectOfType<MapGenerator>();

        // Instance is set in GameModeManager.Awake, so it is ready by Start.
        modeManager = GameModeManager.Instance;
        if (modeManager != null) modeManager.OnModeChanged += HandleModeChanged;

        // Initial Explorer scatter, once the map + NavMesh are ready.
        if (MapGenerator.IsMapReady) TryScatterIfExplorer();
        else MapGenerator.OnMapReady += HandleMapReady;
    }

    void OnEnable()
    {
        WheelchairStateBridge.OnNavigationGoalSet += HandleGoal;
    }

    void OnDisable()
    {
        WheelchairStateBridge.OnNavigationGoalSet -= HandleGoal;
    }

    void OnDestroy()
    {
        if (modeManager != null) modeManager.OnModeChanged -= HandleModeChanged;
        MapGenerator.OnMapReady -= HandleMapReady;
    }

    private void HandleMapReady()
    {
        MapGenerator.OnMapReady -= HandleMapReady;
        TryScatterIfExplorer();
    }

    private void HandleModeChanged(GameModeManager.Mode mode)
    {
        DespawnAll();
        // Explorer: scatter a fresh field of coins to collect.
        // Magic Travel: coins are dropped per-route in HandleGoal instead.
        if (mode == GameModeManager.Mode.Explorer) ScatterExplorerCoins();
    }

    private void TryScatterIfExplorer()
    {
        if (GameModeManager.Instance != null &&
            GameModeManager.Instance.CurrentMode == GameModeManager.Mode.Explorer)
            ScatterExplorerCoins();
    }

    /// <summary>
    /// Magic Travel reward: drops spaced-out coins along the route to the goal.
    /// No-op in Explorer mode so scattered coins survive each forward nav goal.
    /// </summary>
    private void HandleGoal(Vector3 goal)
    {
        if (pool == null) return;
        if (GameModeManager.Instance == null ||
            GameModeManager.Instance.CurrentMode != GameModeManager.Mode.MagicTravel)
            return;

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

    /// <summary>
    /// Explorer reward: scatter coins at random walkable cells across the map,
    /// confirmed against the baked NavMesh, away from the chair and from each other.
    /// </summary>
    private void ScatterExplorerCoins()
    {
        if (pool == null) return;
        DespawnAll();

        if (map == null) map = FindObjectOfType<MapGenerator>();
        if (map == null || map.OccupiedCells == null) return;

        Vector3 chairPos = WheelchairStateBridge.Instance != null
            ? WheelchairStateBridge.Instance.transform.position
            : Vector3.zero;

        int want = Mathf.Min(explorerCoinCount, pool.Length);
        float minFromChairSqr = scatterMinFromChair * scatterMinFromChair;
        float minSepSqr = scatterMinSeparation * scatterMinSeparation;

        int spawned = 0;
        int attempts = 0;
        int maxAttempts = want * 40 + 200;

        while (spawned < want && attempts < maxAttempts)
        {
            attempts++;
            int cx = UnityEngine.Random.Range(0, map.Cols);
            int cy = UnityEngine.Random.Range(0, map.Rows);
            if (map.OccupiedCells[cx, cy]) continue;                       // wall cell

            Vector3 cell = map.CellToWorld(cx, cy);
            if (!NavMesh.SamplePosition(cell, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
            if ((hit.position - chairPos).sqrMagnitude < minFromChairSqr) continue;

            Vector3 p = hit.position + Vector3.up * coinHeight;

            bool tooClose = false;
            for (int i = 0; i < spawned; i++)
            {
                if ((pool[i].transform.position - p).sqrMagnitude < minSepSqr) { tooClose = true; break; }
            }
            if (tooClose) continue;

            pool[spawned].Spawn(p);
            if (ScoreManager.Instance != null) ScoreManager.Instance.Register(pool[spawned]);
            spawned++;
        }

        if (spawned < want)
            Debug.LogWarning("[CoinSpawner] Scattered only " + spawned + "/" + want +
                             " Explorer coins (map may be cramped).");
    }

    private void DespawnAll()
    {
        if (pool == null) return;
        foreach (Coin c in pool)
            if (c != null) c.Despawn();
    }
}
