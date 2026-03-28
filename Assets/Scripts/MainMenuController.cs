using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; 

public class MainMenuController : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject modeSelectionPanel;
    public GameObject destinationPanel;

    [Header("BCI Controls")]
    public KeyCode toggleKey = KeyCode.Space;
    public KeyCode selectKey = KeyCode.Return; 

    [Header("Accessibility Settings")]
    public float inputCooldown = 0.5f; // Half a second delay between accepted inputs
    private float nextInputTime = 0f;

    [Header("Screen 1: Mode Buttons")]
    public Button[] modeButtons;
    
    [Header("Screen 2: Destination Cards")]
    public Button[] destinationButtons;

    private Button[] currentActiveButtons;
    private int currentIndex = 0;

    void Start()
    {
        modeSelectionPanel.SetActive(true);
        destinationPanel.SetActive(false);
        currentActiveButtons = modeButtons;
        HighlightCurrentButton();
    }

    void Update()
    {
        // THE DEBOUNCER: If the current time hasn't passed our cooldown, ignore all key presses!
        if (Time.time < nextInputTime) return;

        if (Input.GetKeyDown(toggleKey))
        {
            currentIndex++;
            if (currentIndex >= currentActiveButtons.Length)
            {
                currentIndex = 0;
            }
            HighlightCurrentButton();
        }

        if (Input.GetKeyDown(selectKey))
        {
            // Lock the input system for 0.5 seconds BEFORE we swap the screens
            nextInputTime = Time.time + inputCooldown;
            
            currentActiveButtons[currentIndex].onClick.Invoke();
        }
    }

    private void HighlightCurrentButton()
    {
        if (currentActiveButtons.Length > 0)
        {
            currentActiveButtons[currentIndex].Select();
        }
    }

    // --- BUTTON CLICK FUNCTIONS ---

    public void OnExplorerModeClicked()
    {
        SceneManager.LoadScene("MapScene"); 
    }

    public void OnMagicTravelClicked()
    {
        modeSelectionPanel.SetActive(false);
        destinationPanel.SetActive(true);
        currentActiveButtons = destinationButtons;
        currentIndex = 0; 
        HighlightCurrentButton();
    }

    public void OnDestinationSelected(string targetName)
    {
        PlayerPrefs.SetString("Destination", targetName);
        PlayerPrefs.Save(); 
        SceneManager.LoadScene("MapScene");
    }
}