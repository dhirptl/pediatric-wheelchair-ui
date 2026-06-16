using UnityEngine;

/// <summary>
/// Spins the driven wheels and swivels the casters of the wheelchair model from the
/// avatar's actual motion. It reads the root transform's frame-to-frame displacement
/// and heading change, so it is fully decoupled from how motion is produced - it works
/// for both the NavMesh goal path and the low-level velocity path in
/// <see cref="WheelchairStateBridge"/>, and will keep working when ROS odometry drives
/// the same transform.
///
/// Mesh axes after the URDF import: a driven wheel rolls about its local +X; a caster
/// swivels about its local +Y (vertical).
/// </summary>
public class WheelchairWheelAnimator : MonoBehaviour
{
    [Header("Wheels (assigned by the importer / inspector)")]
    [Tooltip("The two large powered wheels (left, right).")]
    public Transform[] drivenWheels;
    [Tooltip("The four caster wheels.")]
    public Transform[] casters;

    [Header("Geometry (meters)")]
    public float drivenRadius = 0.127f;
    public float casterRadius = 0.076f;
    [Tooltip("Distance from center to a driven wheel, for turn-in-place spin.")]
    public float halfTrack = 0.31115f;

    [Header("Feel")]
    [Tooltip("How quickly casters rotate to face the travel direction.")]
    public float casterTurnSpeed = 12f;

    Transform root;
    Vector3 lastPos;
    float lastYaw;

    void Start()
    {
        root = transform.root;
        lastPos = root.position;
        lastYaw = root.eulerAngles.y;
    }

    void Update()
    {
        if (root == null) return;

        Vector3 delta = root.position - lastPos;
        float forwardDelta = Vector3.Dot(delta, root.forward);
        float yawDelta = Mathf.DeltaAngle(lastYaw, root.eulerAngles.y); // degrees

        // --- Driven wheels: roll with forward travel, counter-rotate on turns. ---
        float rollDeg = (forwardDelta / Mathf.Max(drivenRadius, 1e-4f)) * Mathf.Rad2Deg;
        // Arc each driven wheel travels when turning in place (left +, right -).
        float turnArc = (yawDelta * Mathf.Deg2Rad * halfTrack / Mathf.Max(drivenRadius, 1e-4f)) * Mathf.Rad2Deg;
        if (drivenWheels != null)
        {
            for (int i = 0; i < drivenWheels.Length; i++)
            {
                if (drivenWheels[i] == null) continue;
                float sign = (i == 0) ? -1f : 1f; // left wheel rolls backward when turning right
                drivenWheels[i].Rotate(Vector3.right, rollDeg + sign * turnArc, Space.Self);
            }
        }

        // --- Casters: roll and swivel toward the travel direction. ---
        float casterRoll = (forwardDelta / Mathf.Max(casterRadius, 1e-4f)) * Mathf.Rad2Deg;
        Vector3 flatVel = new Vector3(delta.x, 0f, delta.z);
        bool moving = flatVel.sqrMagnitude > 1e-8f;
        if (casters != null)
        {
            foreach (var c in casters)
            {
                if (c == null) continue;
                if (moving)
                {
                    Quaternion target = Quaternion.LookRotation(flatVel.normalized, Vector3.up);
                    c.rotation = Quaternion.Slerp(c.rotation, target, Time.deltaTime * casterTurnSpeed);
                }
                c.Rotate(Vector3.right, casterRoll, Space.Self);
            }
        }

        lastPos = root.position;
        lastYaw = root.eulerAngles.y;
    }
}
