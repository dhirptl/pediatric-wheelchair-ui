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

    private Button[] currentActiveButtons;

    void Start()
    {
        modeSelectionPanel.SetActive(true);
        destinationPanel.SetActive(false);
        SetActiveButtons(modeButtons);
    }

    void Update()
    {
        if (currentActiveButtons == null || currentActiveButtons.Length == 0) return;

        int selected = scanner.Tick(currentActiveButtons.Length);
        HighlightCurrentButton();
        if (selected >= 0)
            currentActiveButtons[selected].onClick.Invoke();
    }

    private void SetActiveButtons(Button[] buttons)
    {
        currentActiveButtons = buttons;
        // Reset() also arms the input cooldown, so the key that selected the previous
        // screen doesn't immediately trigger something on the new one.
        scanner.Reset();
        HighlightCurrentButton();
    }

    private void HighlightCurrentButton()
    {
        if (currentActiveButtons != null && currentActiveButtons.Length > 0)
        {
            int i = Mathf.Clamp(scanner.CurrentIndex, 0, currentActiveButtons.Length - 1);
            currentActiveButtons[i].Select();
        }
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
        SetActiveButtons(destinationButtons);
    }

    public void OnDestinationSelected(string targetName)
    {
        GamePrefs.SetString(GamePrefs.GameMode, "MagicTravel");
        GamePrefs.SetString(GamePrefs.Destination, targetName);
        SceneManager.LoadScene("MapScene");
    }
}
