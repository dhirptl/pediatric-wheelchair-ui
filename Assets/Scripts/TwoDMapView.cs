using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the 3D vs 2D *view* state (independent of the control mode in
/// GameModeManager). The 2D view reuses the mini-map's top-down render texture
/// full-screen, hides the small mini-map, and floats a Google-Maps-style
/// wheelchair icon that tracks the avatar's position and heading.
///
/// The three control modes (Magic Travel / Explorer / Smart Guide) keep working
/// in 2D because their HUD panels simply overlay the full-screen map.
/// </summary>
public class TwoDMapView : MonoBehaviour
{
    public enum ViewMode { ThreeD, TwoD }

    public static TwoDMapView Instance { get; private set; }

    [Header("2D view objects")]
    [Tooltip("Full-screen RawImage showing the mini-map render texture.")]
    public RawImage twoDMap;
    [Tooltip("The small corner mini-map panel, hidden while in 2D view.")]
    public GameObject miniMapPanel;
    [Tooltip("Wheelchair marker icon floated over the full-screen map.")]
    public RectTransform wheelchairIcon;
    [Tooltip("Admin 'TRIM' entry button; shown only while the 2D map is up.")]
    public GameObject trimButton;

    [Header("References (auto-found if empty)")]
    public MiniMapController miniMap;
    public string avatarName = "Wheelchair_Avatar";
    [Tooltip("In-world arrow on the mini-map render texture; hidden in 2D so only the icon shows.")]
    public Transform minimapMarker;

    public ViewMode CurrentView { get; private set; } = ViewMode.ThreeD;
    public event Action<ViewMode> OnViewChanged;

    private Transform avatar;

    void Awake()
    {
        Instance = this;
        if (Enum.TryParse(GamePrefs.GetString(GamePrefs.ViewMode, ViewMode.ThreeD.ToString()), out ViewMode saved))
            CurrentView = saved;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (miniMap == null) miniMap = FindObjectOfType<MiniMapController>();
        ResolveAvatar();
        SetView(CurrentView);
    }

    private void ResolveAvatar()
    {
        if (avatar == null)
        {
            var go = GameObject.Find(avatarName);
            if (go != null) avatar = go.transform;
        }
        if (minimapMarker == null && avatar != null)
            minimapMarker = avatar.Find("MinimapMarker");
    }

    // Wired to the "2D MAP" top tab.
    public void ToggleView() => SetView(CurrentView == ViewMode.TwoD ? ViewMode.ThreeD : ViewMode.TwoD);

    public void SetView(ViewMode view)
    {
        CurrentView = view;
        GamePrefs.SetString(GamePrefs.ViewMode, view.ToString());

        bool twoD = view == ViewMode.TwoD;
        if (twoDMap != null) twoDMap.gameObject.SetActive(twoD);
        if (wheelchairIcon != null) wheelchairIcon.gameObject.SetActive(twoD);
        if (trimButton != null) trimButton.SetActive(twoD);
        if (miniMapPanel != null) miniMapPanel.SetActive(!twoD);
        // The in-world arrow is the indicator on the small mini-map (3D); in 2D the
        // crisp UI icon replaces it, so hide the arrow to avoid a double marker.
        ResolveAvatar();
        if (minimapMarker != null) minimapMarker.gameObject.SetActive(!twoD);

        OnViewChanged?.Invoke(view);
    }

    void Update()
    {
        if (CurrentView != ViewMode.TwoD || wheelchairIcon == null) return;

        if (avatar == null)
        {
            var go = GameObject.Find(avatarName);
            if (go != null) avatar = go.transform;
            else return;
        }

        var cam = miniMap != null ? miniMap.Cam : null;
        if (cam == null || twoDMap == null) return;

        // Project the avatar's world position through the top-down mini-map camera
        // into the full-screen RawImage's local rect.
        Vector3 vp = cam.WorldToViewportPoint(avatar.position);
        Rect r = twoDMap.rectTransform.rect;
        wheelchairIcon.anchoredPosition = new Vector2(
            (vp.x - 0.5f) * r.width,
            (vp.y - 0.5f) * r.height);

        // Map looks straight down (+Z = up on screen). The avatar's Y heading is
        // clockwise from +Z, so negate for UI's counter-clockwise Z rotation.
        wheelchairIcon.localEulerAngles = new Vector3(0f, 0f, -avatar.eulerAngles.y);
    }
}
