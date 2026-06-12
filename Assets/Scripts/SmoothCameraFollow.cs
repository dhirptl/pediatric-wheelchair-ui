using UnityEngine;

/// <summary>
/// Gentle third-person follow camera for the wheelchair. Replaces parenting the
/// camera under the avatar (which whipped the whole view around on every turn):
/// position eases with SmoothDamp and yaw trails the chair with its own slower
/// smoothing, so turns read as the WORLD gliding by - calmer for a child and
/// less motion-sickness-prone on a fixed mounted screen.
/// </summary>
public class SmoothCameraFollow : MonoBehaviour
{
    [Tooltip("What to follow. Auto-found by name if left empty.")]
    public Transform target;
    public string targetName = "Wheelchair_Avatar";

    [Header("Framing")]
    [Tooltip("Camera offset in the target's local frame (back and up).")]
    public Vector3 localOffset = new Vector3(0f, 9f, -7f);
    [Tooltip("Extra height above the target the camera looks at.")]
    public float lookHeight = 1.2f;

    [Header("Smoothing")]
    [Tooltip("Seconds the position takes to catch up (SmoothDamp).")]
    public float positionSmoothTime = 0.35f;
    [Tooltip("Seconds the yaw takes to catch up. Larger = lazier, calmer turns.")]
    public float yawSmoothTime = 0.6f;

    private Vector3 posVelocity;
    private float yawVelocity;
    private float yaw;

    void Start()
    {
        if (target == null)
        {
            var go = GameObject.Find(targetName);
            if (go != null) target = go.transform;
        }
        if (target != null)
        {
            yaw = target.eulerAngles.y;
            SnapToTarget();
        }
    }

    /// <summary>Jump straight to the framed position (no easing) - e.g. after a spawn warp.</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        yaw = target.eulerAngles.y;
        transform.position = DesiredPosition();
        transform.rotation = DesiredRotation();
        posVelocity = Vector3.zero;
        yawVelocity = 0f;
    }

    void LateUpdate()
    {
        if (target == null) return;
        yaw = Mathf.SmoothDampAngle(yaw, target.eulerAngles.y, ref yawVelocity, yawSmoothTime);
        transform.position = Vector3.SmoothDamp(transform.position, DesiredPosition(), ref posVelocity, positionSmoothTime);
        transform.rotation = DesiredRotation();
    }

    private Vector3 DesiredPosition()
    {
        return target.position + Quaternion.Euler(0f, yaw, 0f) * localOffset;
    }

    private Quaternion DesiredRotation()
    {
        Vector3 look = target.position + Vector3.up * lookHeight - transform.position;
        return look.sqrMagnitude > 0.001f ? Quaternion.LookRotation(look) : transform.rotation;
    }
}
