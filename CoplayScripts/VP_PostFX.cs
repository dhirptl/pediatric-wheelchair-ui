using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Visual polish pass 1: post-processing (bloom/vignette/grade), lighting,
/// fog + ambient, and the smooth-follow camera rig swap.
/// </summary>
public class VP_PostFX
{
    public static string Execute()
    {
        var sb = new StringBuilder();

        // --- 1. Volume profile asset with bloom + vignette + gentle grade ---
        if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            AssetDatabase.CreateFolder("Assets", "Settings");

        string profilePath = "Assets/Settings/GamePostFX.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }

        var bloom = GetOrAdd<Bloom>(profile);
        bloom.intensity.Override(0.8f);
        bloom.threshold.Override(0.95f);
        bloom.scatter.Override(0.65f);

        var vignette = GetOrAdd<Vignette>(profile);
        vignette.intensity.Override(0.2f);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(new Color(0.05f, 0.03f, 0.12f));

        var grade = GetOrAdd<ColorAdjustments>(profile);
        grade.saturation.Override(12f);
        grade.postExposure.Override(0.05f);

        EditorUtility.SetDirty(profile);
        sb.Append("profile ok; ");

        // --- 2. Global volume in the scene ---
        var volGo = GameObject.Find("GlobalPostFX");
        if (volGo == null) volGo = new GameObject("GlobalPostFX");
        var vol = volGo.GetComponent<Volume>();
        if (vol == null) vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = profile;
        sb.Append("volume ok; ");

        // --- 3. Camera: enable postFX + swap rigid parenting for SmoothCameraFollow ---
        var cam = Camera.main;
        if (cam != null)
        {
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }

            if (cam.transform.parent != null && cam.transform.parent.name == "Wheelchair_Avatar")
            {
                Vector3 localOffset = cam.transform.localPosition;   // (0, 9, -7)
                cam.transform.SetParent(null, true);
                var follow = cam.GetComponent<SmoothCameraFollow>();
                if (follow == null) follow = cam.gameObject.AddComponent<SmoothCameraFollow>();
                follow.target = GameObject.Find("Wheelchair_Avatar").transform;
                follow.localOffset = localOffset;
                sb.Append("camera reparented+follow; ");
            }
            else sb.Append("camera already free; ");
            EditorUtility.SetDirty(cam.gameObject);
        }
        else sb.Append("NO CAMERA; ");

        // --- 4. Lighting: warm key light, colored trilight ambient, soft depth fog ---
        var sun = RenderSettings.sun;
        if (sun == null)
        {
            var sunGo = GameObject.Find("Directional Light");
            if (sunGo != null) sun = sunGo.GetComponent<Light>();
        }
        if (sun != null)
        {
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
            EditorUtility.SetDirty(sun.gameObject);
            sb.Append("sun ok; ");
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.95f);
        RenderSettings.ambientEquatorColor = new Color(0.45f, 0.42f, 0.65f);
        RenderSettings.ambientGroundColor = new Color(0.16f, 0.12f, 0.28f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.55f, 0.62f, 0.88f);
        RenderSettings.fogDensity = 0.0065f;
        sb.Append("ambient+fog ok");

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T comp)) return comp;
        return profile.Add<T>(true);
    }
}
