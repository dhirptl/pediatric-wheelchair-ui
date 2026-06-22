using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Edit-mode inspector: reports the onClick persistent listeners wired on the
// settings-menu SMART GUIDE button, and whether it's in the pause scan ring.
public static class InspectSmartGuideButton
{
    // Runtime: open the pause/settings overlay so it can be captured/seen.
    public static string OpenPauseMenu()
    {
        if (GameModeManager.Instance == null) return "no GameModeManager";
        GameModeManager.Instance.OpenPause();
        return "pause overlay opened";
    }

    public static string Execute()
    {
        var sb = new StringBuilder();
        var canvas = GameObject.Find("GameHUDCanvas");
        if (canvas == null) return "GameHUDCanvas NOT FOUND";
        var t = canvas.transform.Find("PauseOverlayPanel/SettingsBox/Btn_ModeSmartGuide");
        if (t == null) return "Btn_ModeSmartGuide NOT FOUND";
        var btnGo = t.gameObject;
        var btn = btnGo.GetComponent<Button>();
        int n = btn.onClick.GetPersistentEventCount();
        sb.Append("onClick listeners=" + n);
        for (int i = 0; i < n; i++)
        {
            var tgt = btn.onClick.GetPersistentTarget(i);
            sb.Append(" | [" + i + "] " + (tgt != null ? tgt.GetType().Name : "null") + "." + btn.onClick.GetPersistentMethodName(i));
        }
        sb.Append(" | interactable=" + btn.interactable + " active=" + btnGo.activeInHierarchy);

        var psc = canvas.transform.Find("PauseOverlayPanel").GetComponent<PanelScanController>();
        bool inRing = false;
        if (psc != null && psc.group != null && psc.group.options != null)
            foreach (var o in psc.group.options) if (o == btn) inRing = true;
        sb.Append(" | inScanRing=" + inRing);
        return sb.ToString();
    }
}
