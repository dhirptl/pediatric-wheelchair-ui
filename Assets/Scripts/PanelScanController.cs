using UnityEngine;

/// <summary>
/// Drop-in switch-access scanning for an overlay panel (pause, destination, etc).
/// While the panel is enabled it grabs scan focus, walks its buttons with the
/// shared SwitchScanner (Space cycles, Enter selects), and invokes the selected
/// button's onClick - so the same persistent listeners that fire on a touch tap
/// also fire from the two-switch loop. Closing the panel returns focus to the
/// Explorer command panel automatically.
/// </summary>
public class PanelScanController : MonoBehaviour
{
    [Tooltip("Buttons to scan, in highlight order. Space cycles, Enter selects.")]
    public ScanGroup group = new ScanGroup();

    private bool wired;

    void Awake()
    {
        if (!wired)
        {
            group.OnOptionSelected += OnSelected;
            wired = true;
        }
    }

    void OnEnable()
    {
        ScanFocus.Push(this);
        group.Activate();
    }

    void OnDisable()
    {
        group.Deactivate();
        ScanFocus.Pop(this);
    }

    void Update()
    {
        if (ScanFocus.IsTop(this)) group.Tick();
    }

    private void OnSelected(int index)
    {
        if (group.options == null || index < 0 || index >= group.options.Length) return;
        var btn = group.options[index];
        if (btn != null && btn.interactable) btn.onClick.Invoke();
    }
}
