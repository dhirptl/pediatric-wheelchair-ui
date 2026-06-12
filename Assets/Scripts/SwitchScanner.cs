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

    [Tooltip("Seconds each option stays highlighted before auto-advancing (TimeScan only). Tune per child.")]
    public float dwellSeconds = 8f;
    [Tooltip("Minimum seconds between accepted key presses (debounce).")]
    public float inputCooldown = 0.5f;
    [Tooltip("Remember a key pressed during the cooldown and apply it the moment the window reopens, so no press is ever silently lost.")]
    public bool bufferDuringCooldown = true;

    private int index;
    private float nextInputTime;
    private float nextAdvanceTime;
    private bool started;
    private SwitchKey bufferedKey;

    private enum SwitchKey { None, Toggle, Select }

    // Two-stage grid scanning state (TickGrid only).
    private int gridRow;
    private int gridCol;
    private bool inColumnStage;

    public int CurrentIndex => index;
    public int CurrentRow => gridRow;
    public int CurrentCol => gridCol;
    public bool InColumnStage => inColumnStage;

    /// <summary>Call when the option set becomes active; resets highlight and timers.</summary>
    public void Reset()
    {
        index = 0;
        gridRow = 0;
        gridCol = 0;
        inColumnStage = false;
        bufferedKey = SwitchKey.None;
        nextInputTime = Time.time + inputCooldown;
        nextAdvanceTime = Time.time + dwellSeconds;
        started = true;
    }

    /// <summary>
    /// Reads the switch keys through the debounce window. A press that lands
    /// during the cooldown is latched (first press wins) and delivered the moment
    /// the window reopens, instead of being silently dropped - a swallowed press
    /// reads as "my button is broken" to a child. Re-arms the cooldown on delivery.
    /// </summary>
    private SwitchKey ReadKey(bool allowToggle)
    {
        bool toggleDown = allowToggle && Input.GetKeyDown(toggleKey);
        bool selectDown = Input.GetKeyDown(selectKey);

        if (Time.time < nextInputTime)
        {
            if (bufferDuringCooldown && bufferedKey == SwitchKey.None)
            {
                if (toggleDown) bufferedKey = SwitchKey.Toggle;
                else if (selectDown) bufferedKey = SwitchKey.Select;
            }
            return SwitchKey.None;
        }

        if (bufferedKey != SwitchKey.None)
        {
            SwitchKey key = bufferedKey;
            bufferedKey = SwitchKey.None;
            nextInputTime = Time.time + inputCooldown;
            return key;     // same-frame fresh presses fall into the new cooldown
        }

        if (toggleDown || selectDown)
        {
            nextInputTime = Time.time + inputCooldown;
            return toggleDown ? SwitchKey.Toggle : SwitchKey.Select;
        }
        return SwitchKey.None;
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
            if (ReadKey(allowToggle: false) == SwitchKey.Select)
            {
                nextAdvanceTime = Time.time + dwellSeconds; // give the next dwell a full window
                return index;
            }
        }
        else // ToggleSelect
        {
            SwitchKey key = ReadKey(allowToggle: true);
            if (key == SwitchKey.Toggle)
                index = (index + 1) % optionCount;
            else if (key == SwitchKey.Select)
                return index;
        }
        return -1;
    }

    /// <summary>
    /// Two-stage row/column scanning over a row-major grid (the standard
    /// switch-access pattern): first the highlight walks whole rows; selecting a
    /// row drops into it, then the highlight walks its cells; selecting a cell
    /// returns its flat index. Works in both modes - ToggleSelect advances with
    /// the toggle key, TimeScan auto-advances every dwellSeconds.
    /// Returns the selected flat index this frame, or -1.
    /// </summary>
    public int TickGrid(int rows, int cols, int optionCount)
    {
        if (rows <= 0 || cols <= 0 || optionCount <= 0) return -1;
        if (!started) Reset();
        if (gridRow >= rows) { gridRow = 0; inColumnStage = false; }
        int colsInRow = Mathf.Min(cols, optionCount - gridRow * cols);
        if (gridCol >= colsInRow) gridCol = 0;

        bool advance = false, commit = false;
        if (mode == Mode.TimeScan && Time.time >= nextAdvanceTime)
        {
            advance = true;
            nextAdvanceTime = Time.time + dwellSeconds;
        }
        SwitchKey key = ReadKey(allowToggle: mode == Mode.ToggleSelect);
        if (key == SwitchKey.Toggle)
        {
            advance = true;
        }
        else if (key == SwitchKey.Select)
        {
            commit = true;
            nextAdvanceTime = Time.time + dwellSeconds;
        }

        if (commit)
        {
            if (!inColumnStage)
            {
                inColumnStage = true;   // enter the highlighted row
                gridCol = 0;
                return -1;
            }
            inColumnStage = false;      // select the highlighted cell
            int flat = Mathf.Min(gridRow * cols + gridCol, optionCount - 1);
            gridRow = 0;
            gridCol = 0;
            return flat;
        }

        if (advance)
        {
            if (inColumnStage) gridCol = (gridCol + 1) % colsInRow;
            else gridRow = (gridRow + 1) % rows;
        }
        return -1;
    }
}
