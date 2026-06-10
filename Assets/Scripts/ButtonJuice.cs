using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Pop" feedback for the rounded Figma buttons and the Active Command panel.
/// On Pop() the target gently punches up by a few percent and eases back, an
/// optional Graphic flashes a highlight color, and a soft sound plays once.
///
/// Composes with ButtonHighlighter rather than fighting it: ButtonHighlighter
/// reads CurrentPunch each frame and multiplies it into its own scale lerp, so
/// a highlighted button still pops. On the Active Command panel (which has no
/// ButtonHighlighter) Pop() drives the scale itself.
/// </summary>
public class ButtonJuice : MonoBehaviour
{
    [Tooltip("Peak scale multiplier on a pop (1.05 = +5%).")]
    public float punchScale = 1.05f;
    [Tooltip("Seconds for the punch to rise and settle back.")]
    public float punchTime = 0.15f;

    [Header("Color pulse (optional)")]
    [Tooltip("Graphic to flash on a pop (e.g. the panel background). Optional.")]
    public Graphic flashTarget;
    public Color flashColor = new Color(1f, 0.95f, 0.4f, 1f);

    [Header("Sound")]
    public AudioClip popClip;
    [Range(0f, 1f)] public float volume = 0.6f;

    private AudioSource source;
    private Vector3 baseScale;
    private Color baseFlashColor;
    private float punch = 1f;          // current extra multiplier, decays to 1
    private bool driveScale;           // true when no ButtonHighlighter is present
    private Coroutine routine;

    /// <summary>Extra scale multiplier ButtonHighlighter folds into its lerp.</summary>
    public float CurrentPunch => punch;

    void Awake()
    {
        baseScale = transform.localScale;
        if (flashTarget != null) baseFlashColor = flashTarget.color;

        source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;       // 2D UI sound

        // If a highlighter is present it owns the scale; otherwise we drive it.
        driveScale = GetComponent<ButtonHighlighter>() == null;
    }

    /// <summary>Trigger the pop: punch scale, flash color, play the soft sound.</summary>
    public void Pop()
    {
        if (popClip != null) source.PlayOneShot(popClip, volume);
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(PunchRoutine());
    }

    private IEnumerator PunchRoutine()
    {
        float t = 0f;
        while (t < punchTime)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / punchTime);
            // Rise fast, settle back: 0 -> 1 -> 0 over the window.
            float curve = Mathf.Sin(p * Mathf.PI);
            punch = 1f + (punchScale - 1f) * curve;

            if (driveScale) transform.localScale = baseScale * punch;
            if (flashTarget != null) flashTarget.color = Color.Lerp(baseFlashColor, flashColor, curve);
            yield return null;
        }
        punch = 1f;
        if (driveScale) transform.localScale = baseScale;
        if (flashTarget != null) flashTarget.color = baseFlashColor;
        routine = null;
    }
}
