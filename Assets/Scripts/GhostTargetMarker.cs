using UnityEngine;

/// <summary>
/// A flat glowing disc shown on the floor where the wheelchair is about to go,
/// so the child sees the destination BEFORE any motion starts. Gently pulses
/// while visible (slow scale breathe, well under the 3 Hz photosensitivity
/// limit). Pure visualization - never touches the agent or the bridge.
///
/// If nothing is wired in the scene, CreateDefault() builds a simple unlit
/// yellow disc at runtime so the preview always works.
/// </summary>
public class GhostTargetMarker : MonoBehaviour
{
    [Tooltip("Peak scale multiplier of the breathe pulse (1.15 = +15%).")]
    public float pulseScale = 1.15f;
    [Tooltip("Breathe cycles per second. Keep well below 3 Hz.")]
    public float pulseHz = 1.2f;
    [Tooltip("Lift above the floor so the disc never z-fights.")]
    public float yOffset = 0.05f;

    private Vector3 baseScale;
    private bool shown;

    void Awake()
    {
        baseScale = transform.localScale;
        gameObject.SetActive(false);
    }

    /// <summary>Place the marker at the candidate goal and start pulsing.</summary>
    public void Show(Vector3 worldPos)
    {
        transform.position = worldPos + Vector3.up * yOffset;
        transform.localScale = baseScale;
        shown = true;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        shown = false;
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (!shown) return;
        float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI * pulseHz);
        transform.localScale = baseScale * Mathf.Lerp(1f, pulseScale, t);
    }

    /// <summary>
    /// Builds a fallback marker (flattened cylinder, unlit yellow) so no prefab
    /// or scene wiring is required. A nicer authored disc can replace it via the
    /// inspector field later.
    /// </summary>
    public static GhostTargetMarker CreateDefault()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "GhostTargetMarker";
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.localScale = new Vector3(1.2f, 0.02f, 1.2f);

        Renderer r = go.GetComponent<Renderer>();
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null) unlit = Shader.Find("Unlit/Color");
        if (r != null && unlit != null)
        {
            // [MainColor] on both shaders maps Material.color to the right property.
            // HDR-bright yellow so the post-processing bloom gives it a soft glow.
            r.sharedMaterial = new Material(unlit) { color = new Color(2.2f, 2f, 0.25f, 1f) };
        }
        return go.AddComponent<GhostTargetMarker>();
    }
}
