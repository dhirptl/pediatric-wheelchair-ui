using UnityEngine;

/// <summary>
/// The seam between the Smart Guide UI and whatever is detecting the world.
///
/// RIGHT NOW it's backed by FollowAssistBackend (the SIMULATION placeholder that
/// stands in for the LIDAR). LATER, the real ROS 2 / LIDAR client implements this
/// same interface as a ROS2LidarFeed, and the only line that changes is where
/// SmartGuideController resolves its feed - the UI code never has to know which one
/// it's talking to.
/// </summary>
public interface ILidarFeed
{
    /// <summary>True if at least one target of this type is currently detected.</summary>
    bool HasTarget(SmartGuideTarget.TargetType type);

    /// <summary>The nearest detected target of a type (from the chair), or null if none.</summary>
    SmartGuideTarget Acquire(SmartGuideTarget.TargetType type, Vector3 fromChair);

    /// <summary>Live distance, in meters, from the chair to an already-acquired target.</summary>
    float DistanceTo(SmartGuideTarget target, Vector3 fromChair);
}
