using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Explorer Mode controller. Two actions only: move forward 3 m, and turn right 45 deg
/// (clockwise). The tablet is a frontend client: these map to the future cmd_vel publish
/// points (ExecuteForward / ExecuteTurnRight). For now they drive the avatar locally so
/// the behaviour is testable without ROS.
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
    [Tooltip("The wheelchair's NavMeshAgent. Auto-found by name if left empty.")]
    public NavMeshAgent agent;
    public string avatarName = "Wheelchair_Avatar";

    [Header("Motion")]
    [Tooltip("Distance per forward command, in meters.")]
    public float forwardDistance = 3f;
    [Tooltip("Degrees rotated clockwise per turn-right command.")]
    public float turnAngle = 45f;
    [Tooltip("Seconds the turn animation takes.")]
    public float turnDuration = 0.25f;
    [Tooltip("Search radius when snapping a step/warp onto the NavMesh.")]
    public float navSampleRadius = 8f;

    [Header("Highlight (scan strategies)")]
    public Button forwardButton;
    public Button turnRightButton;
    [Tooltip("Scale applied to the currently highlighted button.")]
    public float highlightScale = 1.12f;

    private Button[] options;
    private bool isTurning;

    void Start()
    {
        if (agent == null)
        {
            var avatar = GameObject.Find(avatarName);
            if (avatar != null) agent = avatar.GetComponent<NavMeshAgent>();
        }
        if (agent == null)
            Debug.LogWarning("[ExplorerUIController] No NavMeshAgent found; controls will do nothing.");
        else
            EnsureOnNavMesh();

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

    // --- Actions (future cmd_vel publish points) ---

    public void OnMoveForwardClicked() { ExecuteForward(); }   // kept for button onClick
    public void OnTurnRightClicked()   { ExecuteTurnRight(); }

    public void ExecuteForward()
    {
        if (!IsReady()) return;
        Vector3 target = agent.transform.position + agent.transform.forward * forwardDistance;
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    public void ExecuteTurnRight()
    {
        if (agent == null || isTurning) return;
        agent.ResetPath();              // stop any forward motion before reorienting
        StartCoroutine(TurnRoutine(turnAngle));
    }

    IEnumerator TurnRoutine(float degrees)
    {
        isTurning = true;
        Transform t = agent.transform;
        Quaternion from = t.rotation;
        Quaternion to = from * Quaternion.Euler(0f, degrees, 0f); // +Y = clockwise from above
        float elapsed = 0f;
        while (elapsed < turnDuration)
        {
            elapsed += Time.deltaTime;
            t.rotation = Quaternion.Slerp(from, to, Mathf.Clamp01(elapsed / turnDuration));
            yield return null;
        }
        t.rotation = to;
        isTurning = false;
    }

    // --- Helpers ---

    void EnsureOnNavMesh()
    {
        if (agent == null || agent.isOnNavMesh) return;
        if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
            agent.Warp(hit.position);
    }

    bool IsReady()
    {
        if (agent == null) return false;
        EnsureOnNavMesh();
        return agent.isOnNavMesh;
    }

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
