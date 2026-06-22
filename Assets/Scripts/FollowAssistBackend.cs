using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The detection seam for Smart Guide, mirroring WheelchairStateBridge's
/// SIMULATION_PLACEHOLDER / ROS_CONNECTED pattern.
///
/// RIGHT NOW (SIMULATION_PLACEHOLDER): there is no LIDAR sensor wired to the GUI,
/// so "detected" targets are just the SmartGuideTarget markers placed in the scene.
/// LATER (LIDAR_CONNECTED): the real LIDAR feed will publish detected doorways,
/// caregivers, and walls/corridors, and this class will surface them through the
/// exact same GetNearbyTargets() API - so the Smart Guide UI never has to change.
/// </summary>
public class FollowAssistBackend : MonoBehaviour, ILidarFeed
{
    public enum DetectionSource { SIMULATION_PLACEHOLDER, LIDAR_CONNECTED }

    [Header("Architecture Toggle")]
    public DetectionSource currentMode = DetectionSource.SIMULATION_PLACEHOLDER;

    [Header("Simulation placement (placeholder only)")]
    [Tooltip("In SIMULATION mode, scatter the scene's target markers this far (meters) around the chair, snapped to the NavMesh, so it can actually drive to them and arrive. The real LIDAR feed will supply live positions instead.")]
    public float simSpawnRadius = 6f;
    public string avatarName = "Wheelchair_Avatar";

    public static FollowAssistBackend Instance { get; private set; }

    private readonly List<SmartGuideTarget> detected = new List<SmartGuideTarget>();
    private bool simPlaced;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // Once the NavMesh is baked and the chair has spawned, drop the placeholder
        // markers a few meters around the chair so Smart Guide has something
        // genuinely reachable to drive to. Runs once. (Sim mode only.)
        if (currentMode == DetectionSource.SIMULATION_PLACEHOLDER && !simPlaced)
            TryPlaceSimTargets();
    }

    private void TryPlaceSimTargets()
    {
        var avatar = GameObject.Find(avatarName);
        if (avatar == null) return;
        var targets = Object.FindObjectsByType<SmartGuideTarget>(FindObjectsSortMode.None);
        if (targets.Length == 0) return;

        Vector3 origin = avatar.transform.position;
        Vector3 fwd = avatar.transform.forward;
        // Fan the targets out in front of the chair (left / center / right).
        float[] angles = { -50f, 0f, 50f };
        bool anyPlaced = false;
        for (int i = 0; i < targets.Length; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, angles[i % angles.Length], 0f) * fwd;
            Vector3 desired = origin + dir.normalized * simSpawnRadius;
            if (NavMesh.SamplePosition(desired, out NavMeshHit hit, simSpawnRadius, NavMesh.AllAreas))
            {
                targets[i].transform.position = hit.position;
                anyPlaced = true;
            }
        }
        if (anyPlaced) simPlaced = true; // NavMesh ready; placed for this session.
    }

    /// <summary>All targets the chair can currently "see", to offer in the panel.</summary>
    public IReadOnlyList<SmartGuideTarget> GetNearbyTargets()
    {
        detected.Clear();
        if (currentMode == DetectionSource.SIMULATION_PLACEHOLDER)
        {
            // RIGHT NOW: the scene's placeholder markers stand in for LIDAR hits.
            detected.AddRange(Object.FindObjectsByType<SmartGuideTarget>(FindObjectsSortMode.None));
        }
        else
        {
            // LATER THIS SUMMER: read detections from the LIDAR feed and translate
            // each into a SmartGuideTarget (type + world position), e.g.:
            // foreach (var hit in lidarClient.LatestDetections) detected.Add(...);
        }
        return detected;
    }

    /// <summary>The nearest detected target of a given type, or null if none is seen.</summary>
    public SmartGuideTarget GetTarget(SmartGuideTarget.TargetType type, Vector3 from)
    {
        SmartGuideTarget best = null;
        float bestDist = float.MaxValue;
        foreach (var t in GetNearbyTargets())
        {
            if (t == null || t.type != type) continue;
            float d = t.DistanceTo(from);
            if (d < bestDist) { bestDist = d; best = t; }
        }
        return best;
    }

    /// <summary>True if at least one target of this type is currently detected.</summary>
    public bool HasTarget(SmartGuideTarget.TargetType type)
    {
        foreach (var t in GetNearbyTargets())
            if (t != null && t.type == type) return true;
        return false;
    }

    // --- ILidarFeed --------------------------------------------------------

    /// <summary>The nearest detected target of a type, or null. (ILidarFeed)</summary>
    public SmartGuideTarget Acquire(SmartGuideTarget.TargetType type, Vector3 fromChair)
        => GetTarget(type, fromChair);

    /// <summary>
    /// Live distance from the chair to an acquired target. In SIMULATION this is the
    /// geometric distance to the marker; the real LIDAR feed will return the sensor's
    /// measured range to the tracked object instead - same call site either way.
    /// </summary>
    public float DistanceTo(SmartGuideTarget target, Vector3 fromChair)
        => target != null ? target.DistanceTo(fromChair) : float.PositiveInfinity;
}
