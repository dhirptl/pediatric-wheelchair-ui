using UnityEngine;

public class TestBridge
{
    public static string SendForward()
    {
        var ctrl = Object.FindObjectOfType<ExplorerUIController>();
        var bridge = WheelchairStateBridge.Instance;
        if (ctrl == null || bridge == null) return "ctrl=" + (ctrl != null) + " bridge=" + (bridge != null);
        Vector3 before = bridge.transform.position;
        ctrl.ExecuteForward();
        return "before=" + before + " goal=" + bridge.CurrentGoal + " hasGoal=" + bridge.HasGoal;
    }

    public static string ReadPose()
    {
        var bridge = WheelchairStateBridge.Instance;
        if (bridge == null) return "no bridge";
        return "pos=" + bridge.transform.position + " hasGoal=" + bridge.HasGoal
             + " onNavMesh=" + (bridge.placeholderAgent != null && bridge.placeholderAgent.isOnNavMesh);
    }
}
