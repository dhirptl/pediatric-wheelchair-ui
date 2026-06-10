using UnityEditor;

public class DeleteClickToMove
{
    public static string Execute()
    {
        bool ok = AssetDatabase.DeleteAsset("Assets/Scripts/ClickToMove.cs");
        AssetDatabase.Refresh();
        return "Deleted Assets/Scripts/ClickToMove.cs: " + ok;
    }
}
