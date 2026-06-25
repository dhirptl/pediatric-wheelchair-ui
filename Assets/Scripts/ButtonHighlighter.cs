using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Programmatic selection feedback - no Animator assets. While highlighted (the
/// active switch-scan target) the button smoothly scales UP and its Outline border
/// pulses between two colors, so a two-switch user can clearly see which option
/// Enter will activate. If a ButtonJuice sits on the same object its transient
/// punch is folded into the scale here so the two effects compose. All references
/// cached; zero per-frame allocation.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ButtonHighlighter : MonoBehaviour
{
    [Tooltip("Scale multiplier while highlighted (the obvious 'this is selected' enlargement).")]
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
