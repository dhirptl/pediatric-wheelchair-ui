using UnityEngine;

/// <summary>
/// Sits on a child of the collectible and shows the currently equipped food as a
/// flat sprite that always faces the camera. Reads <see cref="FoodManager.EquippedIcon"/>
/// and live-updates when the player equips a different food. Kept renderer-agnostic
/// of the old coin mesh so pickup logic (distance-based in ScoreManager) is untouched.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class FoodBillboard : MonoBehaviour
{
    private SpriteRenderer sr;
    private FoodManager subscribed;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        TrySubscribe();
        Refresh();
    }

    void OnDisable()
    {
        if (subscribed != null) subscribed.OnFoodChanged -= Refresh;
        subscribed = null;
    }

    void LateUpdate()
    {
        // FoodManager may not have existed yet when we first enabled (spawn order);
        // keep trying until it's up, then stop re-checking.
        if (subscribed == null) { TrySubscribe(); Refresh(); }

        Camera cam = Camera.main;
        if (cam == null) return;
        // Face the camera; flip so the front of the sprite (its +Z) points at it.
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
    }

    private void TrySubscribe()
    {
        FoodManager fm = FoodManager.Instance;
        if (fm == null || fm == subscribed) return;
        if (subscribed != null) subscribed.OnFoodChanged -= Refresh;
        subscribed = fm;
        subscribed.OnFoodChanged += Refresh;
    }

    private void Refresh()
    {
        if (sr == null) return;
        FoodManager fm = FoodManager.Instance;
        if (fm != null) sr.sprite = fm.EquippedIcon;
    }
}
