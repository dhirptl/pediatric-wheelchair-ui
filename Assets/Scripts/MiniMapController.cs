using UnityEngine;

/// <summary>
/// Frames the orthographic mini-map camera over the runtime-generated map so the
/// whole floor plan fits the render texture regardless of which PNG was loaded.
/// </summary>
[RequireComponent(typeof(Camera))]
public class MiniMapController : MonoBehaviour
{
    [Tooltip("The map generator to frame. Auto-found if left empty.")]
    public MapGenerator map;
    [Tooltip("Extra margin around the map edges (1 = exact fit).")]
    public float padding = 1.05f;
    [Tooltip("Camera height above the floor.")]
    public float height = 60f;

    public Camera Cam { get; private set; }

    void Awake()
    {
        Cam = GetComponent<Camera>();
    }

    void Start()
    {
        if (map == null) map = FindObjectOfType<MapGenerator>();

        if (MapGenerator.IsMapReady) Frame();
        else MapGenerator.OnMapReady += Frame;
    }

    void OnDestroy()
    {
        MapGenerator.OnMapReady -= Frame;
    }

    private void Frame()
    {
        if (map == null) return;
        transform.position = new Vector3(0f, height, 0f);   // map is centered at origin
        transform.rotation = Quaternion.Euler(90f, 0f, 0f); // straight down
        Cam.orthographic = true;
        Cam.orthographicSize = Mathf.Max(map.WorldWidth, map.WorldHeight) / 2f * padding;
    }
}
