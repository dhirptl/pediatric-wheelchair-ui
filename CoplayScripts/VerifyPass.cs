using System.Reflection;
using UnityEngine;

/// <summary>Play-mode verification helpers for the accessibility pass.</summary>
public class VerifyPass
{
    private static ExplorerController Explorer()
    {
        return Object.FindObjectOfType<ExplorerController>(true);
    }

    private static string State(ExplorerController e)
    {
        var f = typeof(ExplorerController).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
        return f.GetValue(e).ToString();
    }

    public static string Snapshot()
    {
        var e = Explorer();
        if (e == null) return "no explorer";
        var b = WheelchairStateBridge.Instance;
        var ghost = GameObject.Find("GhostTargetMarker");
        return "state=" + State(e)
             + " hasGoal=" + (b != null && b.HasGoal)
             + " yaw=" + (b != null ? b.transform.eulerAngles.y.ToString("F1") : "?")
             + " label=" + (e.commandLabel != null ? e.commandLabel.text : "?")
             + " ghostActive=" + (ghost != null && ghost.activeSelf)
             + " fill=" + (e.windUpFill != null ? e.windUpFill.fillAmount.ToString("F2") : "none")
             + " turnDur=" + e.turnDuration + " windUp=" + e.windUpSeconds;
    }

    public static string Forward()
    {
        var e = Explorer();
        if (e == null) return "no explorer";
        e.BeginCommand(ExplorerController.Command.Forward);
        return Snapshot();
    }

    public static string TurnRight()
    {
        var e = Explorer();
        if (e == null) return "no explorer";
        e.BeginCommand(ExplorerController.Command.TurnRight);
        return Snapshot();
    }

    public static string Stop()
    {
        var e = Explorer();
        if (e == null) return "no explorer";
        e.DoStop();
        return Snapshot();
    }
}
