using UnityEngine;

/// <summary>
/// Marks one thing in the world that Smart Guide can offer to follow - a doorway,
/// a caregiver ("grown-up"), or a wall/corridor to ride alongside.
///
/// Right now these are placed by hand in the scene so the simulation has something
/// to detect and drive toward. Once the LIDAR sensor is wired to the GUI,
/// FollowAssistBackend will spawn/refresh these from the real detection feed
/// instead, and the rest of the Smart Guide UI keeps working unchanged.
/// </summary>
public class SmartGuideTarget : MonoBehaviour
{
    public enum TargetType { Doorway, Caregiver, WallCorridor }

    [Tooltip("Which kind of thing this is. The Smart Guide panel offers one button per detected type.")]
    public TargetType type = TargetType.Doorway;

    /// <summary>Distance from a world position to this target, in meters.</summary>
    public float DistanceTo(Vector3 from)
    {
        return Vector3.Distance(from, transform.position);
    }

    /// <summary>Kid-friendly label for the chosen type ("doorway", "grown-up", "wall").</summary>
    public static string FriendlyName(TargetType t)
    {
        switch (t)
        {
            case TargetType.Doorway:      return "doorway";
            case TargetType.Caregiver:    return "grown-up";
            case TargetType.WallCorridor: return "wall";
            default:                      return "spot";
        }
    }
}
