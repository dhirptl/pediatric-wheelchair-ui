using UnityEngine;

/// <summary>
/// Reusable single-/dual-switch selection helper for accessibility input.
/// Implements two scanning strategies shared by the main menu and explorer mode:
///   - ToggleSelect: one key cycles the highlighted option, another selects it.
///   - TimeScan:     the highlight auto-advances on a timer; a single key selects
///                   whichever option is currently highlighted (one key total).
///
/// Use from a MonoBehaviour: call Reset() when the option set becomes active, then
/// call Tick(optionCount) every frame and read CurrentIndex for highlighting.
/// </summary>
[System.Serializable]
public class SwitchScanner
{
    public enum Mode { ToggleSelect, TimeScan }

    [Tooltip("ToggleSelect: one key cycles, another selects. TimeScan: auto-advancing highlight, single key selects.")]
    public Mode mode = Mode.ToggleSelect;

    [Tooltip("Key that advances the highlight (ToggleSelect only).")]
    public KeyCode toggleKey = KeyCode.Space;
    [Tooltip("Key that selects the highlighted option (both modes).")]
    public KeyCode selectKey = KeyCode.Return;

    [Tooltip("Seconds each option stays highlighted before auto-advancing (TimeScan only).")]
    public float dwellSeconds = 25f;
    [Tooltip("Minimum seconds between accepted key presses (debounce).")]
    public float inputCooldown = 0.5f;

    private int index;
    private float nextInputTime;
    private float nextAdvanceTime;
    private bool started;

    public int CurrentIndex => index;

    /// <summary>Call when the option set becomes active; resets highlight and timers.</summary>
    public void Reset()
    {
        index = 0;
        nextInputTime = Time.time + inputCooldown;
        nextAdvanceTime = Time.time + dwellSeconds;
        started = true;
    }

    /// <summary>
    /// Call every frame with the number of options. Returns the index selected this
    /// frame, or -1 if nothing was selected. Updates CurrentIndex for highlighting.
    /// </summary>
    public int Tick(int optionCount)
    {
        if (optionCount <= 0) return -1;
        if (!started) Reset();
        if (index >= optionCount) index = 0;

        if (mode == Mode.TimeScan)
        {
            if (Time.time >= nextAdvanceTime)
            {
                index = (index + 1) % optionCount;
                nextAdvanceTime = Time.time + dwellSeconds;
            }
            if (Time.time >= nextInputTime && Input.GetKeyDown(selectKey))
            {
                nextInputTime = Time.time + inputCooldown;
                nextAdvanceTime = Time.time + dwellSeconds; // give the next dwell a full window
                return index;
            }
        }
        else // ToggleSelect
        {
            if (Time.time < nextInputTime) return -1;
            if (Input.GetKeyDown(toggleKey))
            {
                index = (index + 1) % optionCount;
                nextInputTime = Time.time + inputCooldown;
            }
            else if (Input.GetKeyDown(selectKey))
            {
                nextInputTime = Time.time + inputCooldown;
                return index;
            }
        }
        return -1;
    }
}
