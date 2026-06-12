using UnityEngine;

/// <summary>
/// Gentle hover bob for the avatar's visual rig - the bot floats softly on its
/// glow ring. Slow sine (well under any flashing concern), purely cosmetic;
/// the collider/agent on the parent are untouched.
/// </summary>
public class BotIdleBob : MonoBehaviour
{
    [Tooltip("Vertical travel in meters.")]
    public float amplitude = 0.06f;
    [Tooltip("Bob cycles per second.")]
    public float hz = 0.5f;

    private Vector3 basePos;

    void Awake()
    {
        basePos = transform.localPosition;
    }

    void Update()
    {
        float y = Mathf.Sin(Time.time * 2f * Mathf.PI * hz) * amplitude;
        transform.localPosition = basePos + new Vector3(0f, y, 0f);
    }
}
