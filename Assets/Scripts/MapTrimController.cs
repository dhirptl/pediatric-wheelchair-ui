using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Admin-only "trim the map" overlay for the 2D view. The admin drags a keep-box
/// over the full-screen 2D map; everything outside is greyed out. CONFIRM converts
/// the box to a normalized crop of the occupancy grid, persists it, and reloads the
/// scene so the map rebuilds (and re-bakes its NavMesh) against the cropped, re-
/// centered, view-filling region. CANCEL closes without changes; RESET clears the
/// trim back to the full map.
///
/// Coordinates flow: keep-box (0..1 over the 2D RawImage) -> camera viewport ->
/// world (MiniMapController.Cam, the same path MiniMapClickHandler uses) ->
/// MapGenerator.WorldToCurrentNormalized -> MapGenerator.SetCropComposed. Going
/// through world correctly accounts for the mini-map camera's letterbox framing.
///
/// Mouse-driven setup tool; intentionally NOT part of the two-switch scan rings.
/// It grabs ScanFocus while open so stray Space/Enter don't drive panels behind it.
/// </summary>
public class MapTrimController : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [Header("Map references")]
    [Tooltip("The full-screen 2D map RawImage. Crop coordinates are measured against its rect.")]
    public RawImage twoDMap;
    [Tooltip("Mini-map camera controller (for viewport->world). Auto-found if empty.")]
    public MiniMapController miniMap;

    [Header("Keep-box visuals (anchors driven each refresh)")]
    public RectTransform keepBox;
    public RectTransform shadeLeft;
    public RectTransform shadeRight;
    public RectTransform shadeTop;
    public RectTransform shadeBottom;
    [Tooltip("Optional corner tick visuals, moved to the box corners. May be empty.")]
    public RectTransform[] cornerHandles;

    [Header("Interaction")]
    [Tooltip("Normalized distance from an edge that counts as grabbing it (resize vs move).")]
    public float edgeGrab = 0.05f;
    [Tooltip("Smallest keep-box the admin can shrink to, as a fraction of the view.")]
    public float minBox = 0.12f;

    // Keep-box as a normalized rect (0..1) over the 2D RawImage. min=(xMin,yMin).
    private Rect box = new Rect(0.1f, 0.1f, 0.8f, 0.8f);

    private enum Mode { None, Move, Left, Right, Top, Bottom, BL, BR, TL, TR }
    private Mode drag = Mode.None;
    private Vector2 dragStartNorm;
    private Rect dragStartBox;

    private MapGenerator mapGen;

    void Awake()
    {
        if (miniMap == null) miniMap = FindObjectOfType<MiniMapController>();
        mapGen = FindObjectOfType<MapGenerator>();
    }

    // --- Open / close (wired to the TRIM entry + CANCEL buttons) --------------

    public void Open()
    {
        gameObject.SetActive(true);
        if (mapGen == null) mapGen = FindObjectOfType<MapGenerator>();
        if (miniMap == null) miniMap = FindObjectOfType<MiniMapController>();
        box = new Rect(0.1f, 0.1f, 0.8f, 0.8f); // start inset so the margins are grabbable
        ScanFocus.Push(this);                    // freeze the two-switch keys while trimming
        Refresh();
    }

    public void Close()
    {
        ScanFocus.Pop(this);
        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        // Safety net if the overlay is hidden some other way (e.g. leaving 2D view).
        ScanFocus.Pop(this);
    }

    // --- Buttons -------------------------------------------------------------

    /// <summary>Apply the crop and reload so the map rebuilds against it.</summary>
    public void Confirm()
    {
        if (mapGen == null || miniMap == null || miniMap.Cam == null) { Close(); return; }

        // Box corners (0..1 over the RawImage == mini-map camera viewport) -> world.
        Vector3 wa = ViewportToWorld(box.xMin, box.yMin);
        Vector3 wb = ViewportToWorld(box.xMax, box.yMax);

        // World -> normalized over the currently built map (handles camera letterbox).
        Vector2 na = mapGen.WorldToCurrentNormalized(wa);
        Vector2 nb = mapGen.WorldToCurrentNormalized(wb);

        var keep = Rect.MinMaxRect(
            Mathf.Min(na.x, nb.x), Mathf.Min(na.y, nb.y),
            Mathf.Max(na.x, nb.x), Mathf.Max(na.y, nb.y));

        mapGen.SetCropComposed(keep);
        ScanFocus.Pop(this);
        ReloadScene();
    }

    /// <summary>Close without changing the map.</summary>
    public void Cancel() => Close();

    /// <summary>Clear the trim back to the full map and reload.</summary>
    public void ResetTrim()
    {
        if (mapGen == null) mapGen = FindObjectOfType<MapGenerator>();
        if (mapGen != null) mapGen.ResetCrop();
        ScanFocus.Pop(this);
        ReloadScene();
    }

    private static void ReloadScene()
    {
        Scene s = SceneManager.GetActiveScene();
        SceneManager.LoadScene(s.buildIndex >= 0 ? s.buildIndex : 0);
    }

    private Vector3 ViewportToWorld(float u, float v)
    {
        Camera cam = miniMap.Cam;
        Vector3 w = cam.ViewportToWorldPoint(new Vector3(u, v, cam.transform.position.y));
        w.y = 0f;
        return w;
    }

    // --- Dragging ------------------------------------------------------------

    public void OnPointerDown(PointerEventData e)
    {
        if (!TryNormalized(e, out Vector2 p)) { drag = Mode.None; return; }
        drag = Classify(p);
        dragStartNorm = p;
        dragStartBox = box;
    }

    public void OnDrag(PointerEventData e)
    {
        if (drag == Mode.None) return;
        if (!TryNormalized(e, out Vector2 p)) return;

        if (drag == Mode.Move)
        {
            Vector2 d = p - dragStartNorm;
            float x = Mathf.Clamp(dragStartBox.x + d.x, 0f, 1f - dragStartBox.width);
            float y = Mathf.Clamp(dragStartBox.y + d.y, 0f, 1f - dragStartBox.height);
            box = new Rect(x, y, dragStartBox.width, dragStartBox.height);
        }
        else
        {
            float xMin = box.xMin, xMax = box.xMax, yMin = box.yMin, yMax = box.yMax;
            if (drag == Mode.Left || drag == Mode.TL || drag == Mode.BL) xMin = Mathf.Clamp(p.x, 0f, xMax - minBox);
            if (drag == Mode.Right || drag == Mode.TR || drag == Mode.BR) xMax = Mathf.Clamp(p.x, xMin + minBox, 1f);
            if (drag == Mode.Bottom || drag == Mode.BL || drag == Mode.BR) yMin = Mathf.Clamp(p.y, 0f, yMax - minBox);
            if (drag == Mode.Top || drag == Mode.TL || drag == Mode.TR) yMax = Mathf.Clamp(p.y, yMin + minBox, 1f);
            box = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
        Refresh();
    }

    private Mode Classify(Vector2 p)
    {
        bool nearL = Mathf.Abs(p.x - box.xMin) < edgeGrab;
        bool nearR = Mathf.Abs(p.x - box.xMax) < edgeGrab;
        bool nearB = Mathf.Abs(p.y - box.yMin) < edgeGrab;
        bool nearT = Mathf.Abs(p.y - box.yMax) < edgeGrab;
        bool inX = p.x > box.xMin - edgeGrab && p.x < box.xMax + edgeGrab;
        bool inY = p.y > box.yMin - edgeGrab && p.y < box.yMax + edgeGrab;

        if (nearL && nearB && inX && inY) return Mode.BL;
        if (nearR && nearB && inX && inY) return Mode.BR;
        if (nearL && nearT && inX && inY) return Mode.TL;
        if (nearR && nearT && inX && inY) return Mode.TR;
        if (nearL && inY) return Mode.Left;
        if (nearR && inY) return Mode.Right;
        if (nearB && inX) return Mode.Bottom;
        if (nearT && inX) return Mode.Top;
        if (box.Contains(p)) return Mode.Move;
        return Mode.None;
    }

    private bool TryNormalized(PointerEventData e, out Vector2 norm)
    {
        norm = default;
        RectTransform rt = twoDMap != null ? twoDMap.rectTransform : (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out Vector2 local))
            return false;
        Rect r = rt.rect;
        norm = new Vector2((local.x - r.x) / r.width, (local.y - r.y) / r.height);
        return true;
    }

    // --- Visual refresh ------------------------------------------------------

    private void Refresh()
    {
        SetAnchors(keepBox, box.xMin, box.yMin, box.xMax, box.yMax);
        SetAnchors(shadeLeft, 0f, 0f, box.xMin, 1f);
        SetAnchors(shadeRight, box.xMax, 0f, 1f, 1f);
        SetAnchors(shadeTop, box.xMin, box.yMax, box.xMax, 1f);
        SetAnchors(shadeBottom, box.xMin, 0f, box.xMax, box.yMin);

        if (cornerHandles != null && cornerHandles.Length >= 4)
        {
            PlaceHandle(cornerHandles[0], box.xMin, box.yMin);
            PlaceHandle(cornerHandles[1], box.xMax, box.yMin);
            PlaceHandle(cornerHandles[2], box.xMin, box.yMax);
            PlaceHandle(cornerHandles[3], box.xMax, box.yMax);
        }
    }

    private static void SetAnchors(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void PlaceHandle(RectTransform rt, float ax, float ay)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
        rt.anchoredPosition = Vector2.zero;
    }
}
