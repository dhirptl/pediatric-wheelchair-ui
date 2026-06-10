using UnityEngine;
using UnityEngine.AI;
using System.Text;

/// <summary>
/// Run in PLAY mode (map generated). For each Target_* marker, finds the clear
/// occupancy-grid cell nearest its assigned map quadrant, NavMesh-validated.
/// Prints "name|x|y|z" lines to apply in edit mode afterwards.
/// </summary>
public class PlaceTargets
{
    public static string Execute()
    {
        var map = Object.FindObjectOfType<MapGenerator>();
        if (map == null || map.OccupiedCells == null) return "map not ready";

        var targets = new[]
        {
            new { name = "Target_Kitchen",    fx = 0.30f, fy = 0.30f },
            new { name = "Target_Bathroom",   fx = 0.70f, fy = 0.30f },
            new { name = "Target_LivingRoom", fx = 0.30f, fy = 0.70f },
            new { name = "Target_Bedroom",    fx = 0.70f, fy = 0.70f },
        };

        var sb = new StringBuilder();
        foreach (var t in targets)
        {
            if (TryFindClearNear(map, t.fx, t.fy, 2, out Vector3 pos))
                sb.AppendLine(t.name + "|" + pos.x + "|" + pos.y + "|" + pos.z);
            else
                sb.AppendLine(t.name + "|FAILED");
        }
        return sb.ToString();
    }

    static bool TryFindClearNear(MapGenerator map, float fx, float fy, int clearance, out Vector3 world)
    {
        world = Vector3.zero;
        float cx0 = map.Cols * fx, cy0 = map.Rows * fy;
        int bestX = -1, bestY = -1;
        float best = float.MaxValue;

        for (int x = clearance; x < map.Cols - clearance; x++)
        {
            for (int y = clearance; y < map.Rows - clearance; y++)
            {
                if (!Clear(map, x, y, clearance)) continue;
                float dx = x - cx0, dy = y - cy0;
                float d = dx * dx + dy * dy;
                if (d < best) { best = d; bestX = x; bestY = y; }
            }
        }
        if (bestX < 0) return false;

        Vector3 candidate = map.CellToWorld(bestX, bestY);
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4f, NavMesh.AllAreas)) return false;
        world = hit.position;
        return true;
    }

    static bool Clear(MapGenerator map, int cx, int cy, int r)
    {
        for (int x = cx - r; x <= cx + r; x++)
            for (int y = cy - r; y <= cy + r; y++)
                if (map.OccupiedCells[x, y]) return false;
        return true;
    }
}
