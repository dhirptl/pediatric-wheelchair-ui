using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global focus stack so only one switch-access scan context consumes the
/// Space/Enter keys at a time. The Explorer command panel scans only while the
/// stack is empty; any overlay panel (pause, destination, theme shop) pushes
/// itself on open and pops on close, so opening Settings hands the two keys to
/// the overlay and closing it returns them to driving - no double-triggering.
/// </summary>
public static class ScanFocus
{
    private static readonly List<MonoBehaviour> stack = new List<MonoBehaviour>();

    // Drop entries whose owner was destroyed (e.g. a focus holder left on the stack
    // across a scene reload - the stack is static and survives LoadScene). Unity's
    // overloaded == makes a destroyed MonoBehaviour compare equal to null. Without
    // this a stale entry would silently block Explorer driving after a reload.
    private static void Prune()
    {
        for (int i = stack.Count - 1; i >= 0; i--)
            if (stack[i] == null) stack.RemoveAt(i);
    }

    /// <summary>True when nothing is grabbing focus (Explorer driving is live).</summary>
    public static bool IsEmpty { get { Prune(); return stack.Count == 0; } }

    /// <summary>Becomes the active scan target (added once; moved to top if present).</summary>
    public static void Push(MonoBehaviour owner)
    {
        if (owner == null) return;
        Prune();
        stack.Remove(owner);
        stack.Add(owner);
    }

    /// <summary>Releases focus. Safe to call out of order (handles odd disable order).</summary>
    public static void Pop(MonoBehaviour owner)
    {
        if (owner == null) return;
        stack.Remove(owner);
        Prune();
    }

    /// <summary>True only for the top-most owner - the one that should read input.</summary>
    public static bool IsTop(MonoBehaviour owner)
    {
        Prune();
        return stack.Count > 0 && stack[stack.Count - 1] == owner;
    }
}
