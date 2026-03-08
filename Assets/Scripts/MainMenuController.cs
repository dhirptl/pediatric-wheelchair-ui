using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject modeSelectionPanel;
    public GameObject destinationPanel;

    void Start()
    {
        // Force the game to start on the Mode Selection screen
        modeSelectionPanel.SetActive(true);
        destinationPanel.SetActive(false);
    }

    public void OnExplorerModeClicked()
    {
        // Loads your actual map level. 
        // Note: Change "MapScene" to whatever you actually named your game scene!
        SceneManager.LoadScene("MapScene");
    }

    public void OnMagicTravelClicked()
    {
        // Hides the first screen and shows the destination cards
        modeSelectionPanel.SetActive(false);
        destinationPanel.SetActive(true);
    }
}