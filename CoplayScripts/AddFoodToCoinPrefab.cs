using UnityEngine;
using UnityEditor;

/// <summary>
/// One-shot editor helper: turns the coin collectible into a food billboard.
/// Adds a "FoodQuad" child (SpriteRenderer + FoodBillboard) to Assets/PreFabs/Coin.prefab,
/// sizes it from the default food sprite, and disables the leftover coin cylinder.
/// UnityEngine + UnityEditor only (no TMPro/UI), returns a summary string, saves in place.
/// Idempotent: re-running just re-applies the same setup.
/// </summary>
public static class AddFoodToCoinPrefab
{
    const string PrefabPath = "Assets/PreFabs/Coin.prefab";
    const string DefaultSpritePath = "Assets/Resources/Food/ice_cream.png";
    const float DesiredHeight = 0.6f; // world units for the food picture

    public static string Execute()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return "ABORT: Coin.prefab not found at " + PrefabPath;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var log = new System.Text.StringBuilder();
        try
        {
            // Hide the old coin cylinder - food replaces the coin look (kept for reversibility).
            Transform body = root.transform.Find("CoinBody");
            if (body != null)
            {
                var mr = body.GetComponent<MeshRenderer>();
                if (mr != null) { mr.enabled = false; log.Append("cylinder hidden; "); }
            }

            Transform existing = root.transform.Find("FoodQuad");
            GameObject quad = existing != null ? existing.gameObject : new GameObject("FoodQuad");
            quad.transform.SetParent(root.transform, false);
            quad.transform.localPosition = Vector3.zero;

            var sr = quad.GetComponent<SpriteRenderer>();
            if (sr == null) sr = quad.AddComponent<SpriteRenderer>();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DefaultSpritePath);
            if (sprite != null)
            {
                sr.sprite = sprite;
                float h = sprite.bounds.size.y;
                float s = h > 0.0001f ? DesiredHeight / h : 1f;
                quad.transform.localScale = new Vector3(s, s, s);
                log.Append("sprite set, scale=" + s.ToString("0.###") + "; ");
            }
            else
            {
                quad.transform.localScale = Vector3.one * 0.25f; // fallback; FoodBillboard sets sprite at runtime
                log.Append("default sprite missing (runtime will fill); ");
            }
            sr.sortingOrder = 10;

            if (quad.GetComponent<FoodBillboard>() == null) quad.AddComponent<FoodBillboard>();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            log.Append("prefab saved.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        return log.ToString();
    }
}
