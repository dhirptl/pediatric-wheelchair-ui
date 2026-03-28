using UnityEngine;
using UnityEngine.AI; // We need this to talk to the NavMesh!

public class ClickToMove : MonoBehaviour
{
    private NavMeshAgent agent;

    void Start()
    {
        // Grab the AI brain attached to our capsule
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        // 0 is the left mouse button
        if (Input.GetMouseButtonDown(0)) 
        {
            // Shoot an invisible laser from the camera through the mouse pointer
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // If the laser hits something solid (like our baked floor)...
            if (Physics.Raycast(ray, out hit))
            {
                // Tell the AI brain to drive to that exact spot!
                agent.SetDestination(hit.point);
            }
        }
    }
}