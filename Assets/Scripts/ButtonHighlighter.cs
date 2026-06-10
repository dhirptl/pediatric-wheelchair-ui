using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Programmatic selection feedback - no Animator assets. While highlighted, the
/// button smoothly scales up and its Outline border pulses between two colors.
/// If a ButtonJuice sits on the same object it pops on highlight (5% punch +
/// soft sound) and its transient punch is folded into the scale here, so the
/// two effects compose instead of fighting. All references cached; zero
/// per-frame allocation.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ButtonHighlighter : MonoBehaviour
{
    [Tooltip("Scale multiplier while highlighted.")]
    public float targetScale = 1.10f;
    [Tooltip("How quickly the scale eases toward its target.")]
    public float lerpSpeed = 10f;
    [Tooltip("Border pulses per second while highlighted.")]
    public float pulseHz = 1.5f;
    public Color pulseA = new Color(1f, 1f, 0f, 1f);
    public Color pulseB = Color.white;

    private Outline outline;
    private ButtonJuice juice;
    private Vector3 baseScale;
    private Color baseOutlineColor;
    private bool highlighted;

    void Awake()
    {
        outline = GetComponent<Outline>();
        juice = GetComponent<ButtonJuice>();
        baseScale = transform.localScale;
        if (outline != null) baseOutlineColor = outline.effectColor;
    }

    public void SetHighlighted(bool on)
    {
        if (highlighted == on) return;
        highlighted = on;
        if (!on && outline != null) outline.effectColor = baseOutlineColor;
        if (on && juice != null) juice.Pop();   // soft pop the moment it becomes highlighted
    }

    void Update()
    {
        float target = highlighted ? targetScale : 1f;
        float punch = juice != null ? juice.CurrentPunch : 1f;
        transform.localScale = Vector3.Lerp(
            transform.localScale, baseScale * target * punch, Time.unscaledDeltaTime * lerpSpeed);

        if (highlighted && outline != null)
        {
            float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI * pulseHz);
            outline.effectColor = Color.Lerp(pulseA, pulseB, t);
        }
    }
}
