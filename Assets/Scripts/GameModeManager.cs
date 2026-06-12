using System;
using UnityEngine;

/// <summary>
/// Owns the active control mode (Magic Travel vs Explorer) and the pause/settings
/// overlay. Mode changes toggle the matching HUD panels, stop any in-flight
/// motion through the bridge, and persist across sessions via PlayerPrefs.
///
/// The game boots straight into the environment (no separate Main Menu scene);
/// this pause/settings overlay is the master mode switcher.
/// </summary>
public class GameModeManager : MonoBehaviour
{
    public enum Mode { MagicTravel, Explorer }

    public static GameModeManager Instance { get; private set; }

    [Header("Mode panels")]
    public GameObject explorerDashboard;
    public GameObject destinationPanel;

    [Header("Pause overlay")]
    public GameObject pauseOverlay;

    public Mode CurrentMode { get; private set; } = Mode.Explorer;
    public event Action<Mode> OnModeChanged;

    void Awake()
    {
        Instance = this;
        if (Enum.TryParse(GamePrefs.GetString(GamePrefs.GameMode, Mode.Explorer.ToString()), out Mode saved))
            CurrentMode = saved;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (pauseOverlay != null) pauseOverlay.SetActive(false);
        Apply();
    }

    // Wired to the pause-overlay buttons.
    public void SetModeMagicTravel() => SetMode(Mode.MagicTravel);
    public void SetModeExplorer() => SetMode(Mode.Explorer);

    public void SetMode(Mode mode)
    {
        CurrentMode = mode;
        GamePrefs.SetString(GamePrefs.GameMode, mode.ToString());
        if (WheelchairStateBridge.Instance != null)
            WheelchairStateBridge.Instance.StopMotion();
        Apply();
        ClosePause();
        OnModeChanged?.Invoke(mode);
    }

    public void OpenPause()
    {
        // Opening the menu always parks the chair - a child reaching for
        // Settings mid-drive must never leave it rolling behind the overlay.
        if (WheelchairStateBridge.Instance != null)
            WheelchairStateBridge.Instance.StopMotion();
        if (pauseOverlay != null) pauseOverlay.SetActive(true);
    }

    public void ClosePause()
    {
        if (pauseOverlay != null) pauseOverlay.SetActive(false);
    }

    private void Apply()
    {
        if (explorerDashboard != null) explorerDashboard.SetActive(CurrentMode == Mode.Explorer);
        if (destinationPanel != null) destinationPanel.SetActive(CurrentMode == Mode.MagicTravel);
    }
}
