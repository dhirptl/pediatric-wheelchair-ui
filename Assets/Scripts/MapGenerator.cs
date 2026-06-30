using System;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using System.IO;

public class MapGenerator : MonoBehaviour
{
    [Header("Map Data")]
    [Tooltip("Map file under StreamingAssets/Maps. Drop a new PNG there and set this name to swap maps - no import settings required.")]
    public string mapFileName = "mymap_test4.png";
    [Tooltip("Fallback occupancy grid, used only if the StreamingAssets file is missing.")]
    public Texture2D occupancyGrid;
    public GameObject wallPrefab;

    [Header("Aesthetics")]
    public float wallHeight = 15f; // Tall, enclosing walls (~15 units; 1 unit ~= 1 m)
    public Material floorMaterial; // Slot for the floor material

    [Header("Generation")]
    [Tooltip("Pixels per wall cell. 1 = one cube per pixel (crisp but heavy). Higher = fewer, chunkier cubes. Uses OR-reduction so walls only thicken, never develop gaps.")]
    public int cellSize = 2;
    [Tooltip("World units per cell. Lower = smaller map. Independent of cellSize (pixel reduction).")]
    public float worldUnitsPerCell = 0.5f;
    [Tooltip("A pixel counts as a wall when its grayscale is below this value.")]
    [Range(0f, 1f)] public float wallThreshold = 0.5f;
    [Tooltip("Drop wall cells that have fewer than this many occupied orthogonal neighbors (removes lone speckles / stray dots on the floor). 0 disables de-speckling.")]
    public int minNeighbors = 1;

    [Header("Trim / Crop")]
    [Tooltip("Smallest fraction of the original PNG a crop may keep on either axis (guards against trimming the map to nothing).")]
    public float minCropFraction = 0.1f;

    // Normalized crop rect against the ORIGINAL PNG (0..1). Default = full map.
    // Persisted in PlayerPrefs and re-applied every launch so an admin trim sticks.
    private Vector2 cropMin = Vector2.zero;
    private Vector2 cropMax = Vector2.one;

    [Serializable]
    private class CropData { public float xMin, yMin, xMax, yMax; }

    /// <summary>The active crop rect in normalized original-PNG space (xy = min, wh = span).</summary>
    public Rect CurrentCrop => new Rect(cropMin.x, cropMin.y, cropMax.x - cropMin.x, cropMax.y - cropMin.y);

    [Header("Auto-Baking")]
    public NavMeshSurface navMeshSurface;

    // Exposed for verification/debugging after a build.
    public static int LastWallCount;

    // --- Readiness signal ---------------------------------------------------
    // Generation (incl. the NavMesh bake) runs synchronously in Awake, so by the
    // time any Start() runs the map is normally ready. Consumers should follow:
    //   if (MapGenerator.IsMapReady) Init(); else MapGenerator.OnMapReady += Init;
    // (and unsubscribe in OnDestroy) so they also survive a future re-generation.
    public static bool IsMapReady { get; private set; }
    public static event Action OnMapReady;

    // --- Grid queries (valid once IsMapReady) -------------------------------
    public bool[,] OccupiedCells { get; private set; }
    public int Cols { get; private set; }
    public int Rows { get; private set; }
    public float WorldWidth => Cols * cellWorld;
    public float WorldHeight => Rows * cellWorld;

    private float cellWorld;   // cell edge length in world units (== worldUnitsPerCell)
    private float offsetX, offsetZ;

    // CHANGED: Awake runs before Start, ensuring the floor exists before the wheelchair drops!
    void Awake()
    {
        IsMapReady = false;
        LoadCrop();            // re-apply a persisted admin trim before building
        Generate3DMap();
    }

    private void LoadCrop()
    {
        var data = GamePrefs.GetJson<CropData>(GamePrefs.MapCrop);
        if (data == null) { cropMin = Vector2.zero; cropMax = Vector2.one; return; }
        cropMin = new Vector2(Mathf.Clamp01(data.xMin), Mathf.Clamp01(data.yMin));
        cropMax = new Vector2(Mathf.Clamp01(data.xMax), Mathf.Clamp01(data.yMax));
        SanitizeCrop();
    }

    static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

    // Keep max > min and never let a crop shrink below minCropFraction on either axis.
    private void SanitizeCrop()
    {
        // Non-finite values (NaN/Inf) defeat every clamp below (NaN comparisons are
        // all false), and a persisted NaN crop would corrupt the next build - so any
        // garbage falls back to the full map.
        if (!Finite(cropMin.x) || !Finite(cropMin.y) || !Finite(cropMax.x) || !Finite(cropMax.y))
        {
            cropMin = Vector2.zero;
            cropMax = Vector2.one;
        }

        float minFrac = Mathf.Clamp01(minCropFraction);
        cropMin.x = Mathf.Clamp01(cropMin.x); cropMin.y = Mathf.Clamp01(cropMin.y);
        cropMax.x = Mathf.Clamp01(cropMax.x); cropMax.y = Mathf.Clamp01(cropMax.y);
        if (cropMax.x - cropMin.x < minFrac) cropMax.x = Mathf.Min(1f, cropMin.x + minFrac);
        if (cropMax.y - cropMin.y < minFrac) cropMax.y = Mathf.Min(1f, cropMin.y + minFrac);
        cropMin.x = Mathf.Clamp(cropMin.x, 0f, cropMax.x - minFrac);
        cropMin.y = Mathf.Clamp(cropMin.y, 0f, cropMax.y - minFrac);
    }

    public void Generate3DMap()
    {
        Texture2D grid = LoadOccupancyGrid();
        if (grid == null)
        {
            Debug.LogError("MapGenerator: no occupancy grid available (StreamingAssets file missing and no fallback assigned).");
            return;
        }

        GameObject mapParent = new GameObject("AutoGeneratedMap");

        int fullW = grid.width;
        int fullH = grid.height;

        // Apply the admin trim: only build the cropped pixel window of the PNG.
        // Everything below works in window-local pixel coords (wx0/wy0 = origin in the
        // full image), so floor size, centering, walls, NavMesh and the published grid
        // all come out the cropped size -> the kept map auto-compresses to fill the view.
        SanitizeCrop();
        int wx0 = Mathf.Clamp(Mathf.FloorToInt(cropMin.x * fullW), 0, fullW - 1);
        int wy0 = Mathf.Clamp(Mathf.FloorToInt(cropMin.y * fullH), 0, fullH - 1);
        int wx1 = Mathf.Clamp(Mathf.CeilToInt(cropMax.x * fullW), wx0 + 1, fullW);
        int wy1 = Mathf.Clamp(Mathf.CeilToInt(cropMax.y * fullH), wy0 + 1, fullH);
        int w = wx1 - wx0;   // cropped window dimensions
        int h = wy1 - wy0;

        // World size per cell, decoupled from the pixel-reduction factor below.
        float wc = Mathf.Max(0.01f, worldUnitsPerCell);

        // 2. OCCUPANCY -> CELL GRID (block-based, OR-reduced so walls stay watertight)
        int cell = Mathf.Max(1, cellSize);
        Color[] pixels = grid.GetPixels(); // single read instead of width*height GetPixel calls
        int cols = (w + cell - 1) / cell;
        int rows = (h + cell - 1) / cell;

        // Center the map on the origin, in world units.
        offsetX = cols * wc / 2f;
        offsetZ = rows * wc / 2f;

        // 1. BUILD THE FLOOR (default Unity plane is 10x10 units, so divide by 10 to fit)
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "AutoFloor";
        floor.transform.localScale = new Vector3(cols * wc / 10f, 1, rows * wc / 10f);
        floor.transform.position = Vector3.zero;
        floor.transform.parent = mapParent.transform;
        if (floorMaterial != null)
            floor.GetComponent<Renderer>().material = floorMaterial;

        bool[,] occ = new bool[cols, rows];
        for (int cxi = 0; cxi < cols; cxi++)
        {
            for (int cyi = 0; cyi < rows; cyi++)
            {
                bool occupied = false;
                int bx = cxi * cell, by = cyi * cell;
                // ix/iy are window-local; offset by (wx0,wy0) into the full-image array.
                for (int ix = bx; ix < bx + cell && ix < w && !occupied; ix++)
                {
                    for (int iy = by; iy < by + cell && iy < h; iy++)
                    {
                        if (pixels[(wx0 + ix) + (wy0 + iy) * fullW].grayscale < wallThreshold) { occupied = true; break; }
                    }
                }
                occ[cxi, cyi] = occupied;
            }
        }

        // 3. DE-SPECKLE: drop isolated cells with too few occupied orthogonal neighbors.
        if (minNeighbors > 0)
        {
            bool[,] cleaned = new bool[cols, rows];
            for (int cxi = 0; cxi < cols; cxi++)
            {
                for (int cyi = 0; cyi < rows; cyi++)
                {
                    if (!occ[cxi, cyi]) continue;
                    int n = 0;
                    if (cxi > 0 && occ[cxi - 1, cyi]) n++;
                    if (cxi < cols - 1 && occ[cxi + 1, cyi]) n++;
                    if (cyi > 0 && occ[cxi, cyi - 1]) n++;
                    if (cyi < rows - 1 && occ[cxi, cyi + 1]) n++;
                    cleaned[cxi, cyi] = n >= minNeighbors;
                }
            }
            occ = cleaned;
        }

        // 4. BUILD THE WALLS
        int wallCount = 0;
        for (int cxi = 0; cxi < cols; cxi++)
        {
            for (int cyi = 0; cyi < rows; cyi++)
            {
                if (!occ[cxi, cyi]) continue;
                float cx = cxi * wc + wc / 2f;
                float cz = cyi * wc + wc / 2f;
                Vector3 spawnPosition = new Vector3(cx - offsetX, wallHeight / 2f, cz - offsetZ);

                GameObject newWall = Instantiate(wallPrefab, spawnPosition, Quaternion.identity);
                newWall.transform.localScale = new Vector3(wc, wallHeight, wc);
                newWall.transform.parent = mapParent.transform;
                wallCount++;
            }
        }

        LastWallCount = wallCount;
        Debug.Log("Map Built and Centered! Walls: " + wallCount);

        // 5. AUTO-BAKE THE NAVMESH
        // Self-heal: the inspector reference can be lost on save/reload, so fall back to the
        // NavMeshSurface on this same GameObject.
        if (navMeshSurface == null) navMeshSurface = GetComponent<NavMeshSurface>();
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
            Debug.Log("NavMesh Automatically Baked!");
        }
        else
        {
            Debug.LogWarning("No NavMeshSurface found; the avatar will have no NavMesh to navigate.");
        }

        // 6. PUBLISH GRID + READINESS so spawners/calibration/minimap can initialize.
        OccupiedCells = occ;
        Cols = cols;
        Rows = rows;
        cellWorld = wc;
        IsMapReady = true;
        OnMapReady?.Invoke();
    }

    /// <summary>World-space center (y = 0) of a grid cell, matching the wall spawn math.</summary>
    public Vector3 CellToWorld(int cx, int cy)
    {
        return new Vector3(
            cx * cellWorld + cellWorld / 2f - offsetX,
            0f,
            cy * cellWorld + cellWorld / 2f - offsetZ);
    }

    // --- Trim / Crop API (used by the admin MapTrimController) ----------------

    /// <summary>
    /// Normalized 0..1 position of a world point across the *currently built* (cropped)
    /// map. Inverse of the centering math in CellToWorld/Generate3DMap. The map is
    /// centered on the origin, so world.x spans [-offsetX, +offsetX] == [0, Cols*cellWorld].
    /// </summary>
    public Vector2 WorldToCurrentNormalized(Vector3 world)
    {
        float wPix = Cols * cellWorld;
        float hPix = Rows * cellWorld;
        float u = wPix > 0f ? (world.x + offsetX) / wPix : 0f;
        float v = hPix > 0f ? (world.z + offsetZ) / hPix : 0f;
        return new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
    }

    /// <summary>
    /// Compose a keep-rectangle expressed in 0..1 of the CURRENTLY built map into the
    /// crop against the ORIGINAL PNG, then persist it. Caller reloads the scene to
    /// rebuild against the new crop. Composition lets repeated trims narrow further.
    /// </summary>
    public void SetCropComposed(Rect normOverCurrent)
    {
        // Ignore garbage (e.g. a degenerate camera projection) rather than persisting it.
        if (!Finite(normOverCurrent.xMin) || !Finite(normOverCurrent.yMin)
            || !Finite(normOverCurrent.xMax) || !Finite(normOverCurrent.yMax))
            return;

        float curW = cropMax.x - cropMin.x;
        float curH = cropMax.y - cropMin.y;

        float nxMin = cropMin.x + Mathf.Clamp01(normOverCurrent.xMin) * curW;
        float nxMax = cropMin.x + Mathf.Clamp01(normOverCurrent.xMax) * curW;
        float nyMin = cropMin.y + Mathf.Clamp01(normOverCurrent.yMin) * curH;
        float nyMax = cropMin.y + Mathf.Clamp01(normOverCurrent.yMax) * curH;

        cropMin = new Vector2(Mathf.Min(nxMin, nxMax), Mathf.Min(nyMin, nyMax));
        cropMax = new Vector2(Mathf.Max(nxMin, nxMax), Mathf.Max(nyMin, nyMax));
        SanitizeCrop();
        SaveCrop();
    }

    /// <summary>Clear the trim back to the full PNG and persist. Caller reloads.</summary>
    public void ResetCrop()
    {
        cropMin = Vector2.zero;
        cropMax = Vector2.one;
        GamePrefs.DeleteKey(GamePrefs.MapCrop);
    }

    private void SaveCrop()
    {
        GamePrefs.SetJson(GamePrefs.MapCrop, new CropData
        {
            xMin = cropMin.x, yMin = cropMin.y, xMax = cropMax.x, yMax = cropMax.y
        });
    }

    /// <summary>
    /// Finds an open spawn position: the free cell nearest the grid center whose
    /// Chebyshev neighborhood of <paramref name="clearanceCells"/> is also free,
    /// confirmed against the baked NavMesh. Robust to map swaps - no manual
    /// transform calibration needed.
    /// </summary>
    public bool TryFindClearPosition(out Vector3 worldPos, int clearanceCells = 2)
        => TryFindClearPositionNear(0.5f, 0.5f, out worldPos, clearanceCells);

    /// <summary>
    /// Like <see cref="TryFindClearPosition"/> but biased toward a normalized grid
    /// point (normX/normY in 0..1). Returns the open, NavMesh-confirmed cell nearest
    /// that point. Used to scatter distinct room waypoints across the floor (e.g.
    /// one per quadrant) so each Magic Travel destination resolves to a different
    /// reachable spot, regardless of which map PNG is loaded.
    /// </summary>
    public bool TryFindClearPositionNear(float normX, float normY, out Vector3 worldPos, int clearanceCells = 2)
    {
        worldPos = Vector3.zero;
        if (OccupiedCells == null) return false;

        float targetX = Mathf.Clamp01(normX) * (Cols - 1);
        float targetY = Mathf.Clamp01(normY) * (Rows - 1);

        int bestX = -1, bestY = -1;
        float bestDist = float.MaxValue;
        for (int x = 0; x < Cols; x++)
        {
            for (int y = 0; y < Rows; y++)
            {
                if (!IsNeighborhoodClear(x, y, clearanceCells)) continue;
                float dx = x - targetX, dy = y - targetY;
                float d = dx * dx + dy * dy;
                if (d < bestDist) { bestDist = d; bestX = x; bestY = y; }
            }
        }

        if (bestX < 0) return false;

        Vector3 candidate = CellToWorld(bestX, bestY);
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, cellWorld * 2f, NavMesh.AllAreas))
        {
            worldPos = hit.position;
            return true;
        }
        return false;
    }

    private bool IsNeighborhoodClear(int cx, int cy, int radius)
    {
        for (int x = cx - radius; x <= cx + radius; x++)
        {
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                if (x < 0 || y < 0 || x >= Cols || y >= Rows) return false;
                if (OccupiedCells[x, y]) return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Loads the occupancy grid from StreamingAssets/Maps/<mapFileName> at runtime.
    /// Texture2D.LoadImage yields a readable, uncompressed, no-mip texture, which
    /// sidesteps all import-setting issues. Falls back to the serialized texture.
    /// </summary>
    Texture2D LoadOccupancyGrid()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Maps", mapFileName);
        if (File.Exists(path))
        {
            byte[] data = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(data)) // resizes to the image; readable + uncompressed + no mips
            {
                tex.filterMode = FilterMode.Point;
                Debug.Log("MapGenerator: loaded map '" + mapFileName + "' (" + tex.width + "x" + tex.height + ") from StreamingAssets.");
                return tex;
            }
            Debug.LogWarning("MapGenerator: could not decode '" + path + "'; using fallback texture.");
        }
        else
        {
            Debug.LogWarning("MapGenerator: no map file at '" + path + "'; using fallback serialized texture.");
        }
        return occupancyGrid;
    }
}
