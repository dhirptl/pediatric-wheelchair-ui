using UnityEngine;

/// <summary>
/// In-map Magic Travel destination buttons. Each press sends the wheelchair to the
/// room's coordinate through the WheelchairStateBridge. The coordinate comes from
/// RoomCalibrationManager's active source (SIM markers now, the robot's ROS room
/// waypoints later) - no per-room calibration is required for the buttons to work.
/// </summary>
public class MagicTravelController : MonoBehaviour
{
    /// <summary>Wired to each room button with the marker name (e.g. "Target_Kitchen").</summary>
    public void OnRoomButton(string roomName)
    {
        var cal = RoomCalibrationManager.Instance;
        if (cal == null)
        {
            Debug.LogWarning("[MagicTravel] No RoomCalibrationManager in scene.");
            return;
        }

        if (cal.TryGetRoomPosition(roomName, out Vector3 pos) && WheelchairStateBridge.Instance != null)
            WheelchairStateBridge.Instance.SendNavigationGoal(pos);
        else
            Debug.LogWarning("[MagicTravel] No coordinate available for '" + roomName + "' yet.");
    }
}
