using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Owns the world coordinates of named rooms ("Target_Kitchen", ...).
/// Sources, in priority order:
///   1. Admin-calibrated coordinates (dropped on the mini-map, persisted as JSON
///      in PlayerPrefs so they survive between trial sessions).
///   2. The Target_* scene markers, snapped onto the baked NavMesh on map load.
/// Phase 2 adds the mini-map click calibration flow on top of this store.
/// </summary>
public class RoomCalibrationManager : MonoBehaviour
{
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

    [Tooltip("Scene markers (Target_*) snapped to the NavMesh when the map is ready. Auto-discovered by name prefix if left empty.")]
    public Transform[] roomTargets;
    [Tooltip("Search radius when snapping a marker onto the NavMesh.")]
    public float snapSampleRadius = 15f;

    private readonly Dictionary<string, Vector3> calibrated = new Dictionary<string, Vector3>();

    void Awake()
    {
        Instance = this;
        Load();
    }

    void Start()
    {
        if (MapGenerator.IsMapReady) SnapTargetsToNavMesh();
        else MapGenerator.OnMapReady += SnapTargetsToNavMesh;
    }

    void OnDestroy()
    {
        MapGenerator.OnMapReady -= SnapTargetsToNavMesh;
        if (Instance == this) Instance = null;
    }

    /// <summary>Calibrated coordinates first, snapped scene marker as fallback.</summary>
    public bool TryGetRoomPosition(string roomName, out Vector3 pos)
    {
        if (calibrated.TryGetValue(roomName, out pos)) return true;

        Transform marker = FindTarget(roomName);
        if (marker != null)
        {
            pos = marker.position;
            return true;
        }

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

    private void SnapTargetsToNavMesh()
    {
        foreach (Transform t in AllTargets())
        {
            if (t == null) continue;
            if (NavMesh.SamplePosition(t.position, out NavMeshHit hit, snapSampleRadius, NavMesh.AllAreas))
            {
                t.position = hit.position;
            }
            else
            {
                Debug.LogWarning("[RoomCalibration] Marker '" + t.name + "' found no NavMesh within "
                    + snapSampleRadius + " m - calibrate it via the mini-map.");
            }
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
