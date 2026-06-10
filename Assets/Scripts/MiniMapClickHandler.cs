using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Converts clicks/taps on the mini-map RawImage into world-floor coordinates:
/// screen point -> RawImage local rect -> 0..1 viewport -> orthographic camera
/// world point on the y=0 plane. Used by the admin room-calibration flow.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class MiniMapClickHandler : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("The mini-map camera controller. Auto-found if left empty.")]
    public MiniMapController miniMap;

    public static event Action<Vector3> OnMiniMapWorldClick;

    private RawImage image;

    void Awake()
    {
        image = GetComponent<RawImage>();
        if (miniMap == null) miniMap = FindObjectOfType<MiniMapController>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (miniMap == null || miniMap.Cam == null) return;

        var rt = (RectTransform)image.transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return;

        // Local point -> normalized 0..1 across the RawImage (the RT fills it 1:1).
        Rect r = rt.rect;
        var viewport = new Vector2((local.x - r.x) / r.width, (local.y - r.y) / r.height);
        if (viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) return;

        // Top-down ortho camera: the y=0 floor plane sits at distance = camera height.
        Camera cam = miniMap.Cam;
        Vector3 world = cam.ViewportToWorldPoint(
            new Vector3(viewport.x, viewport.y, cam.transform.position.y));
        world.y = 0f;

        OnMiniMapWorldClick?.Invoke(world);
    }
}
