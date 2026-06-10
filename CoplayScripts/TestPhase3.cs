using UnityEngine;

public class TestPhase3
{
    static ExplorerUIController Ctrl() => Object.FindObjectOfType<ExplorerUIController>(true);

    public static string Snapshot()
    {
        var c = Ctrl();
        if (c == null) return "no controller";
        var s = c.scanGroup.scanner;
        return "strategy=" + c.strategy
             + " idx=" + s.CurrentIndex + " row=" + s.CurrentRow + " col=" + s.CurrentCol + " colStage=" + s.InColumnStage
             + " dashActive=" + c.dashboardPanel.activeSelf + " gridActive=" + c.gridMenuPanel.activeSelf
             + " fwdScale=" + c.forwardButton.transform.localScale.x.ToString("F3")
             + " leftScale=" + c.turnLeftButton.transform.localScale.x.ToString("F3");
    }

    public static string FastTimeScan()
    {
        var c = Ctrl();
        if (c == null) return "no controller";
        c.scanGroup.scanner.dwellSeconds = 1.5f; // fast dwell for observation only
        c.SetStrategy(2); // TimeScan
        return Snapshot();
    }

    public static string GridStrategy()
    {
        var c = Ctrl();
        if (c == null) return "no controller";
        c.SetStrategy(1); // GridToggleSelect
        return Snapshot();
    }

    public static string TurnLeft()
    {
        var c = Ctrl();
        var bridge = WheelchairStateBridge.Instance;
        if (c == null || bridge == null) return "missing refs";
        float before = bridge.transform.eulerAngles.y;
        c.OnTurnLeftClicked();
        return "yawBefore=" + before.ToString("F1");
    }

    public static string ReadYaw()
    {
        var bridge = WheelchairStateBridge.Instance;
        return bridge == null ? "no bridge" : "yaw=" + bridge.transform.eulerAngles.y.ToString("F1");
    }
}
