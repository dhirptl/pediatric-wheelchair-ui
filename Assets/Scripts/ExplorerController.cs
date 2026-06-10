using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Explorer Mode controller for the single central "Active Command" panel.
///
/// Two input pathways run side by side with no settings swap:
///   1. BCI 2-key loop  - the Toggle key (Space) cycles the panel through
///                        [MOVE FORWARD] -> [TURN RIGHT] -> [TURN LEFT] -> [SETTINGS];
///                        the Select key (Enter) executes the shown command.
///   2. Direct keys     - W/Up, A/Left, D/Right, S/Down immediately execute the
///                        matching command and bypass the toggle cycle. They
///                        trigger the *identical* panel animation (5% pop + color
///                        flash + sound), so feedback is the same either way.
///
/// All motion routes through WheelchairStateBridge - Forward is a relative
/// navigation goal, turns are time-sliced cmd_vel-style angular velocity - so the
/// ROS 2 swap later only flips the bridge's mode.
/// </summary>
public class ExplorerController : MonoBehaviour
{
    public enum Command { Forward, TurnRight, TurnLeft, Settings, Stop }

    [Header("Scanning (keys, dwell, cooldown)")]
    [Tooltip("Toggle = Space cycles the panel, Select = Enter executes. Debounced.")]
    public SwitchScanner scanner = new SwitchScanner();

    [Header("Active Command panel")]
    public TextMeshProUGUI commandLabel;
    public TextMeshProUGUI commandGlyph;
    [Tooltip("Pop/flash feedback on the panel (5% scale + color pulse + sound).")]
    public ButtonJuice panelJuice;

    [Header("Target")]
    [Tooltip("The wheelchair's state bridge. Auto-found by avatar name if left empty.")]
    public WheelchairStateBridge bridge;
    public string avatarName = "Wheelchair_Avatar";

    [Header("Motion")]
    [Tooltip("Distance per forward command, in meters.")]
    public float forwardDistance = 3f;
    [Tooltip("Degrees rotated per turn command.")]
    public float turnAngle = 45f;
    [Tooltip("Seconds the turn animation takes.")]
    public float turnDuration = 0.25f;

    // Only the first four are in the toggle cycle; Stop is direct-key only.
    private const int CycleCount = 4;
    private static readonly string[] labels = { "MOVE FORWARD", "TURN RIGHT", "TURN LEFT", "SETTINGS", "STOP" };
    private static readonly string[] glyphs = { "▲", "▶", "◀", "●", "■" };

    private int displayIndex;
    private int scannerLast = -1;
    private bool isTurning;

    void Start()
    {
        if (bridge == null)
        {
            var avatar = GameObject.Find(avatarName);
            if (avatar != null) bridge = avatar.GetComponent<WheelchairStateBridge>();
        }
        if (bridge == null)
            Debug.LogWarning("[ExplorerController] No WheelchairStateBridge found; controls will do nothing.");
    }

    void OnEnable()
    {
        scanner.Reset();
        scannerLast = scanner.CurrentIndex;
        displayIndex = scanner.CurrentIndex;
        RefreshDisplay();
    }

    void Update()
    {
        // Hand the two keys to any open overlay (pause / destination / shop).
        if (!ScanFocus.IsEmpty) return;

        // --- Direct keys: execute immediately, same panel feedback as the loop. ---
        if (Pressed(KeyCode.W, KeyCode.UpArrow))    { Trigger(Command.Forward);  return; }
        if (Pressed(KeyCode.D, KeyCode.RightArrow)) { Trigger(Command.TurnRight); return; }
        if (Pressed(KeyCode.A, KeyCode.LeftArrow))  { Trigger(Command.TurnLeft);  return; }
        if (Pressed(KeyCode.S, KeyCode.DownArrow))  { Trigger(Command.Stop);      return; }

        // --- 2-key loop: Space cycles the panel, Enter executes the shown command. ---
        int selected = scanner.Tick(CycleCount);
        if (scanner.CurrentIndex != scannerLast)
        {
            scannerLast = scanner.CurrentIndex;
            Show((Command)scanner.CurrentIndex);    // pop on each toggle
        }
        if (selected >= 0) Execute((Command)selected);
    }

    private static bool Pressed(KeyCode a, KeyCode b)
    {
        return Input.GetKeyDown(a) || Input.GetKeyDown(b);
    }

    /// <summary>Direct-key path: show the command on the panel, pop, execute.</summary>
    private void Trigger(Command command)
    {
        Show(command);
        Execute(command);
    }

    /// <summary>Drive the panel to a command and fire the pop/flash feedback.</summary>
    private void Show(Command command)
    {
        displayIndex = (int)command;
        RefreshDisplay();
        if (panelJuice != null) panelJuice.Pop();
    }

    private void RefreshDisplay()
    {
        if (commandLabel != null) commandLabel.text = labels[displayIndex];
        if (commandGlyph != null) commandGlyph.text = glyphs[displayIndex];
    }

    // --- Actions (route through the bridge -> Nav2 goal / cmd_vel later) ---

    private void Execute(Command command)
    {
        switch (command)
        {
            case Command.Forward:
                if (bridge == null || isTurning) return;
                Transform t = bridge.transform;
                bridge.SendNavigationGoal(t.position + t.forward * forwardDistance);
                break;
            case Command.TurnRight:
                ExecuteTurn(turnAngle);
                break;
            case Command.TurnLeft:
                ExecuteTurn(-turnAngle);
                break;
            case Command.Settings:
                if (GameModeManager.Instance != null) GameModeManager.Instance.OpenPause();
                break;
            case Command.Stop:
                if (bridge != null) bridge.StopMotion();
                break;
        }
    }

    private void ExecuteTurn(float degrees)
    {
        if (bridge == null || isTurning) return;
        bridge.StopMotion();            // stop any forward motion before reorienting
        StartCoroutine(TurnRoutine(degrees));
    }

    IEnumerator TurnRoutine(float degrees)
    {
        isTurning = true;
        float remaining = Mathf.Abs(degrees);
        float sign = Mathf.Sign(degrees); // +Y = clockwise from above
        float speed = Mathf.Abs(degrees) / Mathf.Max(0.01f, turnDuration);
        while (remaining > 0f)
        {
            float step = Mathf.Min(speed * Time.deltaTime, remaining);
            // The bridge integrates velocity over deltaTime, so feed it the rate
            // that produces exactly `step` degrees this frame (lands on 45 exactly).
            bridge.SendLowLevelVelocity(0f, sign * step / Mathf.Max(Time.deltaTime, 1e-5f));
            remaining -= step;
            yield return null;
        }
        isTurning = false;
    }
}
