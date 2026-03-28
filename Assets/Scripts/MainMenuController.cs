using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Needed for Button logic

public class MainMenuController : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject modeSelectionPanel;
    public GameObject destinationPanel;

    [Header("BCI Controls")]
    public KeyCode toggleKey = KeyCode.Space;
    public KeyCode selectKey = KeyCode.Return; // "Return" is the Enter key on Mac

    [Header("Screen 1: Mode Buttons")]
    public Button[] modeButtons;
    
    [Header("Screen 2: Destination Cards")]
    public Button[] destinationButtons;

    // These variables keep track of where the user currently is
    private Button[] currentActiveButtons;
    private int currentIndex = 0;

    void Start()
    {
        // Start on the Main Menu
        modeSelectionPanel.SetActive(true);
        destinationPanel.SetActive(false);
        
        // Tell the script we are looking at the Mode Selection screen right now
        currentActiveButtons = modeButtons;
        HighlightCurrentButton();
    }

    void Update()
    {
        // 1. The Toggle Action (Cycle through buttons)
        if (Input.GetKeyDown(toggleKey))
        {
            currentIndex++;
            // If we go past the last button, loop back to the first one
            if (currentIndex >= currentActiveButtons.Length)
            {
                currentIndex = 0;
            }
            HighlightCurrentButton();
        }

        // 2. The Select Action (Click the button)
        if (Input.GetKeyDown(selectKey))
        {
            // This triggers the exact same logic as if the user clicked it with a mouse!
            currentActiveButtons[currentIndex].onClick.Invoke();
        }
    }

    private void HighlightCurrentButton()
    {
        // Unity's built-in Select() visually highlights the button
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
        // Hide Main Menu, Show Destinations
        modeSelectionPanel.SetActive(false);
        destinationPanel.SetActive(true);
        
        // Update our BCI logic to look at the new list of room cards
        currentActiveButtons = destinationButtons;
        currentIndex = 0; // Reset the highlight to the first room card
        HighlightCurrentButton();
    }
    public void OnDestinationSelected(string targetName)
    {
        // 1. Save the exact name of the 3D target object
        PlayerPrefs.SetString("Destination", targetName);
        PlayerPrefs.Save(); 

        // 2. Load the 3D Map Scene
        SceneManager.LoadScene("MapScene");
    }
}