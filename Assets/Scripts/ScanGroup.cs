using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable switch-access scanning over a set of buttons. Owns the shared
/// highlight/selection loop that the main menu, explorer dashboard, grid menu,
/// and (later) destination cards all use - controllers just call Tick() each
/// frame and react to OnOptionSelected. Supports linear scanning and two-stage
/// row/column grid scanning (gridCols > 1).
/// </summary>
[Serializable]
public class ScanGroup
{
    public Button[] options;
    [Tooltip("Columns for two-stage row/column grid scanning; 0 or 1 = linear scan.")]
    public int gridCols = 0;
    public SwitchScanner scanner = new SwitchScanner();
    [Tooltip("Highlight scale for buttons that have no ButtonHighlighter component.")]
    public float fallbackScale = 1.12f;

    public event Action<int> OnOptionSelected;

    private ButtonHighlighter[] highlighters; // cached per option; null entries allowed

    /// <summary>Call when this group's panel becomes the active scan target.</summary>
    public void Activate()
    {
        CacheHighlighters();
        scanner.Reset();
        Refresh();
    }

    /// <summary>Clears every highlight (call when the panel goes inactive).</summary>
    public void Deactivate()
    {
        if (options == null) return;
        for (int i = 0; i < options.Length; i++) SetHighlight(i, false);
    }

    /// <summary>Call every frame while active. Returns the selected index, or -1.</summary>
    public int Tick()
    {
        if (options == null || options.Length == 0) return -1;
        if (highlighters == null || highlighters.Length != options.Length) CacheHighlighters();

        int selected = (gridCols > 1)
            ? scanner.TickGrid(Mathf.CeilToInt(options.Length / (float)gridCols), gridCols, options.Length)
            : scanner.Tick(options.Length);

        Refresh();
        if (selected >= 0) OnOptionSelected?.Invoke(selected);
        return selected;
    }

    private void Refresh()
    {
        for (int i = 0; i < options.Length; i++) SetHighlight(i, IsHighlighted(i));
    }

    private bool IsHighlighted(int i)
    {
        if (gridCols > 1)
        {
            // Row stage lights the whole row; column stage lights the single cell.
            if (!scanner.InColumnStage) return i / gridCols == scanner.CurrentRow;
            return i == scanner.CurrentRow * gridCols + scanner.CurrentCol;
        }
        return i == scanner.CurrentIndex;
    }

    private void SetHighlight(int i, bool on)
    {
        var button = options[i];
        if (button == null) return;
        var hl = highlighters != null && i < highlighters.Length ? highlighters[i] : null;
        if (hl != null)
        {
            hl.SetHighlighted(on);
            return;
        }
        float s = on ? fallbackScale : 1f;
        button.transform.localScale = new Vector3(s, s, 1f);
    }

    private void CacheHighlighters()
    {
        if (options == null) { highlighters = null; return; }
        highlighters = new ButtonHighlighter[options.Length];
        for (int i = 0; i < options.Length; i++)
            if (options[i] != null) highlighters[i] = options[i].GetComponent<ButtonHighlighter>();
    }
}
