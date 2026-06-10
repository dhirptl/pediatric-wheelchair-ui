public class SwitchExplorer
{
    public static string Execute()
    {
        var gmm = GameModeManager.Instance;
        if (gmm == null) return "no GameModeManager";
        gmm.SetModeExplorer();
        return "mode=" + gmm.CurrentMode + " controlsActive=" + (gmm.explorerDashboard != null && gmm.explorerDashboard.activeSelf);
    }
}
