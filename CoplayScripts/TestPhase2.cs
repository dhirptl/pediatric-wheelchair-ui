using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Play-mode end-to-end test of the Phase 2 calibration + mode flow.</summary>
public class TestPhase2
{
    public static string SwitchToMagicTravel()
    {
        var gmm = GameModeManager.Instance;
        if (gmm == null) return "no GameModeManager";
        gmm.SetModeMagicTravel();
        var dash = GameObject.Find("GameHUDCanvas/ControlDashboard"); // null when inactive
        var dest = GameObject.Find("GameHUDCanvas/DestinationPanel");
        return "mode=" + gmm.CurrentMode + " dashboardActive=" + (dash != null) + " destPanelActive=" + (dest != null);
    }

    public static string ClickKitchen()
    {
        var btn = GameObject.Find("GameHUDCanvas/DestinationPanel/Btn_Target_Kitchen");
        if (btn == null) return "kitchen button not found (panel inactive?)";
        btn.GetComponent<Button>().onClick.Invoke();
        var cal = RoomCalibrationManager.Instance;
        return "calibrating=" + cal.IsCalibrating + " calibrated=" + cal.IsCalibrated("Target_Kitchen")
             + " goal=" + (WheelchairStateBridge.Instance != null ? WheelchairStateBridge.Instance.CurrentGoal.ToString() : "?")
             + " hasGoal=" + WheelchairStateBridge.Instance.HasGoal;
    }

    public static string ClickMiniMapCenter()
    {
        var img = GameObject.Find("GameHUDCanvas/MiniMapPanel/MiniMapImage");
        if (img == null) return "minimap image not found";
        var rt = (RectTransform)img.transform;
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        // Screen-space-overlay canvas: world corners are already screen pixels.
        Vector2 screenCenter = (corners[0] + corners[2]) / 2f;

        var ped = new PointerEventData(EventSystem.current) { position = screenCenter };
        ExecuteEvents.Execute(img, ped, ExecuteEvents.pointerClickHandler);

        var cal = RoomCalibrationManager.Instance;
        return "clickedAt=" + screenCenter + " calibrating=" + cal.IsCalibrating
             + " kitchenCalibrated=" + cal.IsCalibrated("Target_Kitchen");
    }
}
