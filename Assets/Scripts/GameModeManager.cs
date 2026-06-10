using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the active control mode (Magic Travel vs Explorer) and the pause/settings
/// overlay. Mode changes toggle the matching HUD panels, stop any in-flight
/// motion through the bridge, and persist across sessions via PlayerPrefs.
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
        if (pauseOverlay != null) pauseOverlay.SetActive(true);
    }

    public void ClosePause()
    {
        if (pauseOverlay != null) pauseOverlay.SetActive(false);
    }

    public void QuitToMenu()
    {
        GamePrefs.DeleteKey(GamePrefs.Destination);
        SceneManager.LoadScene("MainMenu");
    }

    private void Apply()
    {
        if (explorerDashboard != null) explorerDashboard.SetActive(CurrentMode == Mode.Explorer);
        if (destinationPanel != null) destinationPanel.SetActive(CurrentMode == Mode.MagicTravel);
    }
}
