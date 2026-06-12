using System.Text;
using UnityEditor;
using UnityEngine;

public class WireScanGroups
{
    public static string Execute()
    {
        // ScanGroup is a plain [Serializable] class embedded in PanelScanController,
        // so set group.scanner.dwellSeconds through the owning components.
        var log = new StringBuilder();
        foreach (var psc in Object.FindObjectsOfType<PanelScanController>(true))
        {
            var so = new SerializedObject(psc);
            var prop = so.FindProperty("group.scanner.dwellSeconds");
            if (prop == null) { log.Append(psc.name).Append("=noProp; "); continue; }
            prop.floatValue = 8f;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(psc);
            log.Append(psc.name).Append("=set; ");
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        return log.Length > 0 ? log.ToString() : "no PanelScanControllers found";
    }
}
