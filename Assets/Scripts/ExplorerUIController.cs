using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Explorer Mode controller. Discrete actions (move forward 3 m, turn 45 deg) that
/// are issued exclusively through the WheelchairStateBridge - forward becomes a
/// relative navigation goal, turns become time-sliced cmd_vel-style angular
/// velocity. This component never touches the NavMeshAgent directly, so the ROS 2
/// swap later this summer only flips the bridge's mode.
///
/// Three input strategies (BCI / switch-access friendly):
///   - DirectTwoKey: one key = forward, one key = turn-right (immediate).
///   - ToggleSelect: one key cycles the highlighted action, one key executes it.
///   - TimeScan:     the highlight auto-advances on a timer; a single key executes it.
/// On-screen buttons remain and call ExecuteForward / ExecuteTurnRight too.
/// </summary>
public class ExplorerUIController : MonoBehaviour
{
    public enum ExplorerStrategy { DirectTwoKey, ToggleSelect, TimeScan }

    [Header("Strategy")]
    public ExplorerStrategy strategy = ExplorerStrategy.DirectTwoKey;

    [Header("Direct two-key strategy")]
    public KeyCode forwardKey = KeyCode.UpArrow;
    public KeyCode turnRightKey = KeyCode.RightArrow;

    [Header("Toggle/Time scan strategies")]
    public SwitchScanner scanner = new SwitchScanner();

    [Header("Target")]
    [Tooltip("The wheelchair's state bridge. Auto-found by avatar name if left empty.")]
    public WheelchairStateBridge bridge;
    public string avatarName = "Wheelchair_Avatar";

    [Header("Motion")]
    [Tooltip("Distance per forward command, in meters.")]
    public float forwardDistance = 3f;
    [Tooltip("Degrees rotated per turn command (clockwise for turn-right).")]
    public float turnAngle = 45f;
    [Tooltip("Seconds the turn animation takes.")]
    public float turnDuration = 0.25f;

    [Header("Highlight (scan strategies)")]
    public Button forwardButton;
    public Button turnRightButton;
    [Tooltip("Scale applied to the currently highlighted button.")]
    public float highlightScale = 1.12f;

    private Button[] options;
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

        // Option order: 0 = Forward, 1 = Turn-Right.
        options = new Button[] { forwardButton, turnRightButton };
        scanner.Reset();
        UpdateHighlight(strategy == ExplorerStrategy.DirectTwoKey ? -1 : scanner.CurrentIndex);
    }

    void Update()
    {
        if (strategy == ExplorerStrategy.DirectTwoKey)
        {
            if (Input.GetKeyDown(forwardKey)) ExecuteForward();
            if (Input.GetKeyDown(turnRightKey)) ExecuteTurnRight();
            return;
        }

        // ToggleSelect / TimeScan
        scanner.mode = (strategy == ExplorerStrategy.TimeScan)
            ? SwitchScanner.Mode.TimeScan
            : SwitchScanner.Mode.ToggleSelect;

        int selected = scanner.Tick(2);
        UpdateHighlight(scanner.CurrentIndex);
        if (selected == 0) ExecuteForward();
        else if (selected == 1) ExecuteTurnRight();
    }

    // --- Actions (route through the bridge -> Nav2 goal / cmd_vel later) ---

    public void OnMoveForwardClicked() { ExecuteForward(); }   // kept for button onClick
    public void OnTurnRightClicked()   { ExecuteTurnRight(); }

    public void ExecuteForward()
    {
        if (bridge == null || isTurning) return;
        Transform t = bridge.transform;
        bridge.SendNavigationGoal(t.position + t.forward * forwardDistance);
    }

    public void ExecuteTurnRight()
    {
        ExecuteTurn(turnAngle);
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

    // --- Helpers ---

    void UpdateHighlight(int activeIndex)
    {
        if (options == null) return;
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] == null) continue;
            float s = (i == activeIndex) ? highlightScale : 1f;
            options[i].transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
