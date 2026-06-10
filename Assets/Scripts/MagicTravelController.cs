using UnityEngine;

/// <summary>
/// In-map Magic Travel destination buttons. The first press of a room that was
/// never calibrated starts the admin mini-map calibration flow; every later
/// press sends the wheelchair there through the WheelchairStateBridge.
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

        if (!cal.IsCalibrated(roomName))
        {
            cal.BeginCalibration(roomName);
            return;
        }

        if (cal.TryGetRoomPosition(roomName, out Vector3 pos) && WheelchairStateBridge.Instance != null)
            WheelchairStateBridge.Instance.SendNavigationGoal(pos);
    }
}
