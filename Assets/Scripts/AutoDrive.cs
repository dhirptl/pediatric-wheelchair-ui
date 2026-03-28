using UnityEngine;
using UnityEngine.AI;
using System.Collections; // We need this to make the script wait!

public class AutoDrive : MonoBehaviour
{
    private NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        
        // Tell Unity to run our custom delay sequence
        StartCoroutine(DriveWithDelay());
    }

    // This is a Coroutine - it allows us to pause code execution
    IEnumerator DriveWithDelay()
    {
        // Wait for exactly ONE frame to ensure the Map and NavMesh are 100% loaded
        yield return null; 

        // 1. Check if the Menu Scene saved a destination for us
        string targetRoomName = PlayerPrefs.GetString("Destination", "");

        if (targetRoomName != "")
        {
            // 2. Search the 3D scene for an object with that exact name
            GameObject targetObject = GameObject.Find(targetRoomName);

            if (targetObject != null)
            {
                // 3. Drive there!
                agent.SetDestination(targetObject.transform.position);
                Debug.Log("SUCCESS! Calculating path to: " + targetRoomName);
            }
            else
            {
                // This means there is a typo between your UI text box and the Hierarchy name!
                Debug.LogError("Uh oh! I looked everywhere but could not find a target named EXACTLY: '" + targetRoomName + "'");
            }
        }
        else
        {
            Debug.LogWarning("The Map loaded, but PlayerPrefs was completely empty. Did the UI save the string?");
        }
        
        // Clear the saved data
        PlayerPrefs.DeleteKey("Destination");
    }
}