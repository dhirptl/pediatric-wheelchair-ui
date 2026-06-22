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
    public enum Mode { MagicTravel, Explorer, SmartGuide }

    public static GameModeManager Instance { get; private set; }

    [Header("Mode panels")]
    public GameObject explorerDashboard;
    public GameObject destinationPanel;
    public GameObject smartGuidePanel;

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
    public void SetModeSmartGuide() => SetMode(Mode.SmartGuide);

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
        if (pauseOverlay != null) pauseOverlay.SetActive(true);
    }

    // Wired to the Smart Guide panel's "BACK TO MENU" button. Hides the Smart Guide
    // screen so it doesn't linger behind the menu, then opens the settings overlay.
    // Resume (ClosePause) re-Applies the current mode and brings the picker back.
    public void BackToMenu()
    {
        if (smartGuidePanel != null) smartGuidePanel.SetActive(false);
        OpenPause();
    }

    public void ClosePause()
    {
        if (pauseOverlay != null) pauseOverlay.SetActive(false);
        // Restore whichever mode panel is current. A no-op for modes whose panel was
        // never hidden (Explorer / Magic Travel); re-shows Smart Guide after BackToMenu.
        Apply();
    }

    private void Apply()
    {
        if (explorerDashboard != null) explorerDashboard.SetActive(CurrentMode == Mode.Explorer);
        if (destinationPanel != null) destinationPanel.SetActive(CurrentMode == Mode.MagicTravel);
        if (smartGuidePanel != null) smartGuidePanel.SetActive(CurrentMode == Mode.SmartGuide);
    }
}
