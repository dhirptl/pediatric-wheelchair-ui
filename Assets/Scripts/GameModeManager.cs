using System;
using UnityEngine;

/// <summary>
/// Owns the active control mode (Magic Travel / Explorer / Smart Guide). Mode
/// changes toggle the matching HUD panels, stop any in-flight motion through the
/// bridge, and persist across sessions via PlayerPrefs.
///
/// The game boots straight into the environment (no separate Main Menu scene);
/// the persistent top tab bar (ModeTabBar) is the master mode switcher. There is
/// no separate pause/settings modal - Apply() is the single authority deciding
/// which one mode panel is visible, so the tab, the panel, and the mode can never
/// drift apart.
/// </summary>
public class GameModeManager : MonoBehaviour
{
    public enum Mode { MagicTravel, Explorer, SmartGuide }

    public static GameModeManager Instance { get; private set; }

    [Header("Mode panels")]
    public GameObject explorerDashboard;
    public GameObject destinationPanel;
    public GameObject smartGuidePanel;

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
        Apply();
    }

    // Wired to the top tab bar buttons (ModeTabBar).
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
        OnModeChanged?.Invoke(mode);
    }

    private void Apply()
    {
        if (explorerDashboard != null) explorerDashboard.SetActive(CurrentMode == Mode.Explorer);
        if (destinationPanel != null) destinationPanel.SetActive(CurrentMode == Mode.MagicTravel);
        if (smartGuidePanel != null) smartGuidePanel.SetActive(CurrentMode == Mode.SmartGuide);
    }
}
