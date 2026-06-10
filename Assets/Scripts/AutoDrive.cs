using UnityEngine;

/// <summary>
/// Magic Travel: reads the destination chosen in the main menu (PlayerPrefs) and
/// sends a navigation goal through the WheelchairStateBridge once the runtime map
/// (and its NavMesh) is ready. Destination resolution prefers admin-calibrated
/// room coordinates when available, falling back to the Target_* scene markers.
/// </summary>
public class AutoDrive : MonoBehaviour
{
    private WheelchairStateBridge bridge;
    private bool driven;

    void Start()
    {
        bridge = GetComponent<WheelchairStateBridge>();
        if (bridge == null)
        {
            Debug.LogError("[AutoDrive] No WheelchairStateBridge on the avatar; Magic Travel disabled.");
            return;
        }

        if (MapGenerator.IsMapReady) Drive();
        else MapGenerator.OnMapReady += Drive;
    }

    void OnDestroy()
    {
        MapGenerator.OnMapReady -= Drive;
    }

    private void Drive()
    {
        if (driven) return;
        driven = true;

        string targetRoomName = GamePrefs.GetString(GamePrefs.Destination);
        if (string.IsNullOrEmpty(targetRoomName))
        {
            Debug.Log("[AutoDrive] No saved destination; staying put (Explorer mode or fresh start).");
            return;
        }
        GamePrefs.DeleteKey(GamePrefs.Destination);

        if (TryResolveDestination(targetRoomName, out Vector3 pos))
        {
            bridge.SendNavigationGoal(pos);
            Debug.Log("[AutoDrive] Navigation goal sent for '" + targetRoomName + "' at " + pos);
        }
        else
        {
            Debug.LogError("[AutoDrive] Could not resolve a position for destination '" + targetRoomName + "'.");
        }
    }

    private bool TryResolveDestination(string roomName, out Vector3 pos)
    {
        // Admin-calibrated coordinates win (set via the mini-map, Phase 2).
        if (RoomCalibrationManager.Instance != null
            && RoomCalibrationManager.Instance.TryGetRoomPosition(roomName, out pos))
            return true;

        GameObject marker = GameObject.Find(roomName);
        if (marker != null)
        {
            pos = marker.transform.position;
            return true;
        }

        pos = Vector3.zero;
        return false;
    }
}
