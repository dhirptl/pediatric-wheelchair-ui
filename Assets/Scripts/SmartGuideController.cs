using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Smart Guide mode - the friendly LIDAR-assisted follow screen. It runs in two
/// phases:
///
///   PICKER    - asks "What do you want to follow?" and offers one large button per
///               detected target (grown-up, wall, exit/doorway), plus a back button.
///   TRACKING  - once a target is chosen the picker buttons disappear and an
///               "Active Tracking" HUD takes over: the target name, a live distance
///               readout, and a [ Stop Following ] button that is ALWAYS reachable by
///               the two-switch scanner (Enter = emergency stop). The chair also
///               auto-stops and celebrates once it arrives.
///
/// The distance/detection all flows through ILidarFeed, never the concrete backend,
/// so swapping the simulation for the real ROS 2 / LIDAR feed is a one-line change.
///
/// Lives on the SmartGuidePanel GameObject, which GameModeManager toggles active when
/// the mode is selected. Like ThemeShopController it owns its own switch-access
/// scanning and grabs scan focus the moment it opens (OnEnable) so the loop starts
/// cycling instantly - no touch needed to kick it off.
/// </summary>
public class SmartGuideController : MonoBehaviour
{
    [Header("Panel text")]
    public TextMeshProUGUI titleLabel;          // prompt (picker) / celebration (success)
    public TextMeshProUGUI followNameLabel;     // "Following: grown-up" (tracking)
    public TextMeshProUGUI distanceLabel;       // "Distance: 2.4 m" (tracking)
    [Tooltip("Shown while choosing a target.")]
    public string promptText = "What do you want to follow?";

    [Header("Target buttons (one per type)")]
    public Button caregiverButton;   // Caregiver (grown-up)
    public Button wallButton;        // WallCorridor
    public Button doorButton;        // Doorway (exit / doorway)
    [Tooltip("Hands the scanner to the top tab bar (ModeTabBar.Focus) so the child can switch mode/view. Appended to the picker scan ring.")]
    public Button menuButton;

    [Header("Active-tracking HUD")]
    [Tooltip("Ends the follow immediately. Always the only item in the tracking scan ring, so Enter is a one-press emergency stop.")]
    public Button stopButton;

    [Header("Switch-access scanning")]
    public SwitchScanner scanner = new SwitchScanner();
    [Tooltip("Highlight scale for buttons without a ButtonHighlighter.")]
    public float fallbackScale = 1.08f;

    [Header("Follow behavior")]
    [Tooltip("The wheelchair's state bridge. Auto-found by avatar name if left empty.")]
    public WheelchairStateBridge bridge;
    public string avatarName = "Wheelchair_Avatar";
    [Tooltip("Stop and celebrate once the chair is within this many meters of the target. Sits just above the NavMeshAgent's stopping plateau so arrival reliably fires.")]
    public float arrivalRadius = 1.25f;
    [Tooltip("Seconds between logged distance samples (UI updates every frame regardless).")]
    public float logInterval = 1.0f;
    [Tooltip("Seconds the 'You made it!' message shows before returning to the picker.")]
    public float successHold = 1.6f;

    private enum Phase { Picker, Following, Success }

    private ILidarFeed feed;
    private readonly List<Button> ring = new List<Button>();
    private readonly List<SmartGuideTarget.TargetType?> ringTypes = new List<SmartGuideTarget.TargetType?>(); // null = menu/stop
    private readonly FollowAssistLog log = new FollowAssistLog();

    private Phase phase = Phase.Picker;
    private SmartGuideTarget activeTarget;
    private float logTimer;
    private int scannerLast = -1;
    private Coroutine successCo;

    void Start()
    {
        if (bridge == null)
        {
            var avatar = GameObject.Find(avatarName);
            if (avatar != null) bridge = avatar.GetComponent<WheelchairStateBridge>();
        }
    }

    void OnEnable()
    {
        feed = FollowAssistBackend.Instance;   // the only line that changes for the real ROS 2 feed
        EnterPicker();
        // Grab the two switch keys immediately so scanning starts without a click.
        ScanFocus.Push(this);
    }

    void OnDisable()
    {
        if (successCo != null) { StopCoroutine(successCo); successCo = null; }
        // Leaving the mode mid-follow counts as a cancel; flush whatever we logged.
        if (log.IsActive) log.EndFollow(false);
        if (phase == Phase.Following && bridge != null) bridge.StopMotion();
        phase = Phase.Picker;
        activeTarget = null;
        ClearHighlights();
        ScanFocus.Pop(this);
    }

    void Update()
    {
        if (!ScanFocus.IsTop(this)) return;
        if (phase == Phase.Success) return; // celebration coroutine owns the screen

        if (phase == Phase.Following)
        {
            TickFollow();
            if (phase != Phase.Following) return; // auto-completed this frame
        }

        if (ring.Count == 0) return;
        int selected = scanner.Tick(ring.Count);
        if (scanner.CurrentIndex != scannerLast)
        {
            scannerLast = scanner.CurrentIndex;
            RefreshHighlights();
        }
        if (selected >= 0) Activate(selected);
    }

    // --- Picker phase -------------------------------------------------------

    private void EnterPicker()
    {
        phase = Phase.Picker;
        activeTarget = null;
        SetPhaseVisuals(picker: true);
        if (titleLabel != null) titleLabel.text = promptText;
        if (distanceLabel != null) distanceLabel.text = "";

        BuildPickerRing();
        RefreshAvailability();
        scanner.Reset();
        scannerLast = scanner.CurrentIndex;
        RefreshHighlights();
    }

    private void BuildPickerRing()
    {
        ring.Clear();
        ringTypes.Clear();
        AddTargetButton(caregiverButton, SmartGuideTarget.TargetType.Caregiver);
        AddTargetButton(wallButton, SmartGuideTarget.TargetType.WallCorridor);
        AddTargetButton(doorButton, SmartGuideTarget.TargetType.Doorway);
        if (menuButton != null) { ring.Add(menuButton); ringTypes.Add(null); }
    }

    private void AddTargetButton(Button btn, SmartGuideTarget.TargetType type)
    {
        if (btn == null) return;
        ring.Add(btn);
        ringTypes.Add(type);
    }

    /// <summary>Grey out target buttons whose type the feed isn't currently detecting.</summary>
    private void RefreshAvailability()
    {
        for (int i = 0; i < ring.Count; i++)
        {
            if (!ringTypes[i].HasValue) continue; // menu button always usable
            bool seen = feed != null && feed.HasTarget(ringTypes[i].Value);
            if (ring[i] != null) ring[i].interactable = seen;
        }
    }

    private void Activate(int index)
    {
        if (index < 0 || index >= ring.Count) return;
        Button btn = ring[index];
        // Switch-scan selection and touch both route through onClick, so the target
        // buttons (FollowCaregiver/Wall/Doorway), the menu button (ModeTabBar.Focus)
        // and the stop button (StopFollowing) behave identically either way.
        if (btn != null && btn.interactable) btn.onClick.Invoke();
    }

    // --- Follow phase -------------------------------------------------------

    // Wired to the three target buttons' onClick (touch) and reached via the scan ring.
    public void FollowCaregiver() => RequestFollow(SmartGuideTarget.TargetType.Caregiver);
    public void FollowWall() => RequestFollow(SmartGuideTarget.TargetType.WallCorridor);
    public void FollowDoorway() => RequestFollow(SmartGuideTarget.TargetType.Doorway);

    private void RequestFollow(SmartGuideTarget.TargetType type)
    {
        if (phase != Phase.Picker) return; // ignore taps once a follow / success is under way
        StartFollow(type);
    }

    private void StartFollow(SmartGuideTarget.TargetType type)
    {
        if (feed == null) feed = FollowAssistBackend.Instance;
        Vector3 from = bridge != null ? bridge.transform.position : transform.position;
        activeTarget = feed != null ? feed.Acquire(type, from) : null;

        if (activeTarget == null)
        {
            if (titleLabel != null) titleLabel.text = "Hmm, I can't see that right now!";
            return; // stay in the picker
        }

        phase = Phase.Following;
        SetPhaseVisuals(picker: false);
        if (followNameLabel != null)
            followNameLabel.text = "Following: " + SmartGuideTarget.FriendlyName(type);

        if (bridge != null) bridge.SendNavigationGoal(activeTarget.transform.position);
        log.BeginFollow(activeTarget);
        logTimer = 0f;

        BuildTrackingRing();
        scanner.Reset();
        scannerLast = scanner.CurrentIndex;
        RefreshHighlights();
    }

    private void BuildTrackingRing()
    {
        ring.Clear();
        ringTypes.Clear();
        if (stopButton != null) { ring.Add(stopButton); ringTypes.Add(null); }
    }

    private void TickFollow()
    {
        if (activeTarget == null) { EnterPicker(); return; }

        Vector3 from = bridge != null ? bridge.transform.position : transform.position;
        float distance = feed != null ? feed.DistanceTo(activeTarget, from) : activeTarget.DistanceTo(from);

        // UI updates every frame for smooth feedback...
        if (distanceLabel != null) distanceLabel.text = "Distance: " + distance.ToString("0.0") + " m";

        // ...but the log only samples about once a second to stay compact.
        logTimer += Time.deltaTime;
        if (logTimer >= logInterval)
        {
            logTimer -= logInterval;
            log.Sample(distance);
        }

        if (distance <= arrivalRadius) CompleteFollow(distance);
    }

    // Wired to the Stop button's onClick (touch) and reached via the tracking scan ring.
    public void StopFollowing()
    {
        if (phase != Phase.Following) return;
        if (bridge != null) bridge.StopMotion();
        log.EndFollow(false); // cancelled
        EnterPicker();
    }

    private void CompleteFollow(float distance)
    {
        log.Sample(distance);     // capture the final, closest reading
        log.EndFollow(true);
        if (bridge != null) bridge.StopMotion();
        phase = Phase.Success;
        activeTarget = null;

        ClearHighlights();
        // Celebration: hide every button + the tracking HUD, show only the title.
        SetActive(caregiverButton, false);
        SetActive(wallButton, false);
        SetActive(doorButton, false);
        SetActive(menuButton, false);
        SetActive(stopButton, false);
        if (followNameLabel != null) followNameLabel.gameObject.SetActive(false);
        if (distanceLabel != null) distanceLabel.gameObject.SetActive(false);
        if (titleLabel != null)
        {
            titleLabel.gameObject.SetActive(true);
            titleLabel.text = "You made it! Great driving!";
        }

        successCo = StartCoroutine(SuccessThenPicker());
    }

    private IEnumerator SuccessThenPicker()
    {
        yield return new WaitForSeconds(successHold);
        successCo = null;
        EnterPicker();
    }

    // --- Phase visuals & highlighting (mirrors ThemeShopController) ----------

    private void SetPhaseVisuals(bool picker)
    {
        SetActive(caregiverButton, picker);
        SetActive(wallButton, picker);
        SetActive(doorButton, picker);
        SetActive(menuButton, picker);
        SetActive(stopButton, !picker);
        if (titleLabel != null) titleLabel.gameObject.SetActive(picker);
        if (followNameLabel != null) followNameLabel.gameObject.SetActive(!picker);
        if (distanceLabel != null) distanceLabel.gameObject.SetActive(!picker);
    }

    private static void SetActive(Button btn, bool on)
    {
        if (btn != null) btn.gameObject.SetActive(on);
    }

    private void RefreshHighlights()
    {
        for (int i = 0; i < ring.Count; i++) SetHighlight(i, i == scanner.CurrentIndex);
    }

    private void ClearHighlights()
    {
        for (int i = 0; i < ring.Count; i++) SetHighlight(i, false);
    }

    private void SetHighlight(int i, bool on)
    {
        if (i < 0 || i >= ring.Count) return;
        Button btn = ring[i];
        if (btn == null) return;
        var hl = btn.GetComponent<ButtonHighlighter>();
        if (hl != null) { hl.SetHighlighted(on); return; }
        float s = on ? fallbackScale : 1f;
        btn.transform.localScale = new Vector3(s, s, 1f);
    }
}
