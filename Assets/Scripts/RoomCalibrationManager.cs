using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Owns the world coordinates of named rooms ("Target_Kitchen", ...).
///
/// The coordinate SOURCE mirrors the SIMULATION_PLACEHOLDER / ROS_CONNECTED seam
/// used by WheelchairStateBridge (motion) and FollowAssistBackend (detection):
///   - SIMULATION_PLACEHOLDER (now): the Target_* scene markers, placed onto the
///     runtime-baked NavMesh - one per quadrant if a marker was authored off-mesh -
///     so every room resolves to a distinct, drivable spot with no setup.
///   - ROS_CONNECTED (this summer): the wheelchair already knows each room's
///     coordinate (saved waypoints / map server); they arrive over a ROS topic and
///     are returned through the same TryGetRoomPosition API - the GUI never owns
///     the coordinates.
///
/// An optional admin mini-map calibration (BeginCalibration/SetRoomPosition,
/// persisted in PlayerPrefs) can override the source for a specific room, but it is
/// no longer required for the room buttons to work.
/// </summary>
public class RoomCalibrationManager : MonoBehaviour
{
    public enum RoomSource { SIMULATION_PLACEHOLDER, ROS_CONNECTED }

    [Serializable]
    public class RoomEntry
    {
        public string name;
        public float x;
        public float z;
    }

    [Serializable]
    private class SaveData
    {
        public List<RoomEntry> entries = new List<RoomEntry>();
    }

    public static RoomCalibrationManager Instance { get; private set; }

    [Header("Architecture Toggle")]
    [Tooltip("SIMULATION = room coordinates come from the scene Target_* markers on the runtime NavMesh. ROS_CONNECTED = the wheelchair supplies known room waypoints from a ROS topic (this summer).")]
    public RoomSource currentMode = RoomSource.SIMULATION_PLACEHOLDER;

    [Tooltip("Scene markers (Target_*) placed onto the NavMesh when the map is ready. Auto-discovered by name prefix if left empty.")]
    public Transform[] roomTargets;
    [Tooltip("Search radius when snapping a marker that was authored near the NavMesh onto it.")]
    public float snapSampleRadius = 15f;
    [Tooltip("Normalized floor point (0..1 in X/Z) to scatter each room toward when its marker was authored off the current NavMesh. Keyed by marker name; unknown rooms fan out by index.")]
    public float roomSpreadClearanceCells = 2;

    [Header("Calibration UI")]
    [Tooltip("HUD text shown while waiting for the admin to tap the mini-map.")]
    public TMPro.TextMeshProUGUI promptText;

    public bool IsCalibrating => !string.IsNullOrEmpty(pendingRoom);

    private readonly Dictionary<string, Vector3> calibrated = new Dictionary<string, Vector3>();
    private string pendingRoom;

    void Awake()
    {
        Instance = this;
        Load();
    }

    void OnEnable()
    {
        MiniMapClickHandler.OnMiniMapWorldClick += HandleMiniMapClick;
    }

    void OnDisable()
    {
        MiniMapClickHandler.OnMiniMapWorldClick -= HandleMiniMapClick;
    }

    void Start()
    {
        if (promptText != null) promptText.gameObject.SetActive(false);
        if (MapGenerator.IsMapReady) PlaceRooms();
        else MapGenerator.OnMapReady += PlaceRooms;
    }

    void OnDestroy()
    {
        MapGenerator.OnMapReady -= PlaceRooms;
        if (Instance == this) Instance = null;
    }

    /// <summary>Arms the mini-map: the next tap on it drops this room's destination node.</summary>
    public void BeginCalibration(string roomName)
    {
        pendingRoom = roomName;
        if (promptText != null)
        {
            promptText.text = "ADMIN: tap the mini-map to place " + PrettyName(roomName);
            promptText.gameObject.SetActive(true);
        }
        Debug.Log("[RoomCalibration] Waiting for a mini-map tap to place '" + roomName + "'...");
    }

    private void HandleMiniMapClick(Vector3 worldPos)
    {
        if (!IsCalibrating) return;
        if (SetRoomPosition(pendingRoom, worldPos))
        {
            pendingRoom = null;
            if (promptText != null) promptText.gameObject.SetActive(false);
        }
        // On a failed sample (clicked inside a wall) stay armed so the admin can retry.
    }

    private static string PrettyName(string roomName)
        => roomName.StartsWith("Target_") ? roomName.Substring("Target_".Length) : roomName;

    /// <summary>
    /// The room's world coordinate. Admin mini-map calibration overrides everything;
    /// otherwise it comes from the active source (SIM markers now, ROS waypoints later).
    /// </summary>
    public bool TryGetRoomPosition(string roomName, out Vector3 pos)
    {
        if (currentMode == RoomSource.SIMULATION_PLACEHOLDER)
        {
            // SIM: the placed scene marker is the source of truth (it stands in for
            // the robot's known coordinate, and PlaceRooms guarantees it's on the
            // NavMesh). It deliberately takes precedence over any persisted mini-map
            // calibration, which can be stale from an older/larger map.
            Transform marker = FindTarget(roomName);
            if (marker != null) { pos = marker.position; return true; }
        }
        else // ROS_CONNECTED
        {
            // LATER THIS SUMMER: the wheelchair already knows each room's coordinate
            // (saved waypoints / map server). Read it from the ROS topic, e.g.:
            //   if (roomWaypointClient.TryGet(roomName, out pos)) return true;
        }

        // Fallback: an explicit admin mini-map calibration, if one was set.
        if (calibrated.TryGetValue(roomName, out pos)) return true;

        pos = Vector3.zero;
        return false;
    }

    public bool IsCalibrated(string roomName) => calibrated.ContainsKey(roomName);

    /// <summary>Stores a calibrated room position (NavMesh-validated) and persists it.</summary>
    public bool SetRoomPosition(string roomName, Vector3 worldPos)
    {
        if (!NavMesh.SamplePosition(worldPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            Debug.LogWarning("[RoomCalibration] '" + roomName + "' at " + worldPos + " is not near the NavMesh; ignored.");
            return false;
        }

        calibrated[roomName] = hit.position;
        Save();

        // Keep the scene marker in sync so the mini-map / scene view show the truth.
        Transform marker = FindTarget(roomName);
        if (marker != null) marker.position = hit.position;

        Debug.Log("[RoomCalibration] '" + roomName + "' calibrated to " + hit.position);
        return true;
    }

    // --- internals -----------------------------------------------------------

    // Normalized floor points (0..1 in X/Z) the four known rooms scatter toward when
    // their authored marker is off the current NavMesh - one per quadrant so the
    // destinations stay distinct. The SIM stand-in for the robot's known waypoints.
    private static readonly Dictionary<string, Vector2> SimQuadrants = new Dictionary<string, Vector2>
    {
        { "Target_Kitchen",    new Vector2(0.75f, 0.75f) },
        { "Target_Bathroom",   new Vector2(0.25f, 0.75f) },
        { "Target_Bedroom",    new Vector2(0.25f, 0.25f) },
        { "Target_LivingRoom", new Vector2(0.75f, 0.25f) },
    };

    /// <summary>
    /// SIM source: put every Target_* marker onto the baked NavMesh. A marker that
    /// was authored near the mesh keeps its spot (snapped on); one stranded off-mesh
    /// (e.g. after the map shrank) is relocated to its quadrant so the room is still
    /// reachable. ROS mode skips this - the wheelchair supplies the coordinates.
    /// </summary>
    private void PlaceRooms()
    {
        if (currentMode != RoomSource.SIMULATION_PLACEHOLDER) return;

        var map = FindObjectOfType<MapGenerator>();
        int index = 0;
        foreach (Transform t in AllTargets())
        {
            if (t == null) { index++; continue; }

            // Keep an authored position that's already on/near the mesh.
            if (NavMesh.SamplePosition(t.position, out NavMeshHit hit, snapSampleRadius, NavMesh.AllAreas))
            {
                t.position = hit.position;
                index++;
                continue;
            }

            // Off-mesh: relocate to a distinct reachable cell (quadrant by name, else by index).
            Vector2 norm = SimQuadrants.TryGetValue(t.name, out Vector2 q)
                ? q
                : new Vector2(((index % 2) == 0) ? 0.3f : 0.7f, ((index / 2) % 2 == 0) ? 0.3f : 0.7f);

            if (map != null && map.TryFindClearPositionNear(norm.x, norm.y, out Vector3 placed, (int)roomSpreadClearanceCells))
                t.position = placed;
            else
                Debug.LogWarning("[RoomCalibration] Could not place room marker '" + t.name + "' on the NavMesh.");
            index++;
        }
    }

    private IEnumerable<Transform> AllTargets()
    {
        if (roomTargets != null && roomTargets.Length > 0) return roomTargets;

        var found = new List<Transform>();
        foreach (GameObject go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.name.StartsWith("Target_")) found.Add(go.transform);
        }
        roomTargets = found.ToArray();
        return roomTargets;
    }

    private Transform FindTarget(string roomName)
    {
        foreach (Transform t in AllTargets())
        {
            if (t != null && t.name == roomName) return t;
        }
        return null;
    }

    private void Load()
    {
        calibrated.Clear();
        SaveData data = GamePrefs.GetJson<SaveData>(GamePrefs.RoomCalibration);
        if (data == null) return;
        foreach (RoomEntry e in data.entries)
            calibrated[e.name] = new Vector3(e.x, 0f, e.z);
    }

    private void Save()
    {
        var data = new SaveData();
        foreach (KeyValuePair<string, Vector3> kv in calibrated)
            data.entries.Add(new RoomEntry { name = kv.Key, x = kv.Value.x, z = kv.Value.z });
        GamePrefs.SetJson(GamePrefs.RoomCalibration, data);
    }
}
