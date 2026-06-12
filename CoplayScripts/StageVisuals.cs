using UnityEngine;

public class StageVisuals
{
    public static string WindUp()
    {
        var e = Object.FindObjectOfType<ExplorerController>(true);
        if (e == null) return "no explorer";
        e.windUpSeconds = 20f;          // runtime only: hold READY... for capture
        e.BeginCommand(ExplorerController.Command.Forward);
        return "staged windup";
    }

    public static string Stopped()
    {
        var e = Object.FindObjectOfType<ExplorerController>(true);
        if (e == null) return "no explorer";
        e.stoppedFlashSeconds = 20f;    // runtime only: hold STOPPED for capture
        e.DoStop();
        return "staged stopped";
    }

    public static string TurnGlyph()
    {
        var e = Object.FindObjectOfType<ExplorerController>(true);
        if (e == null) return "no explorer";
        e.turnDuration = 20f;           // runtime only: hold TURNING for capture
        e.BeginCommand(ExplorerController.Command.TurnRight);
        return "staged turn";
    }
}
