using UnityEngine;

/// <summary>
/// A floating, spinning path coin. Deliberately has NO collider: pickup is a
/// distance check in ScoreManager against the bridge's pose event, so the same
/// logic keeps working when poses come from ROS /odom instead of Unity physics.
/// </summary>
public class Coin : MonoBehaviour
{
    public float spinDegPerSec = 140f;
    public float bobAmplitude = 0.15f;
    public float bobHz = 1f;
    [Tooltip("Seconds the star pop plays before the coin fully deactivates.")]
    public float popLifetime = 0.8f;

    private Renderer bodyRenderer;
    private ParticleSystem starPop;
    private float baseY;
    private bool active;

    public Vector3 Position => transform.position;
    public bool IsActive => active;

    void Awake()
    {
        // The visible body is now the food billboard (a SpriteRenderer). Target it
        // explicitly so Collect()/Spawn() toggle the food image, not the leftover
        // (disabled) coin cylinder or the particle-system renderer.
        bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();
        starPop = GetComponentInChildren<ParticleSystem>();
    }

    public void Spawn(Vector3 position)
    {
        CancelInvoke();
        transform.position = position;
        baseY = position.y;
        gameObject.SetActive(true);
        if (bodyRenderer != null) bodyRenderer.enabled = true;
        active = true;
    }

    public void Despawn()
    {
        active = false;
        CancelInvoke();
        gameObject.SetActive(false);
    }

    /// <summary>Star particle pop, then vanish (renderer off immediately).</summary>
    public void Collect()
    {
        if (!active) return;
        active = false;
        if (bodyRenderer != null) bodyRenderer.enabled = false;
        if (starPop != null) starPop.Play();
        Invoke(nameof(FinishCollect), popLifetime);
    }

    private void FinishCollect()
    {
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (!active) return;
        transform.Rotate(0f, spinDegPerSec * Time.deltaTime, 0f, Space.World);
        Vector3 p = transform.position;
        p.y = baseY + Mathf.Sin(Time.time * 2f * Mathf.PI * bobHz) * bobAmplitude;
        transform.position = p;
    }
}
