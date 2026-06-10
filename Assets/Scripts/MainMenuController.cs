using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject modeSelectionPanel;
    public GameObject destinationPanel;

    [Header("Selection strategy")]
    [Tooltip("ToggleSelect (default): Space cycles the highlight, Return selects. TimeScan: highlight auto-advances every dwellSeconds and a single key selects.")]
    public SwitchScanner scanner = new SwitchScanner();

    [Header("Screen 1: Mode Buttons")]
    public Button[] modeButtons;

    [Header("Screen 2: Destination Cards")]
    public Button[] destinationButtons;

    private ScanGroup modeGroup;
    private ScanGroup destinationGroup;
    private ScanGroup activeGroup;

    void Start()
    {
        // Both groups share the serialized scanner so its keys/dwell settings
        // apply to every screen; Activate() resets state between screens.
        modeGroup = new ScanGroup { options = modeButtons, scanner = scanner };
        destinationGroup = new ScanGroup { options = destinationButtons, scanner = scanner };
        modeGroup.OnOptionSelected += i => modeButtons[i].onClick.Invoke();
        destinationGroup.OnOptionSelected += i => destinationButtons[i].onClick.Invoke();

        modeSelectionPanel.SetActive(true);
        destinationPanel.SetActive(false);
        SetActiveGroup(modeGroup);
    }

    void Update()
    {
        activeGroup?.Tick();
    }

    private void SetActiveGroup(ScanGroup group)
    {
        // Activate() also arms the input cooldown, so the key that selected the
        // previous screen doesn't immediately trigger something on the new one.
        activeGroup?.Deactivate();
        activeGroup = group;
        group.Activate();
    }

    // --- BUTTON CLICK FUNCTIONS ---

    public void OnExplorerModeClicked()
    {
        GamePrefs.SetString(GamePrefs.GameMode, "Explorer");
        SceneManager.LoadScene("MapScene");
    }

    public void OnMagicTravelClicked()
    {
        modeSelectionPanel.SetActive(false);
        destinationPanel.SetActive(true);
        SetActiveGroup(destinationGroup);
    }

    public void OnDestinationSelected(string targetName)
    {
        GamePrefs.SetString(GamePrefs.GameMode, "MagicTravel");
        GamePrefs.SetString(GamePrefs.Destination, targetName);
        SceneManager.LoadScene("MapScene");
    }
}
