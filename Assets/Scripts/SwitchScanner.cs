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
    public float dwellSeconds = 20f;
    [Tooltip("Minimum seconds between accepted key presses (debounce).")]
    public float inputCooldown = 0.18f;

    private int index;
    private float nextInputTime;
    private float nextAdvanceTime;
    private bool started;

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
                // Space moves the highlight down (wraps to top); Shift+Space moves up (wraps to bottom).
                index = ReverseHeld()
                    ? (index - 1 + optionCount) % optionCount
                    : (index + 1) % optionCount;
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
        if (mode == Mode.TimeScan)
        {
            if (Time.time >= nextAdvanceTime)
            {
                advance = true;
                nextAdvanceTime = Time.time + dwellSeconds;
            }
            if (Time.time >= nextInputTime && Input.GetKeyDown(selectKey))
            {
                commit = true;
                ArmCooldowns();
            }
        }
        else // ToggleSelect
        {
            if (Time.time >= nextInputTime)
            {
                if (Input.GetKeyDown(toggleKey))
                {
                    advance = true;
                    nextInputTime = Time.time + inputCooldown;
                }
                else if (Input.GetKeyDown(selectKey))
                {
                    commit = true;
                    ArmCooldowns();
                }
            }
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
            // Space advances forward; Shift+Space steps backward through the row/column.
            int step = ReverseHeld() ? -1 : 1;
            if (inColumnStage) gridCol = (gridCol + step + colsInRow) % colsInRow;
            else gridRow = (gridRow + step + rows) % rows;
        }
        return -1;
    }

    /// <summary>True while Shift is held, used to reverse the scan direction.</summary>
    private static bool ReverseHeld()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    private void ArmCooldowns()
    {
        nextInputTime = Time.time + inputCooldown;
        nextAdvanceTime = Time.time + dwellSeconds;
    }
}
