using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Visual polish pass 3: replace the bare placeholder capsule with a friendly
/// hover-bot built from primitives (coral body, glowing hover ring, eyes,
/// antenna), and add slow ambient sparkle motes around the chair. The capsule's
/// collider/agent stay untouched - this is pure visuals under a child object.
/// </summary>
public class VP_Avatar
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        var avatar = GameObject.Find("Wheelchair_Avatar");
        if (avatar == null) return "NO AVATAR";

        // --- Materials (assets, so SRP batching + persistence work) ---
        Material body = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Avatar_Mat.mat");
        Material glow = LoadOrCreateMat("Assets/Materials/Bot_Glow_Mat.mat", m =>
        {
            m.SetColor("_BaseColor", new Color(0.1f, 0.8f, 0.9f, 1f));
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", new Color(0.1f, 1.6f, 1.9f));
        });
        Material eye = LoadOrCreateMat("Assets/Materials/Bot_Eye_Mat.mat", m =>
        {
            m.SetColor("_BaseColor", new Color(0.08f, 0.09f, 0.16f, 1f));
            m.SetFloat("_Smoothness", 0.85f);
        });
        Material tip = LoadOrCreateMat("Assets/Materials/Bot_Tip_Mat.mat", m =>
        {
            m.SetColor("_BaseColor", new Color(1f, 0.85f, 0.3f, 1f));
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", new Color(1.6f, 1.2f, 0.3f));
        });

        // --- Bot visual rig ---
        Transform old = avatar.transform.Find("BotVisual");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject("BotVisual");
        root.transform.SetParent(avatar.transform, false);

        Add(root, PrimitiveType.Sphere, "Body", new Vector3(0f, 0f, 0f), new Vector3(1f, 0.85f, 1f), body);
        Add(root, PrimitiveType.Cylinder, "HoverRing", new Vector3(0f, -0.62f, 0f), new Vector3(1.25f, 0.035f, 1.25f), glow);
        Add(root, PrimitiveType.Sphere, "EyeL", new Vector3(-0.17f, 0.16f, 0.41f), new Vector3(0.17f, 0.2f, 0.1f), eye);
        Add(root, PrimitiveType.Sphere, "EyeR", new Vector3(0.17f, 0.16f, 0.41f), new Vector3(0.17f, 0.2f, 0.1f), eye);
        Add(root, PrimitiveType.Cylinder, "Antenna", new Vector3(0f, 0.62f, 0f), new Vector3(0.035f, 0.18f, 0.035f), eye);
        Add(root, PrimitiveType.Sphere, "AntennaTip", new Vector3(0f, 0.88f, 0f), new Vector3(0.14f, 0.14f, 0.14f), tip);

        // Hide the bare capsule mesh; collider + agent keep working underneath.
        var capsule = avatar.GetComponent<MeshRenderer>();
        if (capsule != null) capsule.enabled = false;
        sb.Append("bot built; ");

        // --- Soft dot texture for particles ---
        string dotPath = "Assets/Textures/SoftDot.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(dotPath) == null)
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(s / 2f, s / 2f)) / (s / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            File.WriteAllBytes(dotPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(dotPath);
        }
        var dot = AssetDatabase.LoadAssetAtPath<Texture2D>(dotPath);

        Material mote = LoadOrCreateMat("Assets/Materials/Mote_Mat.mat", m => { }, "Universal Render Pipeline/Particles/Unlit");
        mote.SetTexture("_BaseMap", dot);
        mote.SetColor("_BaseColor", new Color(1f, 0.95f, 0.75f, 0.5f));
        mote.SetOverrideTag("RenderType", "Transparent");
        mote.SetFloat("_Surface", 1f);
        mote.SetFloat("_Blend", 2f);                       // additive
        mote.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mote.SetFloat("_DstBlend", (float)BlendMode.One);
        mote.SetFloat("_ZWrite", 0f);
        mote.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mote.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(mote);

        // --- Ambient motes around the chair (slow, sparse, calm) ---
        Transform oldMotes = avatar.transform.Find("AmbientMotes");
        if (oldMotes != null) Object.DestroyImmediate(oldMotes.gameObject);
        var motesGo = new GameObject("AmbientMotes");
        motesGo.transform.SetParent(avatar.transform, false);
        motesGo.transform.localPosition = new Vector3(0f, 2f, 0f);
        var ps = motesGo.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.startColor = new Color(1f, 1f, 1f, 0.45f);
        main.maxParticles = 220;

        var emission = ps.emission;
        emission.rateOverTime = 14f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(26f, 4f, 26f);

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.25f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = grad;

        var psr = motesGo.GetComponent<ParticleSystemRenderer>();
        psr.sharedMaterial = mote;
        psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sb.Append("motes ok");

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    private static void Add(GameObject parent, PrimitiveType type, string name,
        Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private static Material LoadOrCreateMat(string path, System.Action<Material> init,
        string shaderName = "Universal Render Pipeline/Lit")
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find(shaderName));
            AssetDatabase.CreateAsset(m, path);
            init(m);
            EditorUtility.SetDirty(m);
        }
        return m;
    }
}
