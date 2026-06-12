using System.Text;
using UnityEditor;
using UnityEngine;

public class WireAccessibilityPass2
{
    public static string Execute()
    {
        var log = new StringBuilder();
        try
        {
            var pop = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/pop_soft.wav");
            var coin = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/coin_sparkle.wav");
            var locked = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/locked_buzz.wav");
            log.Append("clips=").Append(pop != null).Append(coin != null).Append(locked != null).Append("; ");

            int juiced = 0;
            foreach (var juice in Object.FindObjectsOfType<ButtonJuice>(true))
            {
                juice.popClip = pop;
                EditorUtility.SetDirty(juice);
                juiced++;
            }
            log.Append("juiced=").Append(juiced).Append("; ");

            var score = Object.FindObjectOfType<ScoreManager>(true);
            if (score != null) { score.collectClip = coin; EditorUtility.SetDirty(score); }
            log.Append("score=").Append(score != null).Append("; ");

            var shop = Object.FindObjectOfType<ThemeShopController>(true);
            if (shop != null)
            {
                shop.lockedClip = locked;
                if (shop.scanner != null) shop.scanner.dwellSeconds = 8f;
                EditorUtility.SetDirty(shop);
            }
            log.Append("shop=").Append(shop != null).Append("; ");

            var scanGroupType = System.Type.GetType("ScanGroup, Assembly-CSharp");
            log.Append("sgType=").Append(scanGroupType != null).Append("; ");
            if (scanGroupType != null)
            {
                var scannerField = scanGroupType.GetField("scanner");
                var dwellField = scannerField != null ? scannerField.FieldType.GetField("dwellSeconds") : null;
                log.Append("sgFields=").Append(scannerField != null).Append(dwellField != null).Append("; ");
                var found = Object.FindObjectsOfType(scanGroupType, true);
                log.Append("sgFound=").Append(found != null ? found.Length : -1).Append("; ");
                int groups = 0;
                if (found != null && scannerField != null && dwellField != null)
                {
                    foreach (var g in found)
                    {
                        if (g == null) continue;
                        object scanner = scannerField.GetValue(g);
                        if (scanner == null) continue;
                        dwellField.SetValue(scanner, 8f);
                        EditorUtility.SetDirty(g);
                        groups++;
                    }
                }
                log.Append("scanGroups=").Append(groups).Append("; ");
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            log.Append("dirty=true");
        }
        catch (System.Exception ex)
        {
            log.Append(" EX: ").Append(ex.GetType().Name).Append(" ").Append(ex.Message)
               .Append(" @ ").Append(ex.StackTrace);
        }
        return log.ToString();
    }
}
