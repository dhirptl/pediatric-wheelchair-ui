using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Explorer Mode controller. Discrete actions (move forward, turn left/right,
/// stop) issued exclusively through the WheelchairStateBridge - forward becomes
/// a relative navigation goal, turns become time-sliced cmd_vel-style angular
/// velocity - so the ROS 2 swap later only flips the bridge's mode.
///
/// Three accessibility selection strategies (BCI / switch-access friendly):
///   1. DirectKeys:        Space cycles the dashboard buttons, Enter executes.
///   2. GridToggleSelect:  two-stage row/column scanning over a simplified grid
///                         menu - Space cycles rows, Enter enters the row, Space
///                         cycles its cells, Enter executes.
///   3. TimeScan:          the dashboard highlight auto-advances every 20 s
///                         (Forward first, then Turn Right); one designated key
///                         executes whatever is highlighted.
/// On-screen buttons stay tappable in every strategy.
/// </summary>
public class ExplorerUIController : MonoBehaviour
{
    public enum ExplorerStrategy { DirectKeys, GridToggleSelect, TimeScan }

    [Header("Strategy")]
    public ExplorerStrategy strategy = ExplorerStrategy.DirectKeys;

    [Header("Scanning (keys, dwell, cooldown)")]
    public ScanGroup scanGroup = new ScanGroup();

    [Header("Panels")]
    public GameObject dashboardPanel;
    public GameObject gridMenuPanel;

    [Header("Dashboard buttons")]
    public Button forwardButton;
    public Button turnRightButton;
    public Button turnLeftButton;

    [Header("Grid menu buttons (row-major; order must match gridActions)")]
    public Button[] gridButtons;
    public int gridColumns = 2;

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

    private enum Command { Forward, TurnRight, TurnLeft, Stop }

    // Scan order per spec: TimeScan highlights Forward first, then Turn Right.
    private static readonly Command[] dashboardActions = { Command.Forward, Command.TurnRight, Command.TurnLeft };
    private static readonly Command[] gridActions = { Command.Forward, Command.TurnLeft, Command.TurnRight, Command.Stop };

    private Command[] currentActions;
    private bool isTurning;

    void Start()
    {
        if (bridge == null)
        {
            var avatar = GameObject.Find(avatarName);
            if (avatar != null) bridge = avatar.GetComponent<WheelchairStateBridge>();
        }
        if (bridge == null)
            Debug.LogWarning("[ExplorerUIController] No WheelchairStateBridge found; controls will do nothing.");

        scanGroup.OnOptionSelected += HandleSelected;
        ApplyStrategy();
    }

    /// <summary>Switches strategy at runtime (wired to future settings UI; index = enum order).</summary>
    public void SetStrategy(int strategyIndex)
    {
        strategy = (ExplorerStrategy)strategyIndex;
        ApplyStrategy();
    }

    private void ApplyStrategy()
    {
        bool grid = strategy == ExplorerStrategy.GridToggleSelect;
        if (dashboardPanel != null) dashboardPanel.SetActive(!grid);
        if (gridMenuPanel != null) gridMenuPanel.SetActive(grid);

        if (grid)
        {
            scanGroup.options = gridButtons;
            scanGroup.gridCols = gridColumns;
            scanGroup.scanner.mode = SwitchScanner.Mode.ToggleSelect;
            currentActions = gridActions;
        }
        else
        {
            scanGroup.options = new[] { forwardButton, turnRightButton, turnLeftButton };
            scanGroup.gridCols = 0;
            scanGroup.scanner.mode = strategy == ExplorerStrategy.TimeScan
                ? SwitchScanner.Mode.TimeScan
                : SwitchScanner.Mode.ToggleSelect;
            currentActions = dashboardActions;
        }
        scanGroup.Activate();
    }

    void Update()
    {
        scanGroup.Tick();
    }

    private void HandleSelected(int index)
    {
        if (currentActions == null || index >= currentActions.Length) return;
        Execute(currentActions[index]);
    }

    // --- Button onClick entry points (touch input path) ---

    public void OnMoveForwardClicked() { Execute(Command.Forward); }
    public void OnTurnRightClicked()   { Execute(Command.TurnRight); }
    public void OnTurnLeftClicked()    { Execute(Command.TurnLeft); }
    public void OnStopClicked()        { Execute(Command.Stop); }

    // --- Actions (route through the bridge -> Nav2 goal / cmd_vel later) ---

    private void Execute(Command command)
    {
        if (bridge == null) return;
        switch (command)
        {
            case Command.Forward:
                if (isTurning) return;
                Transform t = bridge.transform;
                bridge.SendNavigationGoal(t.position + t.forward * forwardDistance);
                break;
            case Command.TurnRight:
                ExecuteTurn(turnAngle);
                break;
            case Command.TurnLeft:
                ExecuteTurn(-turnAngle);
                break;
            case Command.Stop:
                bridge.StopMotion();
                break;
        }
    }

    private void ExecuteTurn(float degrees)
    {
        if (isTurning) return;
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
