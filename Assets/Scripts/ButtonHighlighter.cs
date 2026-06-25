using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Programmatic selection feedback - no Animator assets. While highlighted, the
/// button's Outline border pulses between two colors (a "glow"). The size never
/// changes: every button stays the same dimensions whether or not it is the
/// active switch-scan target, so a panel of buttons always looks uniform and the
/// highlighted one can never poke outside its container. All references cached;
/// zero per-frame allocation.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ButtonHighlighter : MonoBehaviour
{
    [Tooltip("Border pulses per second while highlighted.")]
    public float pulseHz = 1.5f;
    public Color pulseA = new Color(1f, 1f, 0f, 1f);
    public Color pulseB = Color.white;

    private Outline outline;
    private Color baseOutlineColor;
    private bool highlighted;

    void Awake()
    {
        outline = GetComponent<Outline>();
        if (outline != null) baseOutlineColor = outline.effectColor;
    }

    public void SetHighlighted(bool on)
    {
        if (highlighted == on) return;
        highlighted = on;
        if (!on && outline != null) outline.effectColor = baseOutlineColor; // restore resting border
    }

    void Update()
    {
        if (highlighted && outline != null)
        {
            float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI * pulseHz);
            outline.effectColor = Color.Lerp(pulseA, pulseB, t);
        }
    }
}
