using System.IO;
using UnityEngine;

public class Snap
{
    public static string Execute()
    {
        string dir = Path.Combine(Application.dataPath, "../Snapshots");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "game_view.png");
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);   // async: lands after a frame renders
        return "requested " + path;
    }
}
