using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Explorer Mode controller for the single central "Active Command" panel,
/// built around an explicit drive-state machine so a 2-switch child always
/// knows - and controls - what the chair is doing:
///
///   Scanning - Space cycles [MOVE FORWARD] -> [TURN RIGHT] -> [TURN LEFT] ->
///              [MENU]; Enter executes. Direct keys (W/A/S/D, arrows) execute
///              immediately with the identical panel feedback.
///   WindUp   - forward commands preview first: the panel counts down READY...
///              over windUpSeconds while a ghost disc and the neon path line
///              mark exactly where the chair will go. Any switch press cancels.
///   Moving   - the panel becomes a big red STOP button. ANY switch press
///              (either switch or any direct key) halts the chair immediately,
///              bypassing the scan cooldown - a child must never be more than
///              one press away from stillness.
///   Turning  - slow eased rotation; any switch press cancels mid-turn.
///
/// Commands are never silently dropped: in every non-Scanning state a press
/// means STOP/CANCEL, and while Scanning the SwitchScanner buffers presses that
/// land inside its debounce window. All motion routes through
/// WheelchairStateBridge - Forward is a relative navigation goal, turns are
/// time-sliced cmd_vel-style angular velocity - so the ROS 2 swap later only
/// flips the bridge's mode.
/// </summary>
public class ExplorerController : MonoBehaviour
{
    public enum Command { Forward, TurnRight, TurnLeft, Settings, Stop }
    public enum DriveState { Scanning, WindUp, Moving, Turning }

    [Header("Scanning (keys, dwell, cooldown)")]
    [Tooltip("Toggle = Space cycles the panel, Select = Enter executes. Debounced, presses buffered.")]
    public SwitchScanner scanner = new SwitchScanner();

    [Header("Active Command panel")]
    public TextMeshProUGUI commandLabel;
    public TextMeshProUGUI commandGlyph;
    [Tooltip("Pop/flash feedback on the panel (5% scale + color pulse + sound).")]
    public ButtonJuice panelJuice;
    [Tooltip("Panel background tinted by drive state (yellow wind-up, red while moving).")]
    public Graphic panelBackground;
    public Color windUpColor = new Color(0.55f, 0.45f, 0.05f, 0.9f);
    public Color movingColor = new Color(0.85f, 0.1f, 0.1f, 0.9f);

    [Header("Wind-up (pre-motion preview)")]
    [Tooltip("Seconds the READY... preview shows before the chair moves. 0 = move immediately.")]
    public float windUpSeconds = 1f;
    [Tooltip("Optional fill image (radial/horizontal) showing the wind-up countdown.")]
    public Image windUpFill;
    [Tooltip("Ghost disc shown at the forward goal. Auto-created if empty.")]
    public GhostTargetMarker ghostMarker;
    [Tooltip("Path line used for the pre-motion preview. Auto-found if empty.")]
    public PathVisualizer pathVisualizer;
    [Tooltip("Seconds the STOPPED flash stays on the panel before scanning text returns.")]
    public float stoppedFlashSeconds = 1.2f;

    [Header("Target")]
    [Tooltip("The wheelchair's state bridge. Auto-found by avatar name if left empty.")]
    public WheelchairStateBridge bridge;
    public string avatarName = "Wheelchair_Avatar";

    [Header("Motion")]
    [Tooltip("Distance per forward command, in meters.")]
    public float forwardDistance = 3f;
    [Tooltip("Degrees rotated per turn command.")]
    public float turnAngle = 45f;
    [Tooltip("Seconds the eased turn takes. Slow enough to never startle.")]
    public float turnDuration = 1f;

    [Header("Sound")]
    [Tooltip("Bright chime when a forward command commits (GO!).")]
    public AudioClip goClip;
    [Tooltip("Low soft thunk on stop/cancel.")]
    public AudioClip stopClip;
    [Range(0f, 1f)] public float sfxVolume = 0.7f;

    // Only the first four are in the toggle cycle; Stop is its own drive state.
    private const int CycleCount = 4;
    private static readonly string[] labels = { "MOVE FORWARD", "TURN RIGHT", "TURN LEFT", "MENU", "STOP" };
    // LiberationSans SDF only has ▲ ■ • of the shape glyphs (no ▶ ◀ ≡ arrows -
    // they render as boxes), so the turn glyphs are the ▲ rotated via rich text.
    private static readonly string[] glyphs =
    {
        "▲",                            // Forward
        "<rotate=-90>▲</rotate>",       // TurnRight
        "<rotate=90>▲</rotate>",        // TurnLeft
        "•••",                          // Menu
        "■"                             // Stop
    };

    private DriveState state = DriveState.Scanning;
    private int displayIndex;
    private int scannerLast = -1;
    private Color basePanelColor;
    private Vector3 pendingGoal;
    private float windUpElapsed;
    private bool cancelTurn;
    private Coroutine turnRoutine;
    private Coroutine revertRoutine;
    private AudioSource sfx;

    void Awake()
    {
        if (panelBackground != null) basePanelColor = panelBackground.color;

        sfx = GetComponent<AudioSource>();
        if (sfx == null) sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;          // 2D UI sound
    }

    void Start()
    {
        if (bridge == null)
        {
            var avatar = GameObject.Find(avatarName);
            if (avatar != null) bridge = avatar.GetComponent<WheelchairStateBridge>();
        }
        if (bridge == null)
            Debug.LogWarning("[ExplorerController] No WheelchairStateBridge found; controls will do nothing.");

        if (pathVisualizer == null && bridge != null)
            pathVisualizer = bridge.GetComponent<PathVisualizer>();
        if (ghostMarker == null)
            ghostMarker = GhostTargetMarker.CreateDefault();
    }

    void OnEnable()
    {
        scanner.Reset();
        scannerLast = scanner.CurrentIndex;
        displayIndex = scanner.CurrentIndex;
        state = DriveState.Scanning;
        HideTransients();
        SetPanel(labels[displayIndex], glyphs[displayIndex], basePanelColor, pop: false);
    }

    void Update()
    {
        if (!ScanFocus.IsEmpty)
        {
            // An overlay owns the two keys. Never leave motion transients dangling
            // behind it (opening the menu also parks the chair via GameModeManager).
            if (state != DriveState.Scanning) AbortToScanning();
            return;
        }

        switch (state)
        {
            case DriveState.Scanning: TickScanning(); break;
            case DriveState.WindUp:   TickWindUp();   break;
            case DriveState.Moving:   TickMoving();   break;
            case DriveState.Turning:  TickTurning();  break;
        }
    }

    // --- Scanning -----------------------------------------------------------

    private void TickScanning()
    {
        // Safety net: if motion is in flight from anywhere, the panel must
        // become the STOP button - it always tells the truth about the chair.
        if (bridge != null && bridge.HasGoal) { EnterMoving(); return; }

        // Direct keys: execute immediately, same panel feedback as the loop.
        if (Pressed(KeyCode.W, KeyCode.UpArrow))    { BeginCommand(Command.Forward);   return; }
        if (Pressed(KeyCode.D, KeyCode.RightArrow)) { BeginCommand(Command.TurnRight); return; }
        if (Pressed(KeyCode.A, KeyCode.LeftArrow))  { BeginCommand(Command.TurnLeft);  return; }
        if (Pressed(KeyCode.S, KeyCode.DownArrow))  { DoStop();                        return; }

        // 2-key loop: Space cycles the panel, Enter executes the shown command.
        int selected = scanner.Tick(CycleCount);
        if (scanner.CurrentIndex != scannerLast)
        {
            scannerLast = scanner.CurrentIndex;
            Show((Command)scanner.CurrentIndex);    // pop on each toggle
        }
        if (selected >= 0) BeginCommand((Command)selected);
    }

    /// <summary>
    /// Route a command into the state machine. Public so play-mode verification
    /// can drive states without keyboard injection.
    /// </summary>
    public void BeginCommand(Command command)
    {
        Show(command);                              // confirmation pop on BOTH input paths
        switch (command)
        {
            case Command.Forward:
                if (bridge == null) return;
                Transform t = bridge.transform;
                pendingGoal = t.position + t.forward * forwardDistance;
                if (windUpSeconds > 0f) EnterWindUp();
                else CommitForward();
                break;
            case Command.TurnRight:
                BeginTurn(turnAngle);
                break;
            case Command.TurnLeft:
                BeginTurn(-turnAngle);
                break;
            case Command.Settings:
                if (GameModeManager.Instance != null) GameModeManager.Instance.OpenPause();
                break;
            case Command.Stop:
                DoStop();
                break;
        }
    }

    // --- WindUp (pre-motion preview) -----------------------------------------

    private void EnterWindUp()
    {
        SetState(DriveState.WindUp);
        windUpElapsed = 0f;
        SetPanel("READY...", glyphs[(int)Command.Forward], windUpColor, pop: false);
        if (ghostMarker != null) ghostMarker.Show(pendingGoal);
        if (pathVisualizer != null) pathVisualizer.ShowPreview(pendingGoal);
        if (windUpFill != null)
        {
            windUpFill.fillAmount = 0f;
            windUpFill.gameObject.SetActive(true);
        }
    }

    private void TickWindUp()
    {
        if (AnySwitchPressed()) { StopToScanning(); return; }   // cancel = fail to stillness
        windUpElapsed += Time.deltaTime;
        if (windUpFill != null) windUpFill.fillAmount = Mathf.Clamp01(windUpElapsed / windUpSeconds);
        if (windUpElapsed >= windUpSeconds) CommitForward();
    }

    private void CommitForward()
    {
        if (bridge == null) { AbortToScanning(); return; }
        SetPanel("GO!", glyphs[(int)Command.Forward], movingColor, pop: true);
        PlayClip(goClip);
        bridge.SendNavigationGoal(pendingGoal);     // the ONLY place forward motion is issued
        EnterMoving(keepGoLabel: true);
    }

    // --- Moving (panel = STOP button) ----------------------------------------

    private void EnterMoving(bool keepGoLabel = false)
    {
        SetState(DriveState.Moving);
        if (windUpFill != null) windUpFill.gameObject.SetActive(false);
        if (keepGoLabel)
        {
            // Let the child read GO! before the panel becomes the STOP button.
            if (revertRoutine != null) StopCoroutine(revertRoutine);
            revertRoutine = StartCoroutine(GoToStopLabelRoutine());
        }
        else
        {
            SetPanel("STOP", glyphs[(int)Command.Stop], movingColor, pop: false);
        }
        // Show the truthful (NavMesh-snapped) goal, including externally-set ones.
        if (ghostMarker != null && bridge != null && bridge.HasGoal) ghostMarker.Show(bridge.CurrentGoal);
    }

    private IEnumerator GoToStopLabelRoutine()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        revertRoutine = null;
        if (state == DriveState.Moving)
            SetPanel("STOP", glyphs[(int)Command.Stop], movingColor, pop: false);
    }

    private void TickMoving()
    {
        if (AnySwitchPressed()) { DoStop(); return; }
        if (bridge == null || !bridge.HasGoal) ArriveToScanning();
    }

    /// <summary>
    /// Immediate halt. Public so play-mode verification can drive it. Idempotent
    /// and never debounced - a stop press is always honored instantly.
    /// </summary>
    public void DoStop()
    {
        if (bridge != null) bridge.StopMotion();
        StopToScanning();
    }

    // --- Turning --------------------------------------------------------------

    private void BeginTurn(float degrees)
    {
        if (bridge == null) return;
        bridge.StopMotion();            // stop any forward motion before reorienting
        SetState(DriveState.Turning);
        cancelTurn = false;
        int glyph = degrees >= 0f ? (int)Command.TurnRight : (int)Command.TurnLeft;
        SetPanel("TURNING", glyphs[glyph], windUpColor, pop: false);
        turnRoutine = StartCoroutine(TurnRoutine(degrees));
    }

    private void TickTurning()
    {
        if (AnySwitchPressed()) cancelTurn = true;  // the coroutine sees it this frame
    }

    IEnumerator TurnRoutine(float degrees)
    {
        float elapsed = 0f;
        float prevTarget = 0f;
        while (elapsed < turnDuration && !cancelTurn)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / turnDuration);
            float eased = p * p * (3f - 2f * p);    // smoothstep: gentle start and finish
            float target = degrees * eased;
            float step = target - prevTarget;
            prevTarget = target;
            // The bridge integrates velocity over deltaTime, so feed it the rate
            // that produces exactly `step` degrees this frame (lands on 45 exactly).
            bridge.SendLowLevelVelocity(0f, step / Mathf.Max(Time.deltaTime, 1e-5f));
            yield return null;
        }
        turnRoutine = null;
        if (cancelTurn) StopToScanning();
        else ArriveToScanning();
    }

    // --- Transitions back to Scanning ------------------------------------------

    /// <summary>User-initiated stop/cancel: red STOPPED flash + thunk, then scan.</summary>
    private void StopToScanning()
    {
        KillTurnRoutine();
        HideTransients();
        SetPanel("STOPPED", glyphs[(int)Command.Stop], movingColor, pop: true);
        PlayClip(stopClip);
        SetState(DriveState.Scanning);
        scanner.Reset();                // re-arm cooldown so the stop press can't leak into a cycle
        scannerLast = scanner.CurrentIndex;
        if (revertRoutine != null) StopCoroutine(revertRoutine);
        revertRoutine = StartCoroutine(RevertPanelRoutine());
    }

    /// <summary>Motion finished on its own (arrival / turn complete): quiet return.</summary>
    private void ArriveToScanning()
    {
        KillTurnRoutine();
        HideTransients();
        SetState(DriveState.Scanning);
        RestoreScanPanel();
    }

    /// <summary>An overlay opened mid-motion: clean up silently (pause already parks the chair).</summary>
    private void AbortToScanning()
    {
        KillTurnRoutine();
        cancelTurn = false;
        HideTransients();
        SetState(DriveState.Scanning);
        RestoreScanPanel();
    }

    private IEnumerator RevertPanelRoutine()
    {
        yield return new WaitForSecondsRealtime(stoppedFlashSeconds);
        revertRoutine = null;
        if (state == DriveState.Scanning) RestoreScanPanel();
    }

    private void RestoreScanPanel()
    {
        displayIndex = scanner.CurrentIndex;
        SetPanel(labels[displayIndex], glyphs[displayIndex], basePanelColor, pop: false);
    }

    private void KillTurnRoutine()
    {
        if (turnRoutine != null) { StopCoroutine(turnRoutine); turnRoutine = null; }
        cancelTurn = false;
    }

    private void HideTransients()
    {
        if (ghostMarker != null) ghostMarker.Hide();
        if (pathVisualizer != null) pathVisualizer.ClearPreview();
        if (windUpFill != null) windUpFill.gameObject.SetActive(false);
    }

    // --- Panel + input helpers --------------------------------------------------

    /// <summary>Drive the panel to a command and fire the pop/flash feedback.</summary>
    private void Show(Command command)
    {
        displayIndex = (int)command;
        SetPanel(labels[displayIndex], glyphs[displayIndex], basePanelColor, pop: true);
    }

    private void SetPanel(string label, string glyph, Color color, bool pop)
    {
        if (commandLabel != null) commandLabel.text = label;
        if (commandGlyph != null) commandGlyph.text = glyph;
        if (panelBackground != null)
        {
            panelBackground.color = color;
            // Keep the pop flash settling onto the state color, not the Awake color.
            if (panelJuice != null) panelJuice.RebaseFlashColor(color);
        }
        if (pop && panelJuice != null) panelJuice.Pop();
    }

    private void SetState(DriveState next)
    {
        if (state == next) return;
        Debug.Log("[Explorer] " + state + " -> " + next);
        state = next;
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip != null && sfx != null) sfx.PlayOneShot(clip, sfxVolume);
    }

    /// <summary>
    /// Raw read of every input the child might reach for - both switches and all
    /// direct keys. Used in non-Scanning states where any press means STOP/CANCEL,
    /// deliberately bypassing the scanner cooldown (stopping is idempotent and
    /// must never wait).
    /// </summary>
    private bool AnySwitchPressed()
    {
        return Input.GetKeyDown(scanner.toggleKey) || Input.GetKeyDown(scanner.selectKey)
            || Pressed(KeyCode.W, KeyCode.UpArrow) || Pressed(KeyCode.A, KeyCode.LeftArrow)
            || Pressed(KeyCode.D, KeyCode.RightArrow) || Pressed(KeyCode.S, KeyCode.DownArrow);
    }

    private static bool Pressed(KeyCode a, KeyCode b)
    {
        return Input.GetKeyDown(a) || Input.GetKeyDown(b);
    }
}
